# Tasks

## 1. Documentación de las APIs que se van a usar

- [x] 1.1 Consultar Context7 con EF Core 6 sobre `EF.Functions.Like(matchExpression, pattern, escapeCharacter)`, su traducción a `LIKE ... ESCAPE` en SQL Server y `EF.Functions.Collate`. Si Context7 solo devuelve documentación de otra versión mayor (ya pasó al escribir el diseño), avisar y confirmar las firmas en el XML de documentación del paquete `Microsoft.EntityFrameworkCore` 6.0.36. Consultar también Dapper 2.1 sobre `QuerySingleOrDefaultAsync` con una sentencia `UPDATE ... OUTPUT`. Anotar en `docs/assumptions.md` lo que se confirmó y de dónde salió (D6). Verificar: la entrada existe en `docs/assumptions.md`.

## 2. Dominio de productos (BR-01, BR-02, BR-03, BR-07)

- [x] 2.1 TDD de `ProductRules` en `src/Inventory.Domain/ProductRules.cs` (D2): recorte y validación de `sku` (obligatorio, 50 pasa y 51 falla), `name` (obligatorio, 200 pasa y 201 falla), `description` (solo espacios a `null`, 1000 pasa y 1001 falla) y `categoryId` (`null` y `Guid.Empty` fallan), con `ValidationException` y mensajes en español. Pruebas `BR01_...`, `BR02_...` y `BR03_...` en `tests/Inventory.UnitTests/Domain/ProductRulesTests.cs`. Verificar: `dotnet build` sin advertencias y `dotnet test` en verde.
- [x] 2.2 TDD de `Product.Create` y `Product.Restore` en `src/Inventory.Domain/Entities/Product.cs` (D2): `Create` usa `ProductRules`, fija `StockQuantity = 0` (BR-07), `IsActive = true`, `CreatedAtUtc` recibido y `UpdatedAtUtc = null`; `Restore` conserva todos los valores que recibe. Pruebas en `tests/Inventory.UnitTests/Domain/ProductTests.cs`. Verificar: `dotnet test` en verde.
- [x] 2.3 TDD de `ProductChanges.Create` en `src/Inventory.Domain/Entities/ProductChanges.cs` (D2): valida `name`, `description` y `categoryId` con `ProductRules`, guarda `ProductId` y `UpdatedAtUtc`, y no tiene `Sku` ni `StockQuantity`. Pruebas en `tests/Inventory.UnitTests/Domain/ProductChangesTests.cs`. Verificar: `dotnet test` en verde.

## 3. Puertos, DTO y búsqueda (Application)

- [x] 3.1 Crear en `src/Inventory.Application/Products/` los puertos `IProductWriteRepository` (`FindActiveCategoryAsync`, `ExistsWithSameSkuAsync`, `AddAsync`, `ExistsActiveAsync`, `UpdateAsync`, `DeleteAsync`, según D3, D4 y D5) e `IProductReadRepository` (`FindActiveByIdAsync`, `ListActiveAsync`), el record `CategoryReference`, `ProductDto` (con `From(Product, CategoryReference)`) y `ProductMessages` (mensajes en español de SKU repetido, categoría inexistente o inactiva y producto no encontrado). Crear `FakeProductWriteRepository` y `FakeProductReadRepository` en `tests/Inventory.UnitTests/Fakes/`. Verificar: `dotnet build` sin advertencias.
- [x] 3.2 TDD de `ProductSearch` en `src/Inventory.Application/Products/ProductSearch.cs` (D6): recorta el texto; `null`, `""` y `"   "` dan `null`; `" torn "` da `"torn"`. Pruebas en `tests/Inventory.UnitTests/Products/ProductSearchTests.cs`. Verificar: `dotnet test` en verde.

## 4. Casos de uso de productos

- [x] 4.1 TDD de `CreateProductCommand` y `CreateProductHandler` (solo el puerto de escritura, D3). Pruebas `BR01_...`, `BR03_...` y `BR07_...` en `tests/Inventory.UnitTests/Products/CreateProductHandlerTests.cs`:
  - alta correcta con la fecha de `IClock`, `stockQuantity` 0 y `categoryName` de `CategoryReference`;
  - categoría no encontrada: `ValidationException` con el mensaje de la categoría;
  - SKU repetido: `ConflictException`;
  - `AddAsync` devuelve `false`: `ValidationException` con el mensaje de la categoría;
  - el orden de los errores: con nombre vacío no se consulta la categoría, y con la categoría no encontrada no se comprueba el SKU.

  Verificar: `dotnet test` en verde.
- [x] 4.2 TDD de `UpdateProductCommand` y `UpdateProductHandler` (solo el puerto de escritura, D4). Pruebas en `tests/Inventory.UnitTests/Products/UpdateProductHandlerTests.cs`:
  - edición correcta con `UpdatedAtUtc` de `IClock`, devolviendo el producto de `UpdateAsync` con su `sku` y `stockQuantity` originales;
  - producto inactivo o inexistente: `NotFoundException`, sin consultar la categoría;
  - categoría no encontrada: `ValidationException`;
  - `UpdateAsync` devuelve `null`: se repite la comprobación y se lanza 404 o 400 según el caso;
  - un nombre vacío lanza `ValidationException` sin llamar al puerto.

  Verificar: `dotnet test` en verde.
