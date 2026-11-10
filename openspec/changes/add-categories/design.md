# Design

## Context

- `Inventory.Domain`, `Inventory.Application` (salvo `Security/`) e `Inventory.Infrastructure` solo tienen la plantilla (`Class1.cs`). No hay entidades, puertos, repositorios, `DbContext` ni middleware de errores.
- `Inventory.Infrastructure.csproj` ya referencia Dapper 2.1.89 y `Microsoft.EntityFrameworkCore.SqlServer` 6.0.36. Este último trae `Microsoft.Data.SqlClient`, `Microsoft.Extensions.Options` y la inyección de dependencias. No hace falta ningún paquete nuevo.
- `docker-compose.yml` ya pasa `ConnectionStrings__Inventory` a la API. `db/init.sql` ya crea `Categories` (sin `IsActive`), `Products` con `IX_Products_CategoryId INCLUDE (IsActive)` y `UX_Categories_Name` sin filtro. `dbinit` ejecuta `init.sql` en cada `docker compose up`, y el volumen `sqldata` conserva las bases ya creadas.
- `Inventory.UnitTests` referencia `Inventory.Api` e `Inventory.Application`. No hay `TestHost`, ni proveedor en memoria de EF Core, ni Testcontainers, y no se pueden añadir paquetes.
- Las validaciones de `AuthOptions` se registran en Api, porque `ValidateDataAnnotations` y `ValidateOnStart` están en el framework compartido de ASP.NET Core (ver `docs/assumptions.md`).

Motivación en `proposal.md`. Requisitos en `specs/categories`, `specs/api-errors`, `specs/api-auth` y `specs/dev-identity-provider`.

## Goals / Non-Goals

**Goals:**
- Dejar montada la base que reutilizarán productos, proveedores y movimientos: excepciones de dominio, `Guard`, paginación, conexión de Dapper, `DbContext` de lectura y middleware de errores.
- Que toda la lógica propia (validación de la entidad, paginación, handlers, traducción de errores) se pueda probar con xUnit y fakes.

**Non-Goals:**
- Pruebas automáticas contra SQL Server de los repositorios y de `init.sql`. Se verifican a mano con Docker y se anotan en `docs/bitacora.md`.
- Un sistema de migraciones (EF Migrations, DbUp). `init.sql` sigue siendo idempotente y escrito a mano.
- Comparar nombres sin distinguir acentos: `"Tornilleria"` y `"Tornillería"` son nombres distintos.

## Decisions

### D1. CQRS: escrituras con Dapper, lecturas con EF Core, y dónde vive cada pieza

**Separación de escrituras y lecturas**

```
                 Comandos (POST, DELETE)                 Consultas (GET)
                          |                                     |
   CreateCategoryHandler, DeleteCategoryHandler   GetCategoryByIdHandler, ListCategoriesHandler
                          |                                     |
               ICategoryWriteRepository              ICategoryReadRepository
                          |                                     |
          CategoryWriteRepository (Dapper)      CategoryReadRepository (EF Core, AsNoTracking)
                          |                                     |
                SqlConnectionFactory                  InventoryReadDbContext
                           \                                   /
                            +-------> dbo.Categories <--------+
```

- **Escrituras con Dapper:**
  - `INSERT` y el `UPDATE` atómico del borrado lógico son SQL explícito (D4).
  - El error 2601/2627 del índice único se captura justo donde se ejecuta la sentencia (D3).
  - Las comprobaciones que necesita un comando (nombre activo repetido) las expone el puerto de **escritura**, no el de lectura, como pide `docs/architecture.md`.
- **Lecturas con EF Core:** la proyección a `CategoryDto`, el filtro, el orden y `Skip`/`Take` se escriben con LINQ y se traducen a una sola consulta. Siempre van sin seguimiento: `QueryTrackingBehavior.NoTracking` en el contexto y `AsNoTracking()` explícito en cada consulta (D5).
- **Nunca se mezclan:**
  - Un handler de comando recibe solo `ICategoryWriteRepository` (más `IClock`), y uno de consulta recibe solo `ICategoryReadRepository`.
  - `InventoryReadDbContext` no se usa para escribir: no se llama a `SaveChanges` en ningún sitio.
  - Los repositorios Dapper no se usan para leer listados.
  - Una prueba de arquitectura lo comprueba por reflexión (tarea 4.6).
