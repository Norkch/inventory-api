# Design

## Context

- El cambio `add-categories` dejó montada la base: `Guard`, las excepciones de dominio, `PageRequest` y `PagedResult<T>`, `IClock`, `SqlConnectionFactory`, `InventoryReadDbContext`, `ExceptionHandlingMiddleware`, `InvalidModelStateResponseFactory` y la prueba de arquitectura `CqrsSeparationTests`. Este diseño reutiliza todo eso sin cambiar su comportamiento.
- `db/init.sql` ya crea `dbo.Products` con `StockQuantity DEFAULT 0 CHECK (>= 0)`, `IsActive`, `UpdatedAtUtc`, `UX_Products_Sku` (sin filtro) e `IX_Products_CategoryId INCLUDE (IsActive)`. `Sku` no declara collation.
- `CategoryWriteRepository.DeactivateAsync` desactiva una categoría con una sola sentencia que comprueba `NOT EXISTS` productos activos. Hasta ahora no había ningún endpoint que creara productos.
- Las categorías no se pueden editar (no hay `PUT`), así que el nombre de una categoría no cambia nunca después de crearla.
- `AddCategories` registra, además de lo de categorías, las piezas compartidas: `IClock`, `SqlConnectionFactory` e `InventoryReadDbContext`.
- `InvalidModelStateResponseFactory` describe cualquier parámetro de consulta inválido como "debe ser un número entero", porque hasta ahora solo existían `page` y `pageSize`.
- No se pueden añadir paquetes. No hay `TestHost` ni proveedor en memoria de EF Core: los repositorios se verifican a mano con Docker.
- Context7 no tiene documentación específica de EF Core 6: devuelve la de la rama principal, con notas de EF Core 9. Según esa tabla, `string.Contains` se traduce a `LIKE N'%' + @value + N'%'` sin documentar el escape de comodines (ver D6).

Motivación en `proposal.md`. Requisitos en `specs/products`.

## Goals / Non-Goals

**Goals:**
- Garantizar BR-03 frente a la concurrencia con la desactivación de categorías, sin transacciones explícitas ni niveles de aislamiento especiales.
- Que el orden de los errores del alta (formato, categoría, SKU) sea determinista y se pruebe con fakes.
- Mantener la separación CQRS: los handlers de comando devuelven el producto sin usar el puerto de lectura.

**Non-Goals:**
- Pruebas automáticas contra SQL Server de los repositorios y de `init.sql`.
- Declarar una collation explícita en `Products.Name`. La búsqueda la fija en la consulta (D6) y el orden usa la de la instancia.
- Concurrencia optimista en el `PUT` (`rowversion`, `If-Match`). Si dos ediciones coinciden, gana la última.
- Modificar `StockQuantity` desde este cambio. Solo se lee.

## Decisions

### D1. Dónde vive cada pieza

```
                Comandos (POST, PUT, DELETE)                    Consultas (GET)
                            |                                          |
   CreateProductHandler, UpdateProductHandler,        GetProductByIdHandler, ListProductsHandler
   DeleteProductHandler                                                |
                            |                                          |
                 IProductWriteRepository                     IProductReadRepository
                            |                                          |
           ProductWriteRepository (Dapper)          ProductReadRepository (EF Core, AsNoTracking)
                            |                                          |
                  SqlConnectionFactory                       InventoryReadDbContext
                            \                                         /
                             +--> dbo.Products, dbo.Categories <-----+
```

```
Domain/          Entities/{Product, ProductChanges}  ProductRules
Application/     Products/  CreateProductCommand + CreateProductHandler   --> IProductWriteRepository
                            UpdateProductCommand + UpdateProductHandler   --> IProductWriteRepository
                            DeleteProductCommand + DeleteProductHandler   --> IProductWriteRepository
                            GetProductByIdQuery  + GetProductByIdHandler  --> IProductReadRepository
                            ListProductsQuery    + ListProductsHandler    --> IProductReadRepository
                            ProductDto, CategoryReference, ProductMessages, ProductSearch
Infrastructure/  Persistence/Write/ProductWriteRepository
                 Persistence/Read/{ProductRecord, ProductReadRepository, LikePattern}
Api/             Controllers/ProductsController
                 Contracts/{CreateProductRequest, UpdateProductRequest, ListProductsRequest}
                 Configuration/{PersistenceServiceCollectionExtensions, ProductsServiceCollectionExtensions}
```

