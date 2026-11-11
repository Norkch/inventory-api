# Tasks

## 1. Documentación de las APIs que se van a usar

- [x] 1.1 Consultar Context7 con la versión 6 de cada librería sobre:
  - en Dapper 2.1, `QuerySingleOrDefaultAsync<T>` con `UPDATE ... OUTPUT INSERTED.*` (D6) y con `INSERT ... SELECT ... WHERE EXISTS` seguido de la lectura de los nombres (D4);
  - en EF Core 6, `Join` entre tres `DbSet` con `OrderBy(...).ThenBy(...)`, `Skip`/`Take` y `CountAsync` (D7);
  - en .NET 6, `Regex` con `matchTimeout` y `RegexOptions.CultureInvariant` (D2);
  - en ASP.NET Core 6, una ruta absoluta en una acción (`[HttpGet("/api/suppliers/{supplierId:guid}/products")]`) y `CreatedAtAction` con dos valores de ruta (D8).

  Si Context7 devuelve documentación de otra versión mayor, avisar y confirmar las firmas en el XML de documentación del paquete 6.x. Anotar lo confirmado y su origen en `docs/assumptions.md`. Verificar: la entrada existe en `docs/assumptions.md`.

## 2. Base de datos

- [x] 2.1 En `db/init.sql`, declarar `Suppliers.Nit NVARCHAR(30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL` en el `CREATE TABLE` y añadir la migración idempotente para bases existentes, copiando la de `Products.Sku` (D3):
  - si la collation es distinta, borrar `UX_Suppliers_Nit`;
  - hacer `ALTER COLUMN`;
  - dejar que el bloque de índices lo vuelva a crear.

  Actualizar `docs/data-model.md` (columna `Nit`, principio de collation e índice `UX_Suppliers_Nit` sin filtro, también para los desactivados). Verificar: `docker compose up -d --build` dos veces seguidas termina `dbinit` sin errores, y `sys.columns` muestra la collation en `Suppliers.Nit`.

## 3. Dominio de proveedores (BR-08)

- [x] 3.1 TDD de `SupplierRules` en `src/Inventory.Domain/` (D2). Pruebas `BR08_...` en `tests/Inventory.UnitTests/Domain/SupplierRulesTests.cs`:
  - `ValidateName`: recorta; vacío o solo espacios falla; 200 pasa y 201 falla;
  - `ValidateNit`: recorta; vacío falla; 30 pasa y 31 falla;
  - `ValidateEmail`: `null` o solo espacios da `null`; recorta y conserva mayúsculas; `juan@mail.com` pasa; `juan.mail.com`, `juan@mail`, `juan perez@mail.com` y `juan@@mail.com` fallan; 201 caracteres falla con el mensaje de longitud;
  - `ValidatePhone`: `null` o solo espacios da `null`; `+57 300-123-4567` y `123-4567` pasan; `123-456`, `1234567890123456`, `300 ABC 4567` y `300+1234567` fallan; 31 caracteres falla con el mensaje de longitud.

  Todos los mensajes van en español. Verificar: `dotnet build` sin advertencias y `dotnet test` en verde.
- [x] 3.2 TDD de `Supplier.Create(SupplierDraft)` y `Supplier.ApplyChanges(SupplierChanges)` (o equivalente inmutable) en `src/Inventory.Domain/Entities/`. Pruebas en `tests/Inventory.UnitTests/Domain/SupplierTests.cs`:
  - el alta valida en el orden nombre, NIT, correo y teléfono, y fija `Id`, `IsActive` y `CreatedAtUtc`;
  - los cambios validan nombre, correo y teléfono, y no tocan `Nit` ni `CreatedAtUtc`.

  Verificar: `dotnet test` en verde.

## 4. Casos de uso de proveedores

- [x] 4.1 Crear en `src/Inventory.Application/Suppliers/`:
  - `ISupplierWriteRepository` (`ExistsWithSameNitAsync`, `CreateAsync`, `UpdateAsync` que devuelve `Supplier?` y `DeactivateAsync` que devuelve `bool`);
  - `ISupplierReadRepository` (`FindActiveAsync`, `ListAsync(SupplierListCriteria, ...)`);
  - `SupplierDto.From(...)`;
  - `SupplierMessages` (NIT repetido y proveedor activo no encontrado).

  Crear `FakeSupplierWriteRepository` y `FakeSupplierReadRepository` en `tests/Inventory.UnitTests/Fakes/`. Verificar: `dotnet build` sin advertencias.
