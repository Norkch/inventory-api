# Design

## Context

- `db/init.sql` ya crea `dbo.Suppliers`, `dbo.ProductSuppliers`, `UX_Suppliers_Nit (Nit)`, `UX_ProductSuppliers_Link (ProductId, SupplierId)`, `IX_ProductSuppliers_SupplierId` e `InventoryMovements.SupplierId` con `CK_InventoryMovements_Supplier CHECK (MovementType = 1 OR SupplierId IS NULL)`. `Suppliers.Nit` **no** declara collation, al contrario que `Products.Sku`.
- `ProductWriteRepository` ya resuelve dos casos parecidos:
  - comprueba que la categoría está activa y la bloquea en la misma sentencia con `WHERE EXISTS (SELECT 1 FROM dbo.Categories WITH (UPDLOCK, HOLDLOCK) ...)`;
  - traduce los errores 2601/2627 del índice único a `ConflictException`, tras una comprobación previa (`ExistsWithSameSku`) que da el mensaje en el caso normal.
- `MovementWriteRepository` registra el movimiento en una transacción: un `UPDATE` condicional del stock, el diagnóstico si afecta 0 filas y el `INSERT` con `SupplierId = NULL`. El handler traduce `MovementRegistrationStatus` a excepciones (D4 de `add-inventory-movements`).
- Se reutilizan sin cambios `Guard`, las excepciones de dominio, `PageRequest`, `PagedResult<T>`, `IClock`, `SqlConnectionFactory`, `ExceptionHandlingMiddleware`, `InvalidModelStateResponseFactory`, las políticas de scope y `CqrsSeparationTests`.
- No hay `TestHost` ni proveedor en memoria: los repositorios y la concurrencia se verifican a mano con Docker, como en los cambios anteriores.

Motivación en `proposal.md`. Requisitos en `specs/suppliers`, `specs/product-suppliers` y `specs/inventory-movements`.

## Goals / Non-Goals

**Goals:**
- Que la base garantice BR-08 (NIT único sin distinguir mayúsculas), BR-10 (pareja única) y BR-09 (ningún uso de un proveedor después de desactivarlo), también con peticiones simultáneas.
- Que los handlers decidan cada código (400, 404 y 409) a partir de un resultado tipado del puerto de escritura, y que esa decisión se pruebe con fakes.
- Mantener la separación CQRS y el patrón de los cambios anteriores: una sentencia condicional y un diagnóstico cuando afecta 0 filas.

**Non-Goals:**
- Pruebas automáticas contra SQL Server de los repositorios y de la concurrencia.
- Validación de correo conforme a la RFC 5322 o comprobación de que el dominio existe.
- Normalizar el NIT (quitar puntos o el dígito de verificación) antes de compararlo. Solo se recorta.

## Decisions

### D1. Dónde vive cada pieza

| Capa | Proveedores | Asociaciones | Movimientos (cambios) |
|---|---|---|---|
| Domain | `Supplier`, `SupplierDraft`, `SupplierChanges`, `SupplierRules` | `ProductSupplier`, `ProductSupplierDraft`, `ProductSupplierRules` | `MovementDraft` y `InventoryMovement` añaden `SupplierId`; `MovementRules.ValidateSupplier` |
| Application `Suppliers/` | `CreateSupplier`, `UpdateSupplier`, `DeactivateSupplier`, `GetSupplierById`, `ListSuppliers`; `ISupplierWriteRepository`, `ISupplierReadRepository`, `SupplierDto`, `SupplierMessages` | — | — |
| Application `ProductSuppliers/` | — | `LinkProductSupplier`, `GetProductSupplier`, `ListProductSuppliers`, `ListSupplierProducts`; `IProductSupplierWriteRepository`, `IProductSupplierReadRepository`, `ProductSupplierDto`, `ProductSupplierLinkStatus`, `ProductSupplierMessages` | — |
| Application `Movements/` | — | — | `MovementRegistrationStatus.SupplierNotAvailable`, `MovementDto.SupplierId`, `MovementMessages` |
| Infrastructure | `SupplierWriteRepository` (Dapper), `SupplierReadRepository` (EF Core), `SupplierRecord` | `ProductSupplierWriteRepository` (Dapper), `ProductSupplierReadRepository` (EF Core), `ProductSupplierRecord` | `MovementWriteRepository`, `MovementRecord.SupplierId` |
| Api | `SuppliersController`, `CreateSupplierRequest`, `UpdateSupplierRequest`, `AddSuppliers` | `ProductSuppliersController`, `LinkProductSupplierRequest`, `AddProductSuppliers` | `RegisterMovementRequest.SupplierId` |