- Una clase, record o enum por archivo. Los handlers reciben el comando o la consulta completos.
- `CqrsSeparationTests` ya cubre los handlers nuevos sin cambios: detecta cualquier `*Handler` por reflexión.
- `CategoryReference` (`Id`, `Name`) es lo que el puerto de escritura devuelve al comprobar la categoría (D3).

### D2. Validación en el dominio
- `ProductRules` (Domain) agrupa las constantes (`SkuMaxLength = 50`, `NameMaxLength = 200`, `DescriptionMaxLength = 1000`) y los métodos estáticos que recortan y validan cada campo con `Guard`, con los mensajes en español. Las dos entidades de abajo usan esos mismos métodos, para no duplicar la regla.
- `Product.Create(...)` valida `sku`, `name`, `description` y `categoryId` (obligatorio: `null` o `Guid.Empty` lanza `ValidationException`). Fija `StockQuantity = 0`, `IsActive = true`, `CreatedAtUtc` desde `IClock` y `UpdatedAtUtc = null`.
- `ProductChanges.Create(...)` representa un `PUT`: `productId`, `name`, `description`, `categoryId` y `UpdatedAtUtc`. No tiene `sku` ni `stockQuantity`, así que BR-01 y BR-07 se cumplen por construcción.
- `Product.Restore(...)` reconstruye un producto desde la fila que devuelve el `UPDATE` (D4), sin validar de nuevo.
- Los contratos de Api declaran `string?` y `Guid?` en todos los campos, por el mismo motivo que `CreateCategoryRequest` (D8 de `add-categories`): la regla vive solo en el dominio. `CreateProductRequest` y `UpdateProductRequest` no tienen `StockQuantity`, y `UpdateProductRequest` no tiene `Sku`. System.Text.Json ignora los campos desconocidos.
- **Alternativa descartada:** una sola entidad `Product` con un método `ApplyChanges`. Obligaría a leer el producto antes de editarlo, y esa lectura en un handler de comando tendría que hacerse con el puerto de escritura de todas formas.

### D3. Alta: orden de los errores y concurrencia con la categoría
`CreateProductHandler`:

```
Product.Create(...)                                  --> ValidationException      (400 de formato)
writeRepo.FindActiveCategoryAsync(product)           --> null: ValidationException (400 de categoría)
writeRepo.ExistsWithSameSkuAsync(product)            --> true: ConflictException   (409)
writeRepo.AddAsync(product)                          --> false: ValidationException (400 de categoría)
                                                         2601/2627: ConflictException (409)
return ProductDto.From(product, categoryReference)
```

- Las comprobaciones previas fijan el orden que pide la spec y dan el mensaje correcto en el caso normal. Las expone el puerto de **escritura**, como en categorías.
- `AddAsync` es una sola sentencia:

  ```sql
  INSERT INTO dbo.Products (Id, CategoryId, Sku, Name, Description, StockQuantity, IsActive, CreatedAtUtc, UpdatedAtUtc)
  SELECT @Id, @CategoryId, @Sku, @Name, @Description, 0, 1, @CreatedAtUtc, NULL
   WHERE EXISTS (SELECT 1 FROM dbo.Categories WITH (UPDLOCK, HOLDLOCK)
                  WHERE Id = @CategoryId AND IsActive = 1);
  ```

  Devuelve `true` si inserta una fila. Si la categoría se desactivó entre la comprobación previa y la sentencia, inserta 0 filas y el handler lanza el mismo 400 de categoría.