- [x] 4.3 TDD de `DeleteProductCommand` y `DeleteProductHandler` (solo el puerto de escritura, D5): si `DeleteAsync` devuelve `true` termina sin error y pasa la fecha de `IClock`; si devuelve `false` lanza `NotFoundException` con el mensaje en español. Pruebas `BR06_...` en `tests/Inventory.UnitTests/Products/DeleteProductHandlerTests.cs`. Verificar: `dotnet test` en verde.
- [x] 4.4 TDD de `GetProductByIdQuery` y `GetProductByIdHandler` (solo el puerto de lectura): devuelve el DTO, o lanza `NotFoundException` si no hay ningún producto activo con ese id. Pruebas en `tests/Inventory.UnitTests/Products/GetProductByIdHandlerTests.cs`. Verificar: `dotnet test` en verde.
- [x] 4.5 TDD de `ListProductsQuery` y `ListProductsHandler` (solo el puerto de lectura): construye `PageRequest` y `ProductSearch`, pasa `categoryId` y devuelve el `PagedResult<ProductDto>` del puerto; los parámetros de paginación inválidos lanzan `ValidationException` sin llamar al puerto. Pruebas `BR18_...` en `tests/Inventory.UnitTests/Products/ListProductsHandlerTests.cs`. Verificar: `dotnet test` en verde y `CqrsSeparationTests` sigue en verde con los handlers nuevos.

## 5. Base de datos

- [x] 5.1 Actualizar `db/init.sql` según D7: collation `SQL_Latin1_General_CP1_CI_AS` en `Sku` en el `CREATE TABLE`, y antes del bloque de índices la eliminación protegida de `UX_Products_Sku` y el `ALTER COLUMN` protegido. El índice se recrea sin filtro. Actualizar `docs/data-model.md` (collation de `Sku`, que el SKU es único entre todos los productos aunque estén borrados, y `UpdatedAtUtc` en edición y borrado). Verificar con Docker, y anotar en `docs/bitacora.md`:
  - con un volumen nuevo, `dbinit` termina con código 0;
  - con el volumen anterior a este cambio, `dbinit` también termina con código 0;
  - una segunda ejecución de `init.sql` no cambia nada;
  - `sys.columns` muestra la collation de `Sku` y `sys.indexes` muestra `UX_Products_Sku` con `has_filter = 0`.

## 6. Persistencia (Infrastructure)

- [x] 6.1 Implementar `ProductWriteRepository` con Dapper (D3, D4 y D5), sin EF Core: `FindActiveCategoryAsync`, `ExistsWithSameSkuAsync` (sin filtro de `IsActive`), `AddAsync` con `INSERT ... SELECT ... WHERE EXISTS` y `WITH (UPDLOCK, HOLDLOCK)`, traduciendo 2601/2627 en `ConflictException`, `ExistsActiveAsync`, `UpdateAsync` con `OUTPUT INSERTED.*` y la misma pista de bloqueo, y `DeleteAsync`. Parámetros `nvarchar` con la longitud de `ProductRules`. Verificar: `dotnet build` sin advertencias; la verificación con la base se hace en la tarea 9.1.
- [x] 6.2 TDD de `LikePattern.ForContains` en `src/Inventory.Infrastructure/Persistence/Read/LikePattern.cs` (D6): `"50%"` da `"%50\%%"`, y `_`, `[` y `\` también se escapan con `\`; un texto sin comodines solo se envuelve en `%`. Pruebas en `tests/Inventory.UnitTests/Persistence/LikePatternTests.cs`. Verificar: `dotnet test` en verde.
- [x] 6.3 Implementar `ProductRecord`, el mapeo de `Products` en `InventoryReadDbContext` y `ProductReadRepository` con EF Core (D6), sin `SaveChanges` ni Dapper: `join` con `Categories`, `FindActiveByIdAsync` y `ListActiveAsync` con los filtros `categoryId` y `search` (`EF.Functions.Like` con `LikePattern` y `"\\"`, y `EF.Functions.Collate` sobre `Name`), orden por `Name` y `Id`, `CountAsync`, `Skip`/`Take` y `AsNoTracking()` explícito. Verificar: `dotnet build` sin advertencias; la verificación con la base se hace en la tarea 9.1.

## 7. Errores de la API

- [x] 7.1 TDD del mensaje por tipo en `InvalidModelStateResponseFactory` (D8), ampliando `tests/Inventory.UnitTests/Configuration/InvalidModelStateResponseFactoryTests.cs`: un parámetro `Guid?` (`categoryId`) produce "El parámetro 'categoryId' debe ser un identificador (GUID) válido.", `pageSize` (`int?`) mantiene su mensaje actual, un tipo desconocido produce el mensaje genérico, y una clave `$.categoryId` sigue dando el mensaje de JSON inválido. Verificar: `dotnet test` en verde, incluidas las pruebas que ya existían.

## 8. Endpoints de productos

- [x] 8.1 Mover `IClock`, `SqlConnectionFactory` y `InventoryReadDbContext` de `AddCategories` a una nueva extensión `AddPersistence` (D9) y llamarla desde `Program.cs`. Verificar: `dotnet build` y `dotnet test` en verde, y `docker compose up -d --build` sigue sirviendo `GET /api/categories`.
- [x] 8.2 TDD de `ProductsController` (`[Authorize]`, ruta `api/products`, `{id:guid}`, D10) con los contratos `CreateProductRequest` (`string? Sku`, `string? Name`, `string? Description`, `Guid? CategoryId`), `UpdateProductRequest` (sin `Sku` ni `StockQuantity`) y `ListProductsRequest` (`int? Page`, `int? PageSize`, `Guid? CategoryId`, `string? Search`). Las pruebas, en `tests/Inventory.UnitTests/Controllers/ProductsControllerTests.cs`, usan handlers reales sobre los fakes y comprueban:
  - POST: 201 con el cuerpo y `Location` igual a `/api/products/{id}`;
  - PUT: 200 con el cuerpo;
  - GET por id: 200;
  - GET del listado: 200 con la forma paginada;
  - DELETE: 204;
  - las políticas por acción: `InventoryWrite` en POST, PUT y DELETE, e `InventoryRead` en los dos GET.

  Verificar: `dotnet test` en verde.
- [x] 8.3 Crear `AddProducts` con los puertos, sus implementaciones y los cinco handlers, y llamarla desde `Program.cs`. Añadir `ProducesResponseType` para que Swagger muestre 201/200/204, 400, 401, 403, 404 y 409 donde corresponda. Verificar: `dotnet build` y `dotnet test` en verde; `docker compose up -d --build` arranca la API y Swagger UI muestra los cinco endpoints.
- [x] 8.4 Documentar en `docs/api-contract.md` la sección de productos: endpoints y scopes, cuerpos de petición y respuesta, la representación con `categoryName` y `stockQuantity`, los filtros `categoryId` y `search` (contiene, sin distinguir mayúsculas, comodines literales), el SKU reservado tras el borrado, que el `PUT` reemplaza `description` e ignora `sku` y `stockQuantity`, el borrado con stock congelado y el orden de los errores. Registrar en `docs/assumptions.md` los supuestos de D4 (404 antes que la categoría en el `PUT`), D5 (`UpdatedAtUtc` en el borrado) y D10 (formato de las fechas). Actualizar `docs/business-rules.md` (BR-01: único también entre los borrados; BR-06: se permite con stock, que queda congelado; BR-07: el `PUT` también lo ignora), `docs/architecture.md` (casos de uso de productos y `AddPersistence`) y añadir en `docs/decisions.md` `DEC-003 · SKU reservado para siempre` y `DEC-004 · Borrado de productos con stock`, con sus alternativas y consecuencias. Verificar: los documentos coinciden con `specs/products` y con `design.md`.

## 9. Verificación de extremo a extremo

- [x] 9.1 Con `docker compose up -d --build` y tokens de Keycloak obtenidos con `curl`, recorrer cada escenario de `specs/products`, incluidos:
  - dos `POST` simultáneos con el mismo SKU (uno 201 y otro 409);
  - un `POST` con el SKU de un producto borrado (409);
  - el alta con la categoría bloqueada desde `sqlcmd` con `BEGIN TRAN; UPDATE dbo.Categories SET IsActive = 0 ...` (el `POST` espera y, tras el `COMMIT`, responde 400);
  - `search=50%` y `categoryId=abc`;
  - el borrado de un producto con stock puesto a mano con `sqlcmd` (la fila conserva el stock);
  - los escenarios de BR-05 de `specs/categories` usando ahora `POST` y `DELETE /api/products`.

  Verificar: cada escenario queda anotado en `docs/bitacora.md` con su comando y su resultado, y todos dan el resultado esperado.
- [x] 9.2 Ejecutar la revisión de Clean Code (skill `clean-code-review` o el subagente `code-reviewer`, en una sesión distinta de la que implementó) sobre el diff de la rama, y corregir o registrar en `docs/assumptions.md` cada hallazgo. Verificar: `dotnet build` y `dotnet test` en verde después de las correcciones, y la cobertura con Coverlet anotada en `docs/bitacora.md`.

## Workflow follow-up

- Actualizar `docs/ai-usage.md` con el uso de IA en este entregable.
- Abrir el pull request de `feat/add-products` hacia `main`.
- Archivar el cambio con `/opsx:archive` cuando el PR esté aprobado, y sincronizar la spec `products`.