- [x] 4.2 TDD de `CreateSupplierHandler`, que solo usa el puerto de escritura y `IClock` (D3). Pruebas `BR08_...` en `tests/Inventory.UnitTests/Suppliers/CreateSupplierHandlerTests.cs`:
  - alta correcta con la hora de `IClock`;
  - un cuerpo inválido lanza `ValidationException` sin consultar el NIT;
  - un NIT existente lanza `ConflictException` sin insertar;
  - el `ConflictException` del repositorio (carrera) se propaga.

  Verificar: `dotnet test` en verde.
- [x] 4.3 TDD de `UpdateSupplierHandler` y `DeactivateSupplierHandler` (D6). Pruebas en `tests/Inventory.UnitTests/Suppliers/`:
  - la edición correcta fija `UpdatedAtUtc` desde `IClock`;
  - la edición con un cuerpo inválido no llama al puerto;
  - `null` del puerto lanza `NotFoundException`;
  - la desactivación con `false` lanza `NotFoundException` y con `true` no devuelve nada.

  Verificar: `dotnet test` en verde.
- [x] 4.4 TDD de `GetSupplierByIdHandler` y `ListSuppliersHandler`, que solo usan el puerto de lectura. Pruebas `BR18_...`:
  - un proveedor no activo lanza `NotFoundException`;
  - una paginación inválida lanza `ValidationException` sin consultar;
  - el listado devuelve el `PagedResult<SupplierDto>` del puerto.

  Verificar: `dotnet test` en verde y `CqrsSeparationTests` sigue en verde.

## 5. Persistencia de proveedores

- [x] 5.1 Implementar `SupplierWriteRepository` con Dapper (D3, D6). Incluye:
  - `ExistsWithSameNitAsync`;
  - el `INSERT` con la traducción de 2601/2627 a `ConflictException`;
  - el `UPDATE ... OUTPUT` condicional a `IsActive = 1`, sin `Nit` en el `SET`;
  - el `UPDATE` de desactivación con `UpdatedAtUtc`.

  Los parámetros llevan tipos y longitudes tomados de `SupplierRules`. Verificar: `dotnet build` sin advertencias. La verificación con la base se hace en la tarea 11.1.
- [x] 5.2 Implementar `SupplierRecord`, su mapeo en `InventoryReadDbContext` y `SupplierReadRepository` con EF Core y `AsNoTracking()` (D7). El listado filtra los activos y ordena por `Name` y luego `Id`. Verificar: `dotnet build` sin advertencias.

## 6. Endpoints de proveedores

- [x] 6.1 TDD de `SuppliersController` y de los contratos `CreateSupplierRequest` (`Name`, `Nit`, `Email`, `Phone`) y `UpdateSupplierRequest` (`Name`, `Email`, `Phone`, sin `Nit`) (D8):
  - `[Authorize]` y las políticas `InventoryWrite` e `InventoryRead` por acción;
  - el `POST` responde 201 con `Location` hacia `/api/suppliers/{id}`;
  - el `PUT` responde 200 y el `DELETE` 204;
  - las rutas usan `{id:guid}`.

  Pruebas en `tests/Inventory.UnitTests/Controllers/SuppliersControllerTests.cs` con handlers reales sobre los fakes. Verificar: `dotnet test` en verde.
- [x] 6.2 Crear `AddSuppliers` con los puertos, sus implementaciones y los handlers, y llamarla desde `Program.cs`. Añadir `ProducesResponseType` para 201, 200, 204, 400, 401, 403, 404 y 409 donde corresponda. Documentar en `docs/api-contract.md` la sección de proveedores:
  - endpoints y scopes, cuerpo y representación;
  - reglas de `name`, `nit`, `email` y `phone`;
  - NIT reservado también para los desactivados;
  - `nit` ignorado en el `PUT`;
  - `PUT` que reemplaza (`null` si falta), desactivación sin reactivación y orden de los errores.

  Actualizar `docs/architecture.md` (casos de uso de proveedores y `AddSuppliers`). Verificar: `dotnet build` y `dotnet test` en verde, y Swagger UI muestra los cinco endpoints tras `docker compose up -d --build`.

## 7. Dominio y casos de uso de asociaciones (BR-09, BR-10)

- [x] 7.1 TDD de `ProductSupplierRules.ValidateSupplierSku` y de `ProductSupplier.Create(ProductSupplierDraft)` en `src/Inventory.Domain/`:
  - `supplierId` ausente falla con el mensaje de proveedor obligatorio;
  - `supplierSku` se recorta, solo espacios da `null`, 50 pasa y 51 falla.

  Pruebas en `tests/Inventory.UnitTests/Domain/ProductSupplierTests.cs`. Verificar: `dotnet test` en verde.
