# Arquitectura

Última actualización: 2026-11-07

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
| **Application** | Casos de uso | Comandos, consultas, handlers, puertos (interfaces), DTOs, validación de credenciales OAuth2 | Conocer Dapper, EF Core, HTTP o SQL |
| **Infrastructure** | Implementar los puertos | Repositorios Dapper (escritura), repositorios EF Core (lectura), emisión de tokens, lectura de variables de entorno | Contener reglas de negocio |
| **Api** | Entrada y salida HTTP | Controladores, contratos de petición, middleware de errores, Swagger, autenticación y autorización, raíz de composición | Contener lógica de negocio |

## CQRS
| Lado | Casos de uso | Puertos | Implementación |
|---|---|---|---|
| Comandos (escritura) | CreateCategory, DeleteCategory, CreateProduct, DeleteProduct, RegisterMovement;  CreateSupplier, LinkProductSupplier, UnlinkProductSupplier| `I…WriteRepository` | Dapper |
| Consultas (lectura) | ListProducts y las demás consultas | `I…ReadRepository` | EF Core con `AsNoTracking` |

Regla: un handler de comando nunca usa un puerto de lectura y un handler de consulta nunca usa uno de escritura. Si un comando necesita comprobar algo (por ejemplo, que el SKU no existe), el puerto de **escritura** expone esa comprobación.

Sin MediatR: los handlers son clases explícitas que se inyectan donde se usan.

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
├── docs/            (business-rules, data-model, architecture, api-contract, decisions, ai-usage, bitacora)
├── openspec/        (config.yaml, specs/, changes/)
├── .claude/         (skills/, agents/)
├── src/
│   ├── Inventory.Domain/          Entities/ Enums/ Exceptions/ Guard.cs
│   ├── Inventory.Application/     Abstractions/ Common/ Security/ Categories/ Products/ Movements/ [Suppliers/ Prices/]
│   ├── Inventory.Infrastructure/  Persistence/Write/ Persistence/Read/ Security/ Configuration/
│   └── Inventory.Api/             Controllers/ Contracts/ Middleware/ Authorization/ Program.cs
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
- OAuth2 `client_credentials`; la API emite JWT HS256 con los claims `sub` y `scope`.
- Políticas `InventoryRead` e `InventoryWrite` según el scope.
- Secretos y cadena de conexión solo por variables de entorno.
- Los errores de OAuth2 siguen el formato estándar (`invalid_client`, `invalid_scope`, `unsupported_grant_type`).

## Manejo de errores
| Excepción de dominio | HTTP |
|---|---|
| ValidationException | 400 |
| NotFoundException | 404 |
| ConflictException, InsufficientStockException | 409 |
| Error inesperado | 500, sin detalles internos |

Las respuestas de error usan `application/problem+json`, con el middleware propio.

## Despliegue local
`docker compose up -d --build` levanta `sqlserver` (con health check), luego `dbinit` (ejecuta `db/init.sql` una sola vez) y por último `api`.