# Tasks

## 1. Base del dominio

- [x] 1.1 Crear `ValidationException`, `NotFoundException` y `ConflictException` en `src/Inventory.Domain/Exceptions/` (una por archivo, con el mensaje en español en el constructor). Eliminar `src/Inventory.Domain/Class1.cs`. Verificar: `dotnet build` sin advertencias y `dotnet test` en verde.
- [x] 1.2 TDD de `Guard` en `src/Inventory.Domain/Guard.cs`: primero las pruebas que fallan en `tests/Inventory.UnitTests/Domain/GuardTests.cs` (texto obligatorio: nulo, vacío o solo espacios lanza `ValidationException`; longitud máxima: en el límite pasa y un carácter más lanza `ValidationException`). Después, el código mínimo. Verificar: `dotnet test` con `GuardTests` en verde.

## 2. Entidad Category (BR-04)

- [x] 2.1 TDD de `Category.Create` en `src/Inventory.Domain/Entities/Category.cs` (D2): una prueba por escenario de los requisitos "Nombre de categoría obligatorio y acotado" y "Descripción opcional y acotada" de `specs/categories` (recorte del nombre, ausente, solo espacios, 100 y 101 caracteres, descripción solo con espacios a `null`, 501 caracteres), más `IsActive = true` y `CreatedAtUtc` recibido. Pruebas en `tests/Inventory.UnitTests/Domain/CategoryTests.cs`, con nombres `BR04_...`. Verificar: `dotnet test` con `CategoryTests` en verde.

## 3. Paginación (BR-18)

- [x] 3.1 TDD de `PageRequest.Create` en `src/Inventory.Application/Common/PageRequest.cs` (D6): valores por defecto 1 y 20; `page=0`, `pageSize=0` y `pageSize=101` lanzan `ValidationException` con un mensaje en español que nombra el parámetro; `pageSize=100` es válido. Pruebas `BR18_...` en `tests/Inventory.UnitTests/Common/PageRequestTests.cs`. Verificar: `dotnet test` en verde.
- [x] 3.2 TDD de `PagedResult<T>` en `src/Inventory.Application/Common/PagedResult.cs` (`Items`, `Page`, `PageSize`, `TotalCount` y `TotalPages`, que es 0 sin elementos, 2 con 25 de 20, y conserva `Page` aunque esté fuera de rango). Pruebas en `tests/Inventory.UnitTests/Common/PagedResultTests.cs`. Eliminar `src/Inventory.Application/Class1.cs`. Verificar: `dotnet test` en verde.

## 4. Casos de uso de categorías (Application)

