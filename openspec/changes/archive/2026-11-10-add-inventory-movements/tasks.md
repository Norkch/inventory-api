# Tasks

## 1. Documentación de las APIs que se van a usar

- [x] 1.1 Consultar Context7 con la versión 6 de cada librería sobre:
  - `JwtBearerEvents.OnTokenValidated` y `TokenValidatedContext.Fail(string)` (D6);
  - si `ControllerBase.Created` admite una URI nula y, si no, el uso de `StatusCode(StatusCodes.Status201Created, value)` (D7);
  - en Dapper 2.1, `DbConnection.BeginTransaction` con `CommandDefinition(..., transaction: ...)` y `QuerySingleOrDefaultAsync<int?>` con `UPDATE ... OUTPUT` (D3);
  - en EF Core 6, `OrderByDescending(...).ThenByDescending(...)` con `Skip`/`Take` y el mapeo de una columna `TINYINT` a `byte` (D5).

  Si Context7 devuelve documentación de otra versión mayor (ya ocurrió al escribir el diseño), avisar y confirmar las firmas en el XML de documentación del paquete 6.x. Anotar lo confirmado y su origen en `docs/assumptions.md`. Verificar: la entrada existe en `docs/assumptions.md`.

## 2. Token sin `sub` (delta de `api-auth`)

- [x] 2.1 TDD de la comprobación del claim `sub` (D6), en una clase de `src/Inventory.Api/Configuration/` que recibe un `ClaimsPrincipal`. Pruebas en `tests/Inventory.UnitTests/Configuration/`:
  - con `sub` informado se acepta;
  - sin `sub`, con `sub` vacío y con `sub` solo con espacios se rechaza.

  Verificar: `dotnet test` en verde.
- [x] 2.2 Conectar la comprobación a `options.Events.OnTokenValidated` en `ConfigureJwtBearerOptions`, llamando a `context.Fail(...)` cuando se rechaza. Ampliar `ConfigureJwtBearerOptionsTests` para comprobar que el evento está asignado y que falla con un principal sin `sub`. Registrar en `docs/assumptions.md` que `sub` es el UUID del usuario de servicio de Keycloak y que el 401 por falta de `sub` afecta a todos los endpoints. Actualizar la sección de seguridad de `docs/architecture.md`. Verificar: `dotnet test` en verde y `docker compose up -d --build` sigue respondiendo 200 a `GET /api/categories` con un token real.

## 3. Dominio de movimientos (BR-11)

- [x] 3.1 TDD de `MovementType` y `MovementRules` en `src/Inventory.Domain/` (D2). Pruebas `BR11_...` en `tests/Inventory.UnitTests/Domain/MovementRulesTests.cs`:
  - `ValidateType`: `"Entry"`, `"exit"` y `"  ENTRY  "` se aceptan; `null`, `""`, `"   "`, `"Salida"`, `"1"` y `"3"` lanzan `ValidationException`. Sin `Enum.TryParse`;
  - `ValidateQuantity`: `null`, `0` y `-3` fallan y `1` pasa;
  - `ValidateReason`: solo espacios da `null`, se recorta, 250 pasa y 251 falla;
  - `ValidateRegisteredBy`: vacío falla, 100 pasa y 101 falla.

  Todos los mensajes van en español. Verificar: `dotnet build` sin advertencias y `dotnet test` en verde.
- [x] 3.2 TDD de `InventoryMovement.Create` en `src/Inventory.Domain/Entities/InventoryMovement.cs` (D2). Pruebas en `tests/Inventory.UnitTests/Domain/InventoryMovementTests.cs`:
  - usa `MovementRules` y fija `Id` y `OccurredAtUtc` desde lo que recibe;
  - el delta es `+quantity` en una entrada y `-quantity` en una salida;
  - se puede asignar `StockAfter` a partir del resultado del repositorio.

  Verificar: `dotnet test` en verde.
- [x] 3.3 Crear `InsufficientStockException` en `src/Inventory.Domain/Exceptions/`, heredando de `ConflictException` (D4). Ampliar las pruebas de `ExceptionHandlingMiddleware` para comprobar que responde 409 con ProblemDetails. Verificar: `dotnet test` en verde.

## 4. Puertos, DTO y mensajes (Application)