- [x] 7.2 Crear en `src/Inventory.Application/ProductSuppliers/`:
  - `ProductSupplierLinkStatus`;
  - `ProductSupplierLinkResult`, con el DTO cuando el estado es `Linked`;
  - `IProductSupplierWriteRepository.LinkAsync`;
  - `IProductSupplierReadRepository` (`FindVisibleAsync`, `ExistsActiveProductAsync`, `ExistsActiveSupplierAsync`, `ListByProductAsync` y `ListBySupplierAsync`, con sus criterios);
  - `ProductSupplierDto`;
  - `ProductSupplierMessages` (proveedor no disponible, pareja repetida y pareja no asociada).

  Crear sus fakes en `tests/Inventory.UnitTests/Fakes/`. Verificar: `dotnet build` sin advertencias.
- [x] 7.3 TDD de `LinkProductSupplierHandler`, que solo usa el puerto de escritura y `IClock` (D4). Pruebas `BR09_...` y `BR10_...` en `tests/Inventory.UnitTests/ProductSuppliers/LinkProductSupplierHandlerTests.cs`:
  - un cuerpo inválido lanza `ValidationException` sin llamar al puerto;
  - `ProductNotFound` lanza `NotFoundException`;
  - `SupplierNotAvailable` lanza `ValidationException`;
  - `AlreadyLinked` lanza `ConflictException`;
  - `Linked` devuelve el DTO con la hora de `IClock`.

  Verificar: `dotnet test` en verde.
- [x] 7.4 TDD de `GetProductSupplierHandler`, `ListProductSuppliersHandler` y `ListSupplierProductsHandler`, que solo usan el puerto de lectura (D7). Pruebas `BR18_...`:
  - la pareja no visible lanza `NotFoundException`;
  - una paginación inválida lanza `ValidationException` antes de comprobar la entidad de la ruta;
  - un producto o proveedor no activo lanza `NotFoundException` sin listar;
  - con la entidad activa, devuelve el `PagedResult<ProductSupplierDto>` del puerto, también vacío.

  Verificar: `dotnet test` en verde y `CqrsSeparationTests` sigue en verde.

## 8. Persistencia y endpoints de asociaciones

- [x] 8.1 Implementar `ProductSupplierWriteRepository` con Dapper (D4):
  - el `INSERT ... SELECT ... WHERE EXISTS` con `UPDLOCK, HOLDLOCK` sobre `Suppliers`;
  - la lectura de `productName` y `supplierName` para la respuesta;
  - el diagnóstico de 0 filas (producto y después proveedor);
  - la traducción de 2601/2627 a `AlreadyLinked`.

  Verificar: `dotnet build` sin advertencias. La verificación con la base se hace en la tarea 11.1.
- [x] 8.2 Implementar `ProductSupplierRecord`, su mapeo en `InventoryReadDbContext` y `ProductSupplierReadRepository` con EF Core y `AsNoTracking()` (D7):
  - uniones con `Products` y `Suppliers` activos;
  - orden por nombre del proveedor y `SupplierId` en un sentido, y por nombre del producto y `ProductId` en el otro.

  Verificar: `dotnet build` sin advertencias.
- [x] 8.3 TDD de `ProductSuppliersController` y del contrato `LinkProductSupplierRequest` (`Guid? SupplierId`, `string? SupplierSku`) (D8):
  - el `POST` responde 201 con `Location` hacia `/api/products/{productId}/suppliers/{supplierId}`;
  - los tres `GET` responden 200, incluida la ruta absoluta `/api/suppliers/{supplierId:guid}/products`;
  - no hay acciones `PUT`, `PATCH` ni `DELETE`.

  Pruebas en `tests/Inventory.UnitTests/Controllers/ProductSuppliersControllerTests.cs`. Crear `AddProductSuppliers` y llamarla desde `Program.cs`. Verificar: `dotnet test` en verde y Swagger UI muestra los cuatro endpoints.
- [x] 8.4 Documentar en `docs/api-contract.md` la sección de asociaciones:
  - endpoints y scopes, cuerpo y representación con `productName` y `supplierName`;
  - orden de errores 400 → 404 → 400 → 409;
  - visibilidad solo con producto y proveedor activos;
  - 405 para `PUT` y `DELETE`;
  - el resultado de un alta simultánea con la desactivación del proveedor.

  Actualizar `docs/architecture.md` (casos de uso de asociaciones y quitar `UnlinkProductSupplier`). Verificar: los documentos coinciden con `specs/product-suppliers` y `design.md`.