Cada comando o consulta es su propia clase con su handler, como en productos. El 404 de un producto reutiliza `ProductMessages.ActiveProductNotFound`.

### D2. Validación en el dominio
- `SupplierRules` valida y normaliza `name`, `nit`, `email` y `phone`, y `ProductSupplierRules` hace lo mismo con `supplierSku`. Todas lanzan `ValidationException` con mensajes en español y siguen el patrón de `ProductRules`. El orden es nombre, NIT, correo y teléfono.
- **Correo:** se recorta, una cadena vacía da `null` y se comprueba la longitud (200) antes del formato. El formato es `^[^@\s]+@[^@\s]+\.[^@\s]+$` con `RegexOptions.CultureInvariant` y un tiempo máximo de evaluación. El patrón no tiene cuantificadores anidados, así que no hay retroceso catastrófico. Se guarda tal cual, sin pasarlo a minúsculas.
- **Teléfono:** se recorta, una cadena vacía da `null` y se comprueba la longitud (30). Después se comprueban dos cosas por separado:
  - que cumple `^\+?[0-9 -]+$`;
  - que tiene entre 7 y 15 dígitos, contados con `char.IsDigit` sobre `0-9`.

  Las dos fallan con el mismo mensaje de formato.
- **NIT en el `PUT`:** no se valida porque no forma parte de `UpdateSupplierRequest`. System.Text.Json ignora las propiedades desconocidas.
- **Salida con proveedor:** `MovementRules.ValidateSupplier(MovementType, Guid?)` lanza `ValidationException` si el tipo es `Exit` y `supplierId` tiene valor. Se llama desde `InventoryMovement.Create`, después de validar el tipo y la cantidad. Así el 400 llega antes de llamar al repositorio, que es lo que pide el orden de errores. `CK_InventoryMovements_Supplier` queda como última barrera.
- **Alternativa descartada:** validar el formato con `[EmailAddress]` o `MailAddress`. Aceptan formas que la propuesta no pide, como `juan@mail` sin extensión con `MailAddress`, y esa validación quedaría en Api en lugar de en el dominio.

### D3. NIT único sin distinguir mayúsculas
- `db/init.sql` declara `Nit NVARCHAR(30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL` en el `CREATE TABLE`. Para las bases existentes, repite la migración idempotente de `Products.Sku`:
  1. si la collation es distinta, borra `UX_Suppliers_Nit`;
  2. hace `ALTER COLUMN` con la collation;
  3. el bloque de índices existente vuelve a crear `UX_Suppliers_Nit`, sin filtro, porque el NIT queda reservado también para los desactivados.
- `SupplierWriteRepository.CreateAsync`:
  - `ExistsWithSameNitAsync` (comprobación previa, sin bloqueo) para dar el 409 con mensaje en el caso normal;
  - el `INSERT`, que captura `SqlException` 2601/2627 y lo traduce a `ConflictException` con el mismo mensaje. Esto cubre dos altas simultáneas.
- El handler valida el formato antes de llamar al repositorio, así que el orden 400 → 409 sale solo.
- **Alternativa descartada:** comparar con `UPPER()`. Es el mismo argumento de DEC-003: el índice no protegería las altas simultáneas.

### D4. Alta de la asociación: una sentencia condicional con bloqueo del proveedor
`ProductSupplierWriteRepository.LinkAsync(ProductSupplier link, ...)` ejecuta una sola sentencia, sin transacción explícita:

```sql
INSERT INTO dbo.ProductSuppliers (Id, ProductId, SupplierId, SupplierSku, CreatedAtUtc)
SELECT @Id, @ProductId, @SupplierId, @SupplierSku, @CreatedAtUtc
 WHERE EXISTS (SELECT 1 FROM dbo.Products WHERE Id = @ProductId AND IsActive = 1)
   AND EXISTS (SELECT 1 FROM dbo.Suppliers WITH (UPDLOCK, HOLDLOCK)
                WHERE Id = @SupplierId AND IsActive = 1);
```

```
1 fila                   --> Linked
0 filas                  --> producto activo?   no --> ProductNotFound      (404)
                             si                     --> SupplierNotAvailable (400)
SqlException 2601/2627   --> AlreadyLinked                                   (409)
```