- [x] 4.1 Crear los puertos `IClock` (`Abstractions/`), `ICategoryWriteRepository` y `ICategoryReadRepository`, el DTO `CategoryDto` y el enum `CategoryDeactivationResult` en `src/Inventory.Application/` (D1). Crear los fakes `FakeClock`, `FakeCategoryWriteRepository` y `FakeCategoryReadRepository` en `tests/Inventory.UnitTests/Fakes/`. Verificar: `dotnet build` sin advertencias.
- [x] 4.2 TDD de `CreateCategoryCommand` y `CreateCategoryHandler` (solo el puerto de escritura): alta correcta con la fecha de `IClock`, alta sin descripción, nombre repetido con otras mayúsculas o con espacios (`ConflictException` con el mensaje en español) y error de validación de la entidad. Pruebas `BR04_...` en `tests/Inventory.UnitTests/Categories/CreateCategoryHandlerTests.cs`. Verificar: `dotnet test` en verde.
- [x] 4.3 TDD de `DeleteCategoryCommand` y `DeleteCategoryHandler` (solo el puerto de escritura, D4): `Deactivated` termina sin error, `NotFound` lanza `NotFoundException` y `HasActiveProducts` lanza `ConflictException`, los dos con el mensaje en español. Pruebas `BR05_...` en `tests/Inventory.UnitTests/Categories/DeleteCategoryHandlerTests.cs`. Verificar: `dotnet test` en verde.
- [x] 4.4 TDD de `GetCategoryByIdQuery` y `GetCategoryByIdHandler` (solo el puerto de lectura): devuelve el DTO, o lanza `NotFoundException` con el mensaje en español si no hay ninguna categoría activa con ese id. Pruebas en `tests/Inventory.UnitTests/Categories/GetCategoryByIdHandlerTests.cs`. Verificar: `dotnet test` en verde.
- [x] 4.5 TDD de `ListCategoriesQuery` y `ListCategoriesHandler` (solo el puerto de lectura): construye `PageRequest` desde la consulta y devuelve el `PagedResult<CategoryDto>` del puerto; los parámetros inválidos lanzan `ValidationException` antes de llamar al puerto. Pruebas `BR18_...` en `tests/Inventory.UnitTests/Categories/ListCategoriesHandlerTests.cs`. Verificar: `dotnet test` en verde.
- [x] 4.6 Prueba de arquitectura CQRS (D1) en `tests/Inventory.UnitTests/Architecture/CqrsSeparationTests.cs`. Por reflexión sobre el ensamblado de Application, comprueba que ningún handler de comando (una clase `*Handler` cuyo método `HandleAsync` recibe un tipo `*Command`) tiene en su constructor un puerto `I*ReadRepository`, y que ningún handler de consulta (uno que recibe un tipo `*Query`) tiene un `I*WriteRepository`. Verificar: `dotnet test` en verde, y la prueba falla si se añade a mano un `ICategoryReadRepository` al constructor de `CreateCategoryHandler`.

## 5. Base de datos

- [x] 5.1 Actualizar `db/init.sql` según D11: `IsActive` y la collation `SQL_Latin1_General_CP1_CI_AS` en el `CREATE TABLE`, el `ALTER TABLE ADD IsActive` protegido, la eliminación del índice sin filtro, el `ALTER COLUMN` de la collation protegido y `UX_Categories_Name` filtrado `WHERE IsActive = 1`. Actualizar `docs/data-model.md` (columna, collation, índice filtrado y el requisito de `QUOTED_IDENTIFIER ON`). Verificar con Docker, y anotar en `docs/bitacora.md`:
  - Con un volumen nuevo (`docker compose down -v` y `up -d --build`), `dbinit` termina con código 0.
  - Con el volumen anterior a este cambio, `dbinit` también termina con código 0.
  - Al ejecutar `init.sql` una segunda vez, no cambia nada.
  - `sys.indexes` muestra `UX_Categories_Name` con `has_filter = 1`.
  - `sys.columns` muestra la collation esperada.

## 6. Persistencia (Infrastructure)

- [x] 6.1 Consultar Context7 con Dapper 2.1 y EF Core 6 (`DbContext`, `AddDbContext` con `IServiceProvider`, `QueryTrackingBehavior`, `AsNoTracking`, mapeo de tabla). Si Context7 devuelve documentación de otra versión mayor, avisar e ignorarla. Anotar en `docs/assumptions.md` las APIs que se confirmaron. Verificar: la entrada existe en `docs/assumptions.md`.
- [x] 6.2 TDD de `DatabaseOptions` (`src/Inventory.Infrastructure/Configuration/`) y su registro en Api, con `Validate` y `ValidateOnStart` (D9). Las pruebas, en `tests/Inventory.UnitTests/Configuration/DatabaseOptionsTests.cs` y con el patrón de `AuthOptionsTests`, comprueban que sin `ConnectionStrings:Inventory` (o vacía) la validación falla y el mensaje nombra `ConnectionStrings__Inventory`, y que con la cadena informada pasa. Verificar: `dotnet test` en verde; `dotnet run` sin la variable falla al arrancar con ese mensaje.
- [x] 6.3 Implementar `SystemClock`, `SqlConnectionFactory` y `CategoryWriteRepository` con Dapper (D1, D3 y D4), sin usar EF Core en el lado de escritura:
  - comprobar que existe una categoría activa con ese nombre,
  - insertar, traduciendo `SqlException` 2601/2627 en `ConflictException`,
  - desactivar con la sentencia atómica y la consulta de diagnóstico.
  
  Parámetros `nvarchar` con su longitud. Eliminar `src/Inventory.Infrastructure/Class1.cs`. Verificar: `dotnet build` sin advertencias; la verificación con la base se hace en la tarea 10.1.