## 9. Proveedor en las entradas (BR-16)

- [x] 9.1 TDD de `MovementRules.ValidateSupplier` y de `SupplierId` en `MovementDraft` e `InventoryMovement` (D2). Pruebas `BR16_...` en `tests/Inventory.UnitTests/Domain/`:
  - una salida con `supplierId` lanza `ValidationException`;
  - una salida con `null` y una entrada con o sin `supplierId` pasan;
  - la validación del proveedor va después de la del tipo y la cantidad.

  Verificar: `dotnet test` en verde.
- [x] 9.2 TDD de los cambios en `RegisterMovementHandler`, `MovementRegistrationStatus.SupplierNotAvailable`, `MovementMessages.SupplierNotAvailable` y `MovementDto.SupplierId` (D5). Pruebas en `tests/Inventory.UnitTests/Movements/RegisterMovementHandlerTests.cs`:
  - `SupplierNotAvailable` lanza `ValidationException`;
  - una salida con `supplierId` falla sin llamar al puerto;
  - el movimiento registrado devuelve `supplierId`.

  Actualizar `FakeMovementWriteRepository`. Verificar: `dotnet test` en verde.
- [x] 9.3 Modificar `MovementWriteRepository` (D5):
  - la comprobación del proveedor activo y asociado con `UPDLOCK, HOLDLOCK` dentro de la transacción y antes del `UPDATE`, solo si hay `SupplierId`;
  - el diagnóstico del producto cuando falla;
  - el `INSERT` con `@SupplierId`.

  Añadir `SupplierId` a `MovementRecord`, a su mapeo y a `MovementReadRepository`. Añadir `SupplierId` a `RegisterMovementRequest` y ampliar `MovementsControllerTests`. Verificar: `dotnet build` sin advertencias y `dotnet test` en verde.
- [x] 9.4 Actualizar la documentación:
  - `docs/api-contract.md`: `supplierId` en el cuerpo y en la representación, salida con proveedor (400, **BREAKING**), nuevo orden de errores; quitar `supplierId` de los campos ignorados;
  - `docs/data-model.md`: `SupplierId` deja de estar siempre `NULL`;
  - `docs/business-rules.md`: BR-16 deja de estar "Aplazada";
  - `docs/assumptions.md`: el `UPDLOCK` serializa las entradas con el mismo proveedor (riesgo de D5) y el NIT solo se recorta, sin normalizar.

  Verificar: los documentos coinciden con `specs/inventory-movements` y `design.md`.

## 10. Revisión de Clean Code

- [x] 10.1 Ejecutar la revisión de Clean Code (skill `clean-code-review` o subagente `code-reviewer`, en una sesión distinta de la que implementó) sobre el diff de la rama, y corregir o registrar en `docs/assumptions.md` cada hallazgo. Verificar: `dotnet build` y `dotnet test` en verde después de las correcciones.

## 11. Verificación de extremo a extremo

- [x] 11.1 Con `docker compose up -d --build` y tokens de Keycloak obtenidos con `curl`, recorrer cada escenario de `specs/suppliers`, `specs/product-suppliers` y `specs/inventory-movements`, incluidos:
  - NIT repetido con otras mayúsculas y NIT de un proveedor desactivado (409), y dos altas simultáneas con el mismo NIT (201 y 409);
  - `PUT` con `nit`, `PUT` sin `email` ni `phone`, `DELETE` y la invisibilidad posterior (404 en `GET`, `PUT` y `DELETE`);
  - el alta de asociación en el orden 400 → 404 → 400 → 409, dos altas simultáneas de la misma pareja, y `PUT` y `DELETE` con respuesta 405;
  - los listados en los dos sentidos, que excluyen los inactivos, y el 404 por producto o proveedor no activo en la ruta;
  - una entrada con un proveedor asociado, no asociado o desactivado; una salida con `supplierId` (400); `supplierId` en el historial;
  - BR-09 con concurrencia: varias rondas de desactivación del proveedor a la vez que una entrada y que un alta de asociación, comprobando con `sqlcmd` que no hay ninguna fila creada después de la desactivación.

  Verificar: cada escenario queda anotado en `docs/bitacora.md` con su comando y su resultado, todos dan el resultado esperado y la cobertura de Coverlet queda anotada.

## Workflow follow-up

- Actualizar `docs/ai-usage.md` con el uso de IA en este entregable.
- Abrir el pull request de `feat/add-suppliers` hacia `main`.
- Archivar el cambio con `/opsx:archive` cuando el PR esté aprobado y sincronizar las specs `suppliers`, `product-suppliers` e `inventory-movements`.