- **Orden 404 → 400 → 409:** el índice único solo se comprueba si la fila llega a insertarse, es decir, si el producto y el proveedor están activos. Por eso una pareja repetida con el proveedor desactivado responde 400, como pide la spec, sin necesidad de una comprobación previa de la pareja.
- **BR-09 con desactivación simultánea:** el `UPDLOCK, HOLDLOCK` retiene la fila del proveedor hasta que termina la sentencia. El `UPDATE` de desactivación espera, o el alta espera a que termine la desactivación y ve `IsActive = 0`.
- **El producto no se bloquea:** borrar un producto no comprueba sus asociaciones, y una asociación con un producto borrado queda invisible en todas las lecturas. Bloquearlo solo añadiría esperas sin cambiar ninguna respuesta observable.
- **Resultado tipado:** `ProductSupplierLinkStatus` (`Linked`, `ProductNotFound`, `SupplierNotAvailable`, `AlreadyLinked`). `LinkProductSupplierHandler` lo traduce a `NotFoundException`, `ValidationException` o `ConflictException` (patrón D4 de `add-inventory-movements`).
- **Respuesta:** el 201 necesita `productName` y `supplierName`. El puerto de escritura los devuelve con un `OUTPUT` o un `SELECT` tras el `INSERT`, en la misma conexión, para que el handler de comando no use el puerto de lectura.

### D5. Entrada con proveedor dentro de la transacción del movimiento
`MovementWriteRepository.RegisterAsync` añade un paso **antes** del `UPDATE` del stock, solo cuando `SupplierId` tiene valor:

```
BEGIN TRAN
  [si hay supplierId]
  SELECT CASE WHEN EXISTS (
      SELECT 1 FROM dbo.Suppliers s WITH (UPDLOCK, HOLDLOCK)
        JOIN dbo.ProductSuppliers ps ON ps.SupplierId = s.Id AND ps.ProductId = @ProductId
       WHERE s.Id = @SupplierId AND s.IsActive = 1) ...
     no  --> producto activo?  no --> ROLLBACK --> ProductNotFound       (404)
                               si --> ROLLBACK --> SupplierNotAvailable  (400)
  UPDATE condicional del stock (sin cambios)
     0 filas --> ProductNotFound (404) o InsufficientStock (409)
     8115    --> StockOverflow (409)
  INSERT movimiento con @SupplierId
COMMIT
```

- **Orden:** cuerpo (dominio, D2), producto, proveedor y stock. Si el proveedor falla, se diagnostica el producto para que un producto borrado siga dando 404. Si el producto se borra entre la comprobación del proveedor y el `UPDATE`, este afecta 0 filas y da 404, que es correcto para ese instante.
- **Bloqueo:** el hint se aplica a la tabla `Suppliers`, y la fila queda retenida hasta el `COMMIT` o el `ROLLBACK`. Las asociaciones no se bloquean: nunca se editan ni se borran, así que la que existe ya no puede desaparecer.
- **Orden de bloqueos:** proveedor y después producto. Ninguna otra operación bloquea primero el producto y después el proveedor. El alta de asociación (D4) no bloquea el producto, y la desactivación del proveedor y el borrado del producto tocan una sola fila. No hay ciclo de interbloqueo.
- **`MovementRegistrationStatus.SupplierNotAvailable`:** `RegisterMovementHandler` lo traduce a `ValidationException` con `MovementMessages.SupplierNotAvailable`.
- **Alternativa descartada:** comprobar el proveedor en el handler antes de llamar al repositorio. Saldría de la transacción y reabriría la carrera de BR-09.

### D6. Proveedores: edición y desactivación
- **`UpdateAsync`:**

  ```sql
  UPDATE dbo.Suppliers SET Name = @Name, Email = @Email, Phone = @Phone, UpdatedAtUtc = @UpdatedAtUtc OUTPUT INSERTED.* WHERE Id = @Id AND IsActive = 1
  ```

  Si no devuelve ninguna fila, el resultado es 404. `Nit` y `CreatedAtUtc` nunca forman parte del `SET`.
- **`DeactivateAsync`:**

  ```sql
  UPDATE dbo.Suppliers SET IsActive = 0, UpdatedAtUtc = @UpdatedAtUtc WHERE Id = @Id AND IsActive = 1
  ```

  0 filas da 404 y 1 fila da 204. Fija `UpdatedAtUtc`, igual que el borrado de un producto. No toca `ProductSuppliers` ni `InventoryMovements`.
- **Reactivación:** no existe ningún endpoint para reactivar un proveedor; es la garantía de BR-09.