- [x] 6.4 Implementar `InventoryReadDbContext` (sin seguimiento por defecto), `CategoryRecord` y `CategoryReadRepository` con EF Core (D1 y D5), sin `SaveChanges` ni Dapper en el lado de lectura: obtener por id solo si está activa, y listar las activas ordenadas por `Name` y luego `Id`, con `CountAsync`, `Skip` y `Take` y `AsNoTracking()` explícito. Verificar: `dotnet build` sin advertencias; la verificación con la base se hace en la tarea 10.1.

## 7. Errores de la API

- [x] 7.1 TDD de `ExceptionHandlingMiddleware` en `src/Inventory.Api/Middleware/` (D7): pruebas con `DefaultHttpContext` en `tests/Inventory.UnitTests/Middleware/ExceptionHandlingMiddlewareTests.cs`.
  - `ValidationException`, `NotFoundException` y `ConflictException` producen 400, 404 y 409.
  - Cualquier otra excepción produce 500 con un `detail` genérico que no contiene el mensaje de la excepción.
  - En todos los casos, `Content-Type` es `application/problem+json`, `status` coincide con el código y `title` y `detail` están en español.
  - Sin excepción, la respuesta no se modifica.
  
  Verificar: `dotnet test` en verde.
- [x] 7.2 TDD de la fábrica de respuesta para un modelo inválido (D8), en `src/Inventory.Api/Configuration/`. Un `ModelStateDictionary` con la clave `$` produce 400 con "El cuerpo de la petición no es un JSON válido.", y uno con la clave `pageSize` produce 400 con un `detail` en español que nombra `pageSize`, en los dos casos con `application/problem+json`. Pruebas en `tests/Inventory.UnitTests/Configuration/`. Verificar: `dotnet test` en verde.
- [x] 7.3 Registrar en `Program.cs` el middleware (lo primero del pipeline) y la fábrica (`ApiBehaviorOptions.InvalidModelStateResponseFactory`). Documentar en `docs/api-contract.md` la sección de formato de errores: ProblemDetails para 400, 404, 409 y 500, un ejemplo de cada uno, y que 401 y 403 se mantienen con el cuerpo vacío. Verificar: `dotnet build` y `dotnet test` en verde, y la sección cubre todos los escenarios de `specs/api-errors`.

## 8. Endpoints de categorías

- [x] 8.1 TDD de `CategoriesController` (`[Authorize]`, ruta `api/categories`, `{id:guid}`), con los contratos `CreateCategoryRequest` (`string? Name`, `string? Description`, D8) y `PaginationRequest`. Las pruebas, en `tests/Inventory.UnitTests/Controllers/CategoriesControllerTests.cs`, usan handlers reales sobre los fakes y comprueban:
  - POST: 201 con el cuerpo y `Location` igual a `/api/categories/{id}`.
  - GET por id: 200.
  - GET del listado: 200 con la forma `{ items, page, pageSize, totalCount, totalPages }`.
  - DELETE: 204.
  - Las políticas por acción: `InventoryWrite` en POST y DELETE, `InventoryRead` en los dos GET.
  
  Verificar: `dotnet test` en verde.