- **Por qué dos tecnologías:** es una restricción del proyecto (`AGENTS.md`). En este cambio, además, las escrituras necesitan control fino del SQL (atomicidad e índice filtrado), mientras que en las lecturas lo que importa es componer consultas paginadas sin escribir SQL a mano.
- **Alternativas descartadas:**
  - Solo EF Core: el borrado condicional atómico exigiría `ExecuteSqlRaw` (EF Core 6 no tiene `ExecuteUpdate`), que es Dapper con otro nombre.
  - Solo Dapper: habría que concatenar SQL para la paginación y el orden en cada listado futuro.

**Dónde vive cada pieza**

```
Domain/          Entities/Category  Exceptions/{ValidationException, NotFoundException, ConflictException}  Guard
Application/     Abstractions/IClock
                 Common/{PageRequest, PagedResult<T>}
                 Categories/  CreateCategoryCommand + CreateCategoryHandler      --> ICategoryWriteRepository
                              DeleteCategoryCommand + DeleteCategoryHandler      --> ICategoryWriteRepository
                              GetCategoryByIdQuery  + GetCategoryByIdHandler     --> ICategoryReadRepository
                              ListCategoriesQuery   + ListCategoriesHandler      --> ICategoryReadRepository
                              CategoryDto, CategoryDeactivationResult
Infrastructure/  Configuration/DatabaseOptions
                 Persistence/Write/{SqlConnectionFactory, CategoryWriteRepository}
                 Persistence/Read/{InventoryReadDbContext, CategoryRecord, CategoryReadRepository}
                 SystemClock
Api/             Controllers/CategoriesController
                 Contracts/{CreateCategoryRequest, PaginationRequest}
                 Middleware/ExceptionHandlingMiddleware
                 Configuration/{DatabaseOptionsServiceCollectionExtensions, InvalidModelStateResponse...}
```

- Cada clase, record o enum va en su propio archivo. Los handlers reciben el objeto comando o consulta completo, no sus propiedades sueltas.
- Un handler de comando solo usa `ICategoryWriteRepository` y un handler de consulta solo usa `ICategoryReadRepository`.

### D2. Validación de BR-04 en la entidad
`Category.Create` recorta `name` y `description`, valida con `Guard` y lanza `ValidationException` con el mensaje en español. Una descripción vacía o compuesta solo por espacios queda en `null`. Los límites (100 y 500) se comprueban después de recortar.
- **Fecha de creación:** `CreatedAtUtc` la fija la aplicación con el puerto `IClock` (`SystemClock` en Infrastructure devuelve `DateTime.UtcNow`). Así, las pruebas usan un reloj falso y la respuesta 201 no necesita volver a leer la fila.
- **Alternativa descartada:** el `DEFAULT SYSUTCDATETIME()` de la columna, con `OUTPUT INSERTED.CreatedAtUtc`. Funciona, pero deja la entidad incompleta hasta después de insertarla y obliga a probar la fecha contra la base.

### D3. Unicidad sin distinguir mayúsculas: collation explícita, índice filtrado y traducción del error
- **Collation:** la columna `Name` se declara con `COLLATE SQL_Latin1_General_CP1_CI_AS`, para que la comparación sin distinguir mayúsculas no dependa de la collation por defecto de la instancia. La comprobación previa (`WHERE Name = @Name AND IsActive = 1`) y el índice usan la misma regla.
- **Índice:** `UX_Categories_Name ON Categories(Name) WHERE IsActive = 1`. Permite reutilizar el nombre de una categoría desactivada.
- **Altas simultáneas:** el handler comprueba antes con `ICategoryWriteRepository` para dar un 409 claro en el caso normal. El índice es la defensa final: `CategoryWriteRepository` captura `SqlException` con número 2601 o 2627 y lanza `ConflictException` con el mismo mensaje.
- **Alternativa descartada:** una columna calculada `NormalizedName` (`UPPER(Name)`). Duplica el dato y no aporta nada si la collation ya es explícita.
- **Requisito de `QUOTED_IDENTIFIER`:** los índices filtrados exigen `QUOTED_IDENTIFIER ON` en las sesiones que modifican la tabla. `init.sql` ya lo activa y `Microsoft.Data.SqlClient` lo activa por defecto al conectar.

### D4. Borrado lógico atómico
`ICategoryWriteRepository.DeactivateAsync` ejecuta una sola sentencia:

```sql
UPDATE dbo.Categories SET IsActive = 0
 WHERE Id = @Id AND IsActive = 1
   AND NOT EXISTS (SELECT 1 FROM dbo.Products WHERE CategoryId = @Id AND IsActive = 1);
```