- [x] 4.1 Crear en `src/Inventory.Application/Movements/` lo siguiente:
  - `MovementRegistrationStatus`;
  - `MovementRegistrationResult`;
  - `MovementRegistrant`;
  - `MovementListCriteria`;
  - los puertos `IMovementWriteRepository` (`RegisterAsync(InventoryMovement, ...)`) e `IMovementReadRepository` (`ExistsActiveProductAsync`, `ListAsync(MovementListCriteria, ...)`);
  - `MovementDto`, con `Type` como `string` canónico y `From(InventoryMovement)`;
  - `MovementMessages`, con los mensajes en español de stock insuficiente y de desbordamiento. El 404 reutiliza `ProductMessages.ActiveProductNotFound`.

  Crear `FakeMovementWriteRepository` y `FakeMovementReadRepository` en `tests/Inventory.UnitTests/Fakes/`. Verificar: `dotnet build` sin advertencias.

## 5. Casos de uso de movimientos

- [x] 5.1 TDD de `RegisterMovementCommand` y `RegisterMovementHandler`, que solo usan el puerto de escritura y `IClock` (D4). Pruebas `BR11_...`, `BR12_...` y `BR15_...` en `tests/Inventory.UnitTests/Movements/RegisterMovementHandlerTests.cs`:
  - registro correcto: la hora sale de `IClock`, `registeredBy` del `MovementRegistrant` y `stockAfter` del resultado;
  - `ProductNotFound` lanza `NotFoundException`;
  - `InsufficientStock` lanza `InsufficientStockException`;
  - `StockOverflow` lanza `ConflictException` con su mensaje;
  - con un cuerpo inválido se lanza `ValidationException` sin llamar al puerto, para cumplir el orden 400, 404, 409.

  Verificar: `dotnet test` en verde.
- [x] 5.2 TDD de `ListMovementsQuery` y `ListMovementsHandler`, que solo usan el puerto de lectura (D5). Pruebas `BR18_...` en `tests/Inventory.UnitTests/Movements/ListMovementsHandlerTests.cs`:
  - una paginación inválida lanza `ValidationException` sin consultar el producto;
  - un producto no activo lanza `NotFoundException` sin listar;
  - con un producto activo, devuelve el `PagedResult<MovementDto>` del puerto, también cuando está vacío.

  Verificar: `dotnet test` en verde y `CqrsSeparationTests` sigue en verde con los handlers nuevos.

## 6. Persistencia (Infrastructure)

- [x] 6.1 Implementar `MovementWriteRepository` con Dapper, sin EF Core (D3). Una transacción explícita contiene:
  - el `UPDATE ... OUTPUT INSERTED.StockQuantity` condicional;
  - el `INSERT` con `SupplierId` a `NULL`;
  - la lectura de diagnóstico de `IsActive` cuando no hay filas afectadas;
  - la traducción de `SqlException` 8115 a `StockOverflow`.

  Los parámetros llevan tipos y longitudes explícitos, tomados de `MovementRules`. No se toca `UpdatedAtUtc`. Verificar: `dotnet build` sin advertencias. La verificación con la base se hace en la tarea 8.1.
- [x] 6.2 Implementar `MovementRecord`, el mapeo de `InventoryMovements` en `InventoryReadDbContext` y `MovementReadRepository` con EF Core, sin `SaveChanges` ni Dapper (D5). Incluye `ExistsActiveProductAsync` sobre `Products` y `ListAsync` con:
  - filtro por `ProductId`;
  - orden `OccurredAtUtc DESC` y luego `Id DESC`;
  - `CountAsync`, `Skip` y `Take`;
  - `AsNoTracking()` explícito.

  Verificar: `dotnet build` sin advertencias. La verificación con la base se hace en la tarea 8.1.
- [x] 6.3 Documentar en `docs/data-model.md`, en `Products.StockQuantity` y en `InventoryMovements`, que el stock solo cambia mediante el `UPDATE` condicional atómico más el `INSERT` en una transacción, y que `SupplierId` queda `NULL` hasta BR-16. La nota remite a este diseño. Verificar: el documento coincide con D3.

## 7. Endpoints de movimientos