- **Por qué `UPDLOCK, HOLDLOCK`:** la sentencia autocommit es su propia transacción, y el bloqueo de actualización sobre la fila de la categoría dura hasta que termina. El `UPDATE` de `DeactivateAsync` necesita ese mismo bloqueo, así que las dos sentencias se serializan:

  ```
  alta primero:   INSERT (U en categoría) -> commit -> UPDATE categoría ve el producto -> 409
  baja primero:   UPDATE categoría (X)    -> commit -> INSERT espera, ve IsActive = 0   -> 0 filas -> 400
  ```

  Sin la pista, el `EXISTS` libera el bloqueo compartido al leer, y la desactivación podría colarse antes del `INSERT`.
- **Unicidad del SKU:** la comprobación previa (`WHERE Sku = @Sku`, sin filtro de `IsActive`) da el 409 en el caso normal, y `UX_Products_Sku` es la defensa final ante altas simultáneas. El error 2601/2627 se traduce a `ConflictException` con el mismo mensaje, como en `CategoryWriteRepository`. El mensaje no dice si el otro producto está borrado.
- **`categoryName` en la respuesta 201:** sale de `CategoryReference`. Es seguro porque el nombre de una categoría no cambia después de crearla. Así el handler de comando no necesita el puerto de lectura.
- **Alternativas descartadas:**
  - Transacción explícita con `SERIALIZABLE`. Hace lo mismo con más código y bloquea rangos más amplios.
  - Comprobar la categoría solo en el handler. Deja la carrera descrita en `proposal.md`.

### D4. Edición
`UpdateProductHandler`:

```
ProductChanges.Create(...)                           --> ValidationException (400 de formato)
writeRepo.ExistsActiveAsync(changes)                 --> false: NotFoundException (404)
writeRepo.FindActiveCategoryAsync(changes)           --> null: ValidationException (400 de categoría)
writeRepo.UpdateAsync(changes)                       --> Product? (null: se diagnostica otra vez, 404 o 400)
return ProductDto.From(product, categoryReference)
```

- `UpdateAsync` es una sola sentencia, con la misma pista de bloqueo que el alta:

  ```sql
  UPDATE dbo.Products
     SET Name = @Name, Description = @Description, CategoryId = @CategoryId, UpdatedAtUtc = @UpdatedAtUtc
  OUTPUT INSERTED.Id, INSERTED.CategoryId, INSERTED.Sku, INSERTED.Name, INSERTED.Description,
         INSERTED.StockQuantity, INSERTED.IsActive, INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc
   WHERE Id = @Id AND IsActive = 1
     AND EXISTS (SELECT 1 FROM dbo.Categories WITH (UPDLOCK, HOLDLOCK)
                  WHERE Id = @CategoryId AND IsActive = 1);
  ```

  La sentencia nunca toca `Sku` ni `StockQuantity`. `OUTPUT` devuelve el producto completo (`Product.Restore`) para la respuesta 200 sin una segunda lectura.
- Si la sentencia no devuelve ninguna fila, el handler repite `ExistsActiveAsync` y elige 404 o 400. Si el producto se borró entre medias, la respuesta es 404, que también es correcta.
- **Orden 404 antes que la categoría:** la spec no lo fija para el `PUT`. Se elige el 404 primero porque el recurso es lo primero que identifica la URL. Se registra en `docs/assumptions.md`.
- **Mover un producto de categoría:** el `UPDATE` bloquea la categoría de destino. La de origen no necesita bloqueo: quitarle un producto activo nunca rompe BR-05.

### D5. Borrado lógico
`DeleteAsync` ejecuta `UPDATE dbo.Products SET IsActive = 0, UpdatedAtUtc = @UpdatedAtUtc WHERE Id = @Id AND IsActive = 1`. Si afecta a una fila responde 204, y si no, `NotFoundException`.
- No toca `StockQuantity` ni `InventoryMovements` (BR-06). El stock queda congelado. Que no se puedan registrar movimientos sobre un producto inactivo es BR-11, y se implementa en el cambio de movimientos.
- `UpdatedAtUtc` registra el momento del borrado. Se registra en `docs/assumptions.md`.
- No necesita bloquear la categoría: borrar un producto solo puede liberar la categoría.