- Si la sentencia afecta a una fila, el resultado es `Deactivated`.
- Si no afecta a ninguna, una segunda consulta (`SELECT 1 ... WHERE Id = @Id AND IsActive = 1`) distingue `NotFound` de `HasActiveProducts`.
- El repositorio devuelve el enum `CategoryDeactivationResult` y el handler lo traduce: 204, `NotFoundException` (404) o `ConflictException` (409).
- **Por qué una sola sentencia:** si se comprueba y se actualiza por separado, cuando exista el alta de productos (BR-03) se podría crear un producto activo entre los dos pasos.
- La segunda consulta solo sirve para elegir el mensaje. Si otra petición desactiva la categoría entre medias, el resultado es 404, que también es correcto.
- **Alternativa descartada:** devolver los dos motivos desde la misma sentencia con `OUTPUT` o un `CASE`. Es más difícil de leer y no protege más la consistencia.

### D5. Lecturas con EF Core
- `InventoryReadDbContext` tiene `QueryTrackingBehavior.NoTracking` por defecto, y los repositorios llaman también a `AsNoTracking()` de forma explícita.
- Mapea `CategoryRecord` (clase de Infrastructure) a `dbo.Categories`. El repositorio proyecta a `CategoryDto` en la consulta.
- El listado filtra `IsActive`, ordena con `OrderBy(Name).ThenBy(Id)`, ejecuta `CountAsync` y después `Skip((page - 1) * pageSize).Take(pageSize)`.
- `PagedResult<T>` calcula `TotalPages` como `ceil(totalCount / pageSize)`, que da 0 cuando no hay elementos.
- **Por qué `CategoryRecord` y no la entidad de dominio:** la entidad valida al construirse y no tiene setters. Un registro plano evita configurar EF Core para que construya entidades de dominio, y deja el dominio sin atributos ni dependencias de EF.

### D6. Paginación (BR-18)
- `PaginationRequest` (Api, `[FromQuery]`) tiene `int? Page` e `int? PageSize`.
- `PageRequest.Create` (Application/Common) aplica los valores por defecto (1 y 20), valida los rangos y lanza `ValidationException` con un mensaje en español que nombra `page` o `pageSize`.
- **Por qué en Application:** productos, proveedores y movimientos reutilizarán la misma regla, y así se prueba sin HTTP.

### D7. Errores con ProblemDetails
`ExceptionHandlingMiddleware` va primero en el pipeline. Traduce las excepciones así:

| Excepción | Respuesta |
|---|---|
| `ValidationException` | 400 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| Cualquier otra | 500 con un `detail` genérico en español, sin el mensaje de la excepción, y la excepción completa en `ILogger` |

- Escribe un `ProblemDetails` (`type`, `title`, `status`, `detail`) con `Content-Type: application/problem+json`.
- 401 y 403 no pasan por el middleware: los produce la autenticación y la autorización sin lanzar excepciones, así que siguen con el cuerpo vacío.
- **Alternativa descartada:** `UseExceptionHandler` con `IExceptionHandlerPathFeature`. Es equivalente, pero `architecture.md` ya define un middleware propio y este se prueba directamente con `DefaultHttpContext`.

### D8. Errores de formato en español
- **Mensajes de formato:** con `[ApiController]`, un JSON mal formado o un parámetro que no es un entero producen una respuesta 400 automática con mensajes del framework en inglés. Se sustituye `ApiBehaviorOptions.InvalidModelStateResponseFactory` por una fábrica que devuelve ProblemDetails con un `detail` en español:
  - Para el cuerpo (claves `$` o `$.campo`): "El cuerpo de la petición no es un JSON válido."
  - Para cada parámetro: "El parámetro '{nombre}' debe ser un número entero."
- **Campos obligatorios:** con `Nullable` activado, MVC trata las propiedades de referencia no anulables como `[Required]` y respondería en inglés antes de llegar al dominio. Por eso `CreateCategoryRequest` declara `string? Name` y `string? Description`, y BR-04 se valida solo en `Category.Create`.
- **Alternativa descartada:** `[Required]` y `[MaxLength]` con mensajes en español en el contrato. Duplicaría la regla en Api y en Domain.

### D9. Cadena de conexión obligatoria
- `DatabaseOptions` (Infrastructure/Configuration) se enlaza con `ConnectionStrings:Inventory`, que corresponde a la variable `ConnectionStrings__Inventory`.
- La registra una extensión de Api con `.Validate(...)` y `.ValidateOnStart()`, igual que `AuthOptions`, porque `ValidateOnStart` está en el framework compartido de ASP.NET Core y no en Infrastructure. Si falta, la API no arranca y el error nombra `ConnectionStrings__Inventory`.
- `SqlConnectionFactory` y `AddDbContext` leen la cadena desde `IOptions<DatabaseOptions>`.

