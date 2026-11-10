# Arquitectura

Última actualización: 2026-11-09

## Resumen
API REST en .NET 6 con arquitectura limpia en cuatro capas y separación de comandos y consultas (CQRS). Las escrituras usan Dapper con SQL explícito y las lecturas usan EF Core sin seguimiento. La base es SQL Server en Docker, sin llaves foráneas.

## Capas y regla de dependencias
```
Api ─────────> Application <───────── Infrastructure
                   │
                   v
                Domain
```
Las dependencias de código apuntan solo hacia el dominio.

| Capa | Responsabilidad | Contiene | No puede |
|---|---|---|---|
| **Domain** | Reglas que dependen solo de la propia entidad | Entidades, `*Draft`, enums, excepciones, `Guard` | Depender de otra capa o de paquetes |
| **Application** | Casos de uso | Comandos, consultas, handlers, puertos (interfaces), DTOs, comprobación de scopes (`Security/ScopeSet`, `Security/InventoryScopes`) | Conocer Dapper, EF Core, HTTP o SQL |
| **Infrastructure** | Implementar los puertos | Repositorios Dapper (escritura), repositorios EF Core (lectura), lectura de variables de entorno | Contener reglas de negocio |
| **Api** | Entrada y salida HTTP | Controladores, contratos de petición, middleware de errores, Swagger, validación de JWT (`Configuration/AuthOptions`) y políticas de scope (`Authorization/ScopePolicies`), raíz de composición | Contener lógica de negocio |

## CQRS
| Lado | Casos de uso | Puertos | Implementación |
|---|---|---|---|
| Comandos (escritura) | CreateCategory, DeleteCategory (desactivación), CreateProduct, DeleteProduct, RegisterMovement;  CreateSupplier, LinkProductSupplier, UnlinkProductSupplier| `I…WriteRepository` | Dapper |
| Consultas (lectura) | GetCategoryById, ListCategories, ListProducts y las demás consultas | `I…ReadRepository` | EF Core con `AsNoTracking` |

Regla: un handler de comando nunca usa un puerto de lectura y un handler de consulta nunca usa uno de escritura. Si un comando necesita comprobar algo (por ejemplo, que el SKU no existe), el puerto de **escritura** expone esa comprobación. La prueba `Architecture/CqrsSeparationTests` lo comprueba por reflexión sobre el ensamblado de Application.

Sin MediatR: los handlers son clases explícitas que se inyectan donde se usan.

### Casos de uso de categorías (`add-categories`)
| Caso de uso | Tipo | Handler | Puerto | Implementación | Resultado |
|---|---|---|---|---|---|
| CreateCategory | Comando | `CreateCategoryHandler` (`CreateCategoryCommand`) | `ICategoryWriteRepository` + `IClock` | `CategoryWriteRepository` (Dapper) | `CategoryDto`; 400 (BR-04) o 409 si el nombre ya existe entre las activas |
| DeleteCategory | Comando | `DeleteCategoryHandler` (`DeleteCategoryCommand`) | `ICategoryWriteRepository` | `CategoryWriteRepository` (Dapper, `UPDATE` condicional atómico) | Desactivación (BR-05); 404 o 409 según `CategoryDeactivationResult` |
| GetCategoryById | Consulta | `GetCategoryByIdHandler` (`GetCategoryByIdQuery`) | `ICategoryReadRepository` | `CategoryReadRepository` (EF Core, `InventoryReadDbContext`) | `CategoryDto` de una categoría activa; 404 si no hay ninguna |
| ListCategories | Consulta | `ListCategoriesHandler` (`ListCategoriesQuery`) | `ICategoryReadRepository` | `CategoryReadRepository` (EF Core) | `PagedResult<CategoryDto>` con las activas, por `Name` y luego `Id` (BR-18) |

### Piezas comunes (`Application/Common`)
- `PageRequest`: `PageRequest.Create(page, pageSize)` aplica los valores por defecto (1 y 20) y valida los rangos de BR-18. Lanza `ValidationException` con un mensaje en español que nombra `page` o `pageSize`. La reutilizarán todos los listados.
- `PagedResult<T>`: `Items`, `Page`, `PageSize`, `TotalCount` y `TotalPages` (`ceil(TotalCount / PageSize)`, 0 sin elementos). Conserva `Page` aunque la página esté fuera de rango.
- `Abstractions/IClock`: da la fecha UTC actual a los comandos. `SystemClock` (Infrastructure) la implementa y las pruebas usan `FakeClock`.