- [x] 7.1 TDD de `MovementsController` (D1, D7) y del contrato `RegisterMovementRequest` (`string? Type`, `int? Quantity`, `string? Reason`):
  - `[Authorize]`, con la ruta `api/products/{productId:guid}/movements`;
  - POST con la política `InventoryWrite`: construye `MovementRegistrant` desde el claim `sub` y responde 201 con el cuerpo y **sin** cabecera `Location`;
  - GET con la política `InventoryRead`: recibe `PaginationRequest` y responde 200 con la forma paginada;
  - no hay acciones `PUT`, `PATCH` ni `DELETE` (BR-13).

  Las pruebas van en `tests/Inventory.UnitTests/Controllers/MovementsControllerTests.cs` y usan handlers reales sobre los fakes. Verificar: `dotnet test` en verde.
- [x] 7.2 Crear `AddMovements` con los puertos, sus implementaciones y los dos handlers, y llamarla desde `Program.cs`. Añadir `ProducesResponseType` para 201, 200, 400, 401, 403, 404 y 409 donde corresponda. Verificar: `dotnet build` y `dotnet test` en verde, `docker compose up -d --build` arranca la API y Swagger UI muestra los dos endpoints.
- [x] 7.3 Documentar en `docs/api-contract.md` la sección de movimientos con estos puntos:
  - endpoints y scopes, cuerpo y representación;
  - `type` tolerante con forma canónica;
  - 201 sin `Location` y su motivo;
  - campos ignorados (`supplierId` y otros);
  - `occurredAtUtc` con o sin `Z`;
  - el orden del listado y el orden de los errores;
  - la ausencia de `PUT` y `DELETE`;
  - el 401 por falta de `sub` en la sección de autenticación.

  Además:
  - registrar en `docs/assumptions.md` el 201 sin `Location` y el riesgo de orden de D5 con `IClock`;
  - actualizar `docs/architecture.md` (casos de uso de movimientos y `AddMovements`) y `docs/business-rules.md` (BR-16 aplazada; desbordamiento con 409).

  Verificar: los documentos coinciden con `specs/inventory-movements`, `specs/api-auth` y `design.md`.

## 8. Verificación de extremo a extremo

- [x] 8.1 Con `docker compose up -d --build` y tokens de Keycloak obtenidos con `curl`, recorrer cada escenario de `specs/inventory-movements` y `specs/api-auth`, incluidos:
  - entrada y salida correctas, comprobando `stockQuantity` con `GET /api/products/{id}` y que `updatedAtUtc` no cambia;
  - salida mayor que el stock (409) y salida igual al stock (201 con `stockAfter` 0);
  - dos salidas simultáneas de 4 con stock 5: una 201 y otra 409, con stock final 1 y un solo movimiento nuevo;
  - desbordamiento, poniendo antes con `sqlcmd` `StockQuantity = 2147483000`: entrada de 1000 da 409 y entrada de 647 da 201;
  - atomicidad: provocar un fallo del `INSERT` desde `sqlcmd`, por ejemplo con un bloqueo o un `CHECK` temporal sobre `InventoryMovements`, y comprobar que el stock no cambia;
  - movimiento y listado sobre un producto borrado (404) y listado de un producto activo sin movimientos (200 vacío);
  - `DELETE` y `PUT` sobre las rutas de movimientos;
  - un token sin `sub`, si se puede generar con un cliente de prueba del realm; si no, documentar por qué solo lo cubren las pruebas unitarias.

  Verificar: cada escenario queda anotado en `docs/bitacora.md` con su comando y su resultado, y todos dan el resultado esperado.
- [x] 8.2 Ejecutar la revisión de Clean Code (skill `clean-code-review` o subagente `code-reviewer`, en una sesión distinta de la que implementó) sobre el diff de la rama, y corregir o registrar en `docs/assumptions.md` cada hallazgo. Verificar: `dotnet build` y `dotnet test` en verde después de las correcciones, con la cobertura de Coverlet anotada en `docs/bitacora.md`.

## Workflow follow-up

- Actualizar `docs/ai-usage.md` con el uso de IA en este entregable.
- Abrir el pull request de `feat/add-inventory-movements` hacia `main`.
- Archivar el cambio con `/opsx:archive` cuando el PR esté aprobado y sincronizar las specs `inventory-movements` y `api-auth`.