### D10. Ruta con restricción `{id:guid}`
- `GET` y `DELETE` usan `api/categories/{id:guid}`. Un id que no es un GUID no coincide con ninguna ruta y responde 404 con el cuerpo vacío.
- Esto se acepta: el recurso no existe y no hace falta un mensaje. Los escenarios de 404 con mensaje usan un GUID válido que no existe.
- Se registra en `docs/assumptions.md`.

### D11. `init.sql` idempotente sobre bases existentes
El orden dentro del script es este:
1. `CREATE TABLE Categories` incluye `IsActive BIT NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT 1` y la collation de `Name`, para los clones limpios.
2. `IF COL_LENGTH('dbo.Categories', 'IsActive') IS NULL ALTER TABLE ... ADD IsActive ...`, para los volúmenes que ya existen.
3. Si `UX_Categories_Name` existe sin filtro (`sys.indexes.has_filter = 0`), se elimina.
4. Si la collation de `Name` no es `SQL_Latin1_General_CP1_CI_AS`, se ejecuta `ALTER COLUMN Name NVARCHAR(100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL`. Va después de eliminar el índice, porque SQL Server no permite cambiar la collation de una columna indexada.
5. Si `UX_Categories_Name` no existe, se crea filtrado `WHERE IsActive = 1`.

Ejecutarlo dos veces no cambia nada.

### D12. Eliminación de `check-read` y `check-write`
Se quitan las dos acciones de `AuthController`, el record `AccessCheckResponse` y sus pruebas. `whoami` y `[Authorize]` en el controlador se mantienen. Las pruebas de políticas (`ScopePoliciesTests`) no cambian.

### D13. Estrategia de pruebas
- **Pruebas unitarias con fakes escritos a mano:**
  - `Category.Create`
  - `PageRequest` y `PagedResult<T>`
  - los cuatro handlers, con `FakeCategoryWriteRepository`, `FakeCategoryReadRepository` y `FakeClock`
  - `CategoriesController`, con handlers reales sobre los fakes y comprobando los códigos, `Location` y los atributos `[Authorize(Policy = ...)]` por acción
  - `ExceptionHandlingMiddleware` y la fábrica de modelo inválido, con `DefaultHttpContext`
- **Verificación manual con Docker y `curl`:** los repositorios Dapper y EF Core, `init.sql` (clon limpio y volumen ya existente) y los escenarios de altas simultáneas, 401 y 403 de extremo a extremo. Se anota en `docs/bitacora.md`.
- Los nombres de las pruebas citan la regla (`BR04_...`, `BR05_...`, `BR18_...`).

## Risks / Trade-offs

- [Los repositorios no tienen pruebas automáticas contra SQL Server] → Las sentencias son pocas y se verifican a mano, una por escenario, con la lista de la tarea correspondiente. Si se aprueba un paquete de pruebas de integración más adelante, se añadirán en ese cambio.
- [`ALTER COLUMN` con collation sobre una tabla con datos] → Si un volumen antiguo tiene dos nombres que solo difieren en mayúsculas, crear el índice fallará. El riesgo es bajo, porque hasta ahora no había endpoint de categorías. Si ocurre, `dbinit` falla con el error de SQL Server y la solución es recrear el volumen (`docker compose down -v`).
- [El índice filtrado exige `QUOTED_IDENTIFIER ON`] → `Microsoft.Data.SqlClient` lo activa por defecto. Se anota en `docs/data-model.md` para quien escriba SQL a mano contra la tabla.
- [`ValidationException` coincide en nombre con `System.ComponentModel.DataAnnotations.ValidationException`] → El espacio de nombres `DataAnnotations` no está en los usings implícitos. Donde coincidan los dos (`AuthOptions`), se usa el nombre completo.
- [El orden por nombre depende de la collation] → Es la deseada (`CI_AS`): "adhesivos" y "Adhesivos" se ordenan juntos.
- [Una página fuera de rango ejecuta igualmente `COUNT`] → Es el coste normal de la paginación por desplazamiento. El índice filtrado cubre el filtro y el orden.

## Migration Plan

1. `docker compose up -d --build`: `dbinit` aplica `init.sql` (D11) sobre el volumen existente o sobre uno nuevo.
2. Los clientes que usaban `check-read` o `check-write` pasan a `GET /api/categories` o `POST /api/categories` (ver el REMOVED de `specs/api-auth`).
3. Rollback: volver a la versión anterior de la imagen. La columna `IsActive` y el índice filtrado no rompen esa versión, porque no los usa.