### D7. Lecturas con EF Core
- `SupplierRecord` y `ProductSupplierRecord` se mapean en `InventoryReadDbContext` (`dbo.Suppliers` y `dbo.ProductSuppliers`). `MovementRecord` añade `SupplierId`. Todas las consultas usan `AsNoTracking()` explícito.
- **`SupplierReadRepository`:**
  - `FindActiveAsync`;
  - `ListAsync`, con `Where(IsActive)`, orden `OrderBy(Name).ThenBy(Id)`, `CountAsync`, `Skip` y `Take`.
- **`ProductSupplierReadRepository`:**
  - `FindVisibleAsync(productId, supplierId)`: une `ProductSuppliers`, `Products` (activo) y `Suppliers` (activo). Si no hay fila, el resultado es 404.
  - `ExistsActiveProductAsync` y `ListByProductAsync`: une con `Suppliers` activos y ordena por `Supplier.Name` y luego `SupplierId`.
  - `ExistsActiveSupplierAsync` y `ListBySupplierAsync`: une con `Products` activos y ordena por `Product.Name` y luego `ProductId`.
- **Handlers de listado:** siguen el patrón de `ListMovementsHandler`:
  1. `PageRequest.Create` (400);
  2. existencia del producto o del proveedor de la ruta (404);
  3. listado.
- **Representación de la asociación:** `productId`, `productName`, `supplierId`, `supplierName`, `supplierSku` y `createdAtUtc`. Es la misma en los cinco endpoints. Los nombres hacen falta para que los listados ordenados por nombre se entiendan sin otra consulta, igual que `categoryName` en productos.

### D8. Controladores y rutas
- `SuppliersController` (`api/suppliers`) tiene `POST`, `GET {id:guid}`, `GET`, `PUT {id:guid}` y `DELETE {id:guid}`, con las políticas `InventoryWrite` e `InventoryRead`. El `POST` responde `CreatedAtAction` hacia `GET {id}`.
- `ProductSuppliersController` tiene:
  - `POST api/products/{productId:guid}/suppliers`;
  - `GET api/products/{productId:guid}/suppliers/{supplierId:guid}`;
  - `GET api/products/{productId:guid}/suppliers`;
  - `GET api/suppliers/{supplierId:guid}/products`, con una ruta absoluta en la acción.

  No tiene `PUT`, `PATCH` ni `DELETE`. Como la ruta de `GET` existe, ASP.NET Core responde 405 a los otros métodos, igual que en movimientos. El `POST` responde `CreatedAtAction` hacia `GET .../suppliers/{supplierId}`.
- `RegisterMovementRequest` añade `Guid? SupplierId`. Un valor que no es GUID provoca un error de deserialización, que se responde con el 400 genérico de JSON no válido.
- **Alternativa descartada:** poner `GET api/suppliers/{id}/products` en `SuppliersController`. Ese controlador dependería de los handlers de asociaciones, y la vista inversa es parte de la capacidad `product-suppliers`.

## Risks / Trade-offs

- [`UPDLOCK` en el proveedor serializa las entradas simultáneas que indican el **mismo** proveedor, y las altas de asociación con él, mientras dura cada transacción] → La transacción es corta (tres sentencias). Se mantiene el mismo hint que en categorías por coherencia. Un `HOLDLOCK` sin `UPDLOCK` (bloqueo compartido) permitiría entradas en paralelo y seguiría bloqueando la desactivación. Queda anotado en `docs/assumptions.md` como mejora si el volumen lo pidiera.
- [Cambiar la collation de `Suppliers.Nit` falla si una base existente tiene dos NIT que solo difieren en mayúsculas] → Hasta este cambio no había endpoint de proveedores, así que solo puede pasar con datos puestos a mano. Es el mismo riesgo que DEC-003.
- [El cambio de comportamiento de `supplierId` en las salidas (de ignorado a 400) rompe a los clientes que lo enviaban] → No hay clientes en producción. Se marca **BREAKING** en la propuesta y se documenta en `docs/api-contract.md`.
- [Un NIT con formatos distintos (`900.123.456-7` y `900123456-7`) se considera distinto] → La propuesta solo pide recortar. Se registra como supuesto.
- [La concurrencia de BR-09, BR-08 y BR-10 no tiene prueba automática] → Se verifica a mano contra SQL Server en Docker, con peticiones simultáneas, y se anota en `docs/bitacora.md`.

## Migration Plan

1. `db/init.sql` aplica la migración idempotente de la collation de `Suppliers.Nit` al arrancar `dbinit`. Las bases nuevas ya la crean con la collation.
2. Se despliega la API con `docker compose up -d --build`. No hay variables nuevas.
3. **Rollback:** volver a la imagen anterior de la API. La collation `CI_AS` es compatible con el código anterior, que no usaba proveedores, y no hace falta revertirla.