### D6. Lecturas con EF Core, filtro y búsqueda
- `ProductRecord` se mapea a `dbo.Products` en `InventoryReadDbContext`, sin navegación (no hay FK). `ProductReadRepository` hace un `join` en LINQ entre `Products` y `Categories` y proyecta a `ProductDto` dentro de la consulta. No filtra por `IsActive` de la categoría: D3 y D4 garantizan que un producto activo siempre está en una categoría activa.
- **Listado:** `IsActive`, después `categoryId` si viene, y después `search` si viene. Ordena por `Name` y luego `Id`, ejecuta `CountAsync` y después `Skip`/`Take`, como en categorías.
- **`ProductSearch` (Application):** recorta `search` y lo convierte en `null` si queda vacío. `ListProductsQuery` lo construye antes de llamar al puerto. Se prueba sin base de datos.
- **Búsqueda literal:** no se usa `string.Contains`, porque la documentación disponible no confirma cómo escapa EF Core 6 los comodines en SQL Server. En su lugar, `LikePattern.ForContains(text)` (Infrastructure) escapa `\`, `%`, `_` y `[` con `\` y envuelve el texto en `%...%`. La consulta usa `EF.Functions.Like(column, pattern, "\\")`, que se traduce a `LIKE @pattern ESCAPE N'\'`. El escape se prueba con xUnit.
- **Sin distinguir mayúsculas:** `Sku` tiene collation CI explícita (D7). Para `Name`, el predicado usa `EF.Functions.Collate(record.Name, "SQL_Latin1_General_CP1_CI_AS")`, así la búsqueda no depende de la collation de la instancia aunque la columna no la declare. No hay índice sobre `Name`, así que esto no empeora el plan.
- Antes de implementar, se confirma en Context7 (o en el XML de la API del paquete 6.0.36) que `EF.Functions.Like` con `escapeCharacter` y `EF.Functions.Collate` existen en EF Core 6, y se anota en `docs/assumptions.md`.
- **Alternativa descartada:** `ToLower()` en las dos partes. Daría el mismo resultado, pero tampoco resuelve los comodines.

### D7. `init.sql`: collation explícita en `Products.Sku`
En el mismo orden que D11 de `add-categories`:
1. `CREATE TABLE dbo.Products` declara `Sku NVARCHAR(50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL`, para los clones limpios.
2. Si la collation de `Sku` en `sys.columns` no es `SQL_Latin1_General_CP1_CI_AS` y existe `UX_Products_Sku`, se elimina el índice.
3. Si la collation no es la esperada, `ALTER COLUMN Sku NVARCHAR(50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL`.
4. El bloque final ya existente crea `UX_Products_Sku` si no existe, **sin filtro**.

Los pasos 2 y 3 van antes del bloque de índices. Ejecutarlo dos veces no cambia nada.

### D8. Mensaje de parámetro inválido según su tipo
`InvalidModelStateResponseFactory` busca el tipo del parámetro inválido entre las propiedades de los parámetros de la acción (`context.ActionDescriptor.Parameters`, sin distinguir mayúsculas en el nombre):
- `int`/`int?`: "El parámetro '{nombre}' debe ser un número entero." (el texto actual, que no cambia para `page` y `pageSize`).
- `Guid`/`Guid?`: "El parámetro '{nombre}' debe ser un identificador (GUID) válido."
- Otro tipo o no encontrado: "El parámetro '{nombre}' no tiene un formato válido."

Un `categoryId` inválido en el **cuerpo** sigue dando "El cuerpo de la petición no es un JSON válido.", porque su clave empieza por `$`.
- **Alternativa descartada:** declarar `categoryId` como `string?` y convertirlo en el handler. Mueve la conversión de tipos fuera de Api y obligaría a repetirla en cada filtro.

### D9. Registro de dependencias
`IClock`, `SqlConnectionFactory` y `AddDbContext<InventoryReadDbContext>` salen de `AddCategories` y pasan a `AddPersistence` (Api/Configuration). `Program.cs` llama a `AddPersistence()`, `AddCategories()` y `AddProducts()`. El comportamiento de categorías no cambia.

### D10. Rutas y respuestas
- `api/products` y `api/products/{id:guid}`, igual que en categorías: un id que no es un GUID responde 404 con el cuerpo vacío.
- `POST` devuelve 201 con `Location`, `PUT` devuelve 200 con el producto y `DELETE` devuelve 204.
- `createdAtUtc` en la respuesta 201 lleva el sufijo `Z`, y las lecturas no, por el mismo motivo documentado para categorías. `updatedAtUtc` de la respuesta 200 del `PUT` sale de `OUTPUT` y se serializa sin sufijo, como una lectura. Se anota en `docs/assumptions.md`.

### D11. Estrategia de pruebas
- **Pruebas unitarias con fakes:**
  - `ProductRules`, `Product.Create` y `ProductChanges.Create`
  - `ProductSearch` y `LikePattern`
  - los cinco handlers, con `FakeProductWriteRepository`, `FakeProductReadRepository` y `FakeClock`, incluidos el orden de los errores y las respuestas `false`/`null` de las sentencias atómicas
  - `ProductsController` (códigos, `Location`, políticas por acción)
  - `InvalidModelStateResponseFactory` con un parámetro `Guid?`
- **Verificación manual con Docker y `curl`:** los repositorios, `init.sql` sobre un volumen nuevo y uno existente, las altas simultáneas con el mismo SKU, el alta simultánea con la desactivación de la categoría y la búsqueda con `%`. Se anota en `docs/bitacora.md`.
- Los nombres de las pruebas citan la regla (`BR01_...`, `BR02_...`, `BR03_...`, `BR06_...`, `BR07_...`, `BR18_...`).

## Risks / Trade-offs

- [La carrera de D3 no se puede reproducir de forma fiable con dos `curl`] → La garantía se razona en D3 y se comprueba a mano abriendo una transacción con `sqlcmd` que bloquea la categoría (`BEGIN TRAN; UPDATE ... IsActive = 0`) mientras se lanza el `POST`, y comprobando que el alta espera y responde 400.
- [`ALTER COLUMN` de `Sku` en un volumen con dos SKU que solo difieren en mayúsculas] → Hasta ahora no había endpoint de productos, así que solo puede pasar con datos insertados a mano. Si ocurre, `dbinit` falla con el error de SQL Server y la solución es recrear el volumen (`docker compose down -v`).
- [`EF.Functions.Collate` sobre `Name` impide usar un índice] → No hay índice sobre `Name`, y el listado ya pagina sobre un filtro por `IsActive`.
- [El orden por `Name` usa la collation de la instancia] → La imagen de SQL Server del proyecto usa `SQL_Latin1_General_CP1_CI_AS` por defecto. Declararla en la columna queda fuera de este cambio.
- [Un `PUT` sin `description` la borra] → Es la semántica de reemplazo completo del `PUT` y está en la spec ("Edición correcta"). Se documenta en `docs/api-contract.md`.
- [Dos `PUT` simultáneos sobre el mismo producto] → Gana el último. Es aceptable sin un requisito de concurrencia optimista.

## Migration Plan

1. `docker compose up -d --build`: `dbinit` aplica D7 sobre el volumen existente o sobre uno nuevo.
2. No hay cambios que rompan clientes: los endpoints son nuevos y el mensaje de `page`/`pageSize` no cambia.
3. Rollback: volver a la imagen anterior. La collation de `Sku` y las filas de `Products` no afectan a esa versión.