- [x] 8.2 Registrar en `Program.cs` los handlers, los puertos con sus implementaciones, `IClock`, `DatabaseOptions` y `InventoryReadDbContext`. Añadir `ProducesResponseType` para que Swagger muestre 201/200/204, 400, 401, 403, 404 y 409. Verificar: `dotnet build` y `dotnet test` en verde; `docker compose up -d --build` arranca la API y Swagger UI muestra los cuatro endpoints.
- [x] 8.3 Documentar en `docs/api-contract.md` la sección de categorías: los endpoints, el scope de cada uno, los cuerpos de petición y respuesta, la paginación (BR-18, la forma de la respuesta, el orden y una página fuera de rango), la unicidad y la reutilización del nombre, el borrado lógico con 204, 404 y 409, y el 404 sin cuerpo para un id que no es un GUID (D10). Registrar el supuesto de D10 y el tratamiento de la descripción en `docs/assumptions.md`. Verificar: la sección cubre todos los escenarios de `specs/categories`.

## 9. Retirada de los endpoints de diagnóstico y documentación de reglas

- [x] 9.1 Eliminar `check-read` y `check-write` de `AuthController`, `src/Inventory.Api/Contracts/AccessCheckResponse.cs` y sus pruebas en `AuthControllerTests` (D12). Eliminar `tests/Inventory.UnitTests/UnitTest1.cs`. Actualizar `docs/api-contract.md` (quitar los dos endpoints y la nota de "Temporales") y el paso "Probar" de `README.md` para que use `GET /api/categories` y `POST /api/categories`. Verificar: `dotnet build` y `dotnet test` en verde, y `grep -rn "check-read\|check-write\|AccessCheckResponse" src tests README.md docs/api-contract.md` no devuelve nada.
- [x] 9.2 Actualizar `docs/business-rules.md`:
  - BR-05: eliminar una categoría es lógico (`IsActive = false`), solo se permite sin productos activos, y el nombre queda libre.
  - BR-03: la categoría debe existir y estar activa.
  - Entidad Category: añadir `IsActive`.
  - BR-04: el nombre se recorta y es único entre las categorías activas.
  
  Actualizar `docs/architecture.md`: casos de uso `CreateCategory`, `DeleteCategory` (desactivación), `GetCategoryById` y `ListCategories`, más `PageRequest` y `PagedResult` en Common. Añadir `DEC-002 · Borrado lógico de categorías` en `docs/decisions.md`, con las alternativas descartadas (borrado físico y bloqueo con cualquier producto) y las consecuencias (índice filtrado, nombre reutilizable). Verificar: los tres documentos coinciden con `specs/categories` y con `design.md`.

## 10. Verificación de extremo a extremo

- [x] 10.1 Con `docker compose up -d --build` y tokens de Keycloak obtenidos con `curl`, recorrer cada escenario de `specs/categories`, `specs/api-errors`, `specs/api-auth` (MODIFIED) y `specs/dev-identity-provider` (MODIFIED), incluidos:
  - dos `POST` simultáneos con el mismo nombre (uno da 201 y el otro 409),
  - desactivar una categoría con un producto activo y con solo productos inactivos, insertando los productos a mano con `sqlcmd`,
  - reutilizar el nombre de una categoría desactivada,
  - el 500 genérico con `sqlserver` detenido.
  
  Verificar: cada escenario, con su comando y su resultado, queda anotado en `docs/bitacora.md`, y todos dan el resultado esperado.
- [x] 10.2 Ejecutar la revisión de Clean Code (skill `clean-code-review` o el subagente `code-reviewer`, en una sesión distinta de la que implementó) sobre el diff de la rama, y corregir o registrar en `docs/assumptions.md` cada hallazgo. Verificar: `dotnet build` y `dotnet test` en verde después de las correcciones, y la cobertura con Coverlet anotada en `docs/bitacora.md`.

## Workflow follow-up

- Actualizar `docs/ai-usage.md` con el uso de IA en este entregable.
- Abrir el pull request de `feat/add-categories` hacia `main`.
- Archivar el cambio con `/opsx:archive` cuando el PR esté aprobado, y sincronizar las specs `categories`, `api-errors`, `api-auth` y `dev-identity-provider`.