## Dónde vive cada regla
| Tipo de regla | Dónde | Por qué |
|---|---|---|
| Datos de la propia entidad (nombre obligatorio, cantidad positiva) | Entidad de dominio (`Guard`) | No necesita la base |
| Dependen de la base (SKU único, categoría existente) | Handler con el puerto de escritura | Requiere consultar |
| Concurrencia (stock suficiente) | Sentencia SQL atómica | Solo la base ve el estado real en ese instante |
| Token y permisos | Api (autenticación y autorización) | Preocupación de borde |

## Estructura del repositorio
```
inventory-api/
├── AGENTS.md  CLAUDE.md  README.md
├── Directory.Build.props  global.json
├── docker-compose.yml  Dockerfile  .dockerignore  .env.example
├── db/init.sql
├── keycloak/        (inventory-realm.json: realm, cliente y scopes que se importan al arrancar)
├── docs/            (business-rules, data-model, architecture, api-contract, decisions, ai-usage, bitacora)
├── openspec/        (config.yaml, specs/, changes/)
├── .claude/         (skills/, agents/)
├── src/
│   ├── Inventory.Domain/          Entities/ Enums/ Exceptions/ Guard.cs
│   ├── Inventory.Application/     Abstractions/ Common/ Security/ Categories/ Products/ Movements/ [Suppliers/ Prices/]
│   ├── Inventory.Infrastructure/  Persistence/Write/ Persistence/Read/ Configuration/
│   └── Inventory.Api/             Controllers/ Contracts/ Middleware/ Authorization/ Configuration/ Program.cs
└── tests/Inventory.UnitTests/     Fakes/ y una clase de pruebas por handler
```
Una clase o enum por archivo.

## Flujo de una petición de escritura
```
HTTP POST /api/products/{id}/movements
  → Controller (valida el contrato, lee el cliente del token)
  → RegisterMovementHandler (crea la entidad; la entidad se valida)
  → IMovementWriteRepository (puerto)
  → MovementWriteRepository (Dapper: UPDATE condicional + INSERT, en una transacción)
  → respuesta 201 o excepción de dominio
  → ExceptionHandlingMiddleware (traduce la excepción a ProblemDetails: 400, 404, 409)
```

## Seguridad
- OAuth2 `client_credentials`. **Keycloak emite los tokens; la API solo los valida.** La API no tiene endpoint de token ni guarda credenciales de clientes.
- Validación con JwtBearer (`Api/Configuration/ConfigureJwtBearerOptions`):
  - Las claves de firma se obtienen de la metadata de `Auth__MetadataAddress` (`jwks_uri`, dentro de la red de Docker).
  - `iss` debe ser `Auth__Issuer` y `aud` debe contener `Auth__Audience`.
  - Se comprueba la caducidad con una tolerancia de reloj de 30 s.
  - Los claims se leen con su nombre original (`azp`, `scope`).
- Scopes: `ScopeSet` (`Application/Security/`) separa el claim `scope` y comprueba cada scope como elemento completo, distinguiendo mayúsculas. Las políticas `InventoryRead` e `InventoryWrite` (`Api/Authorization/ScopePolicies`) lo usan.
- Un token inválido o ausente recibe 401 y un token sin el scope exigido, 403, ambos con el cuerpo vacío (ver `api-contract.md`).
- Los errores del flujo OAuth2 (secreto incorrecto, scope inválido, etc.) los devuelve Keycloak, no la API.
- Secretos y cadena de conexión solo por variables de entorno.

## Manejo de errores
| Excepción de dominio | HTTP |
|---|---|
| ValidationException | 400 |
| NotFoundException | 404 |
| ConflictException, InsufficientStockException | 409 |
| Error inesperado | 500, sin detalles internos |

Las respuestas de error usan `application/problem+json`, con el middleware propio.

## Despliegue local
`docker compose up -d --build` levanta `sqlserver` (con health check), luego `dbinit` (ejecuta `db/init.sql` una sola vez) y por último `api`. En paralelo arranca `keycloak` (`http://localhost:8081`), que importa `keycloak/inventory-realm.json`; `api` espera a que el contenedor haya arrancado, no a que Keycloak esté listo.