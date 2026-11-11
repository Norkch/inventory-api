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
| Comandos (escritura) | CreateCategory, DeleteCategory (desactivación), CreateProduct, UpdateProduct, DeleteProduct (desactivación), RegisterMovement;  CreateSupplier, UpdateSupplier, DeactivateSupplier (desactivación), LinkProductSupplier (sin edición ni borrado) | `I…WriteRepository` | Dapper |
| Consultas (lectura) | GetCategoryById, ListCategories, GetProductById, ListProducts y las demás consultas | `I…ReadRepository` | EF Core con `AsNoTracking` |

Regla: un handler de comando nunca usa un puerto de lectura y un handler de consulta nunca usa uno de escritura. Si un comando necesita comprobar algo (por ejemplo, que el SKU no existe), el puerto de **escritura** expone esa comprobación. La prueba `Architecture/CqrsSeparationTests` lo comprueba por reflexión sobre el ensamblado de Application.

Sin MediatR: los handlers son clases explícitas que se inyectan donde se usan.

### Casos de uso de categorías (`add-categories`)
| Caso de uso | Tipo | Handler | Puerto | Implementación | Resultado |
|---|---|---|---|---|---|
| CreateCategory | Comando | `CreateCategoryHandler` (`CreateCategoryCommand`) | `ICategoryWriteRepository` + `IClock` | `CategoryWriteRepository` (Dapper) | `CategoryDto`; 400 (BR-04) o 409 si el nombre ya existe entre las activas |
| DeleteCategory | Comando | `DeleteCategoryHandler` (`DeleteCategoryCommand`) | `ICategoryWriteRepository` | `CategoryWriteRepository` (Dapper, `UPDATE` condicional atómico) | Desactivación (BR-05); 404 o 409 según `CategoryDeactivationResult` |
| GetCategoryById | Consulta | `GetCategoryByIdHandler` (`GetCategoryByIdQuery`) | `ICategoryReadRepository` | `CategoryReadRepository` (EF Core, `InventoryReadDbContext`) | `CategoryDto` de una categoría activa; 404 si no hay ninguna |
| ListCategories | Consulta | `ListCategoriesHandler` (`ListCategoriesQuery`) | `ICategoryReadRepository` | `CategoryReadRepository` (EF Core) | `PagedResult<CategoryDto>` con las activas, por `Name` y luego `Id` (BR-18) |

### Casos de uso de productos (`add-products`)
| Caso de uso | Tipo | Handler | Puerto | Implementación | Resultado |
|---|---|---|---|---|---|
| CreateProduct | Comando | `CreateProductHandler` (`CreateProductCommand`) | `IProductWriteRepository` + `IClock` | `ProductWriteRepository` (Dapper, `INSERT ... SELECT ... WHERE EXISTS` con `UPDLOCK, HOLDLOCK` sobre la categoría) | `ProductDto` con stock 0; 400 (BR-01, BR-02, BR-03) o 409 si el SKU ya existe, aunque el producto esté borrado |
| UpdateProduct | Comando | `UpdateProductHandler` (`UpdateProductCommand`) | `IProductWriteRepository` + `IClock` | `ProductWriteRepository` (Dapper, `UPDATE ... OUTPUT INSERTED.*` con el mismo bloqueo) | `ProductDto` actualizado; nunca cambia `Sku` ni `StockQuantity`; 400 o 404 |
| DeleteProduct | Comando | `DeleteProductHandler` (`DeleteProductCommand`) | `IProductWriteRepository` + `IClock` | `ProductWriteRepository` (Dapper, `UPDATE` condicional) | Desactivación (BR-06), conservando stock y movimientos; 404 si no está activo |
| GetProductById | Consulta | `GetProductByIdHandler` (`GetProductByIdQuery`) | `IProductReadRepository` | `ProductReadRepository` (EF Core, `join` con `Categories`) | `ProductDto` de un producto activo con `CategoryName`; 404 si no hay ninguno |
| ListProducts | Consulta | `ListProductsHandler` (`ListProductsQuery`) | `IProductReadRepository` | `ProductReadRepository` (EF Core, `EF.Functions.Like` con escape) | `PagedResult<ProductDto>` con los activos, filtrados por `categoryId` y `search`, por `Name` y luego `Id` (BR-18) |

El dominio valida los productos con `ProductRules`, que usan `Product.Create` (alta) y `ProductChanges.Create` (edición, sin SKU ni stock). En Api, `AddPersistence` registra lo que comparten todos los módulos (`IClock`, `SqlConnectionFactory` e `InventoryReadDbContext`), y `AddCategories`, `AddProducts` y `AddMovements` registran cada uno sus puertos y handlers.

### Casos de uso de movimientos (`add-inventory-movements`)
| Caso de uso | Tipo | Handler | Puerto | Implementación | Resultado |
|---|---|---|---|---|---|
| RegisterMovement | Comando | `RegisterMovementHandler` (`RegisterMovementCommand` con `MovementRegistrant`) | `IMovementWriteRepository` + `IClock` | `MovementWriteRepository` (Dapper, transacción con `UPDATE ... OUTPUT INSERTED.StockQuantity` condicional e `INSERT`) | `MovementDto` con `StockAfter`; el puerto devuelve `MovementRegistrationResult` y el handler lo traduce: 404 (`ProductNotFound`), 409 (`InsufficientStock`, `StockOverflow`) |
| ListMovements | Consulta | `ListMovementsHandler` (`ListMovementsQuery`) | `IMovementReadRepository` | `MovementReadRepository` (EF Core, `ExistsActiveProductAsync` y listado) | `PagedResult<MovementDto>` por `OccurredAtUtc` y luego `Id`, los dos descendentes (BR-18); 404 si el producto no está activo |

El dominio valida los movimientos con `MovementRules`, que usa `InventoryMovement.Create(MovementDraft)`. `MovementsController` no tiene acciones `PUT`, `PATCH` ni `DELETE` (BR-13), y responde el 201 sin `Location`.

### Casos de uso de proveedores (`add-suppliers`)
| Caso de uso | Tipo | Handler | Puerto | Implementación | Resultado |
|---|---|---|---|---|---|
| CreateSupplier | Comando | `CreateSupplierHandler` (`CreateSupplierCommand`) | `ISupplierWriteRepository` + `IClock` | `SupplierWriteRepository` (Dapper, comprobación previa del NIT e `INSERT`; 2601/2627 se traducen a 409) | `SupplierDto`; 400 (BR-08) o 409 si el NIT ya existe, aunque el proveedor esté desactivado |
| UpdateSupplier | Comando | `UpdateSupplierHandler` (`UpdateSupplierCommand`) | `ISupplierWriteRepository` + `IClock` | `SupplierWriteRepository` (Dapper, `UPDATE ... OUTPUT INSERTED.*` condicional a `IsActive = 1`) | `SupplierDto` actualizado; nunca cambia `Nit`; 400 o 404 |
| DeactivateSupplier | Comando | `DeactivateSupplierHandler` (`DeactivateSupplierCommand`) | `ISupplierWriteRepository` + `IClock` | `SupplierWriteRepository` (Dapper, `UPDATE` condicional con `UpdatedAtUtc`) | Desactivación (BR-09), conservando asociaciones y movimientos; 404 si no está activo |
| GetSupplierById | Consulta | `GetSupplierByIdHandler` (`GetSupplierByIdQuery`) | `ISupplierReadRepository` | `SupplierReadRepository` (EF Core) | `SupplierDto` de un proveedor activo; 404 si no hay ninguno |
| ListSuppliers | Consulta | `ListSuppliersHandler` (`ListSuppliersQuery`) | `ISupplierReadRepository` | `SupplierReadRepository` (EF Core) | `PagedResult<SupplierDto>` con los activos, por `Name` y luego `Id` (BR-18) |

El dominio valida los proveedores con `SupplierRules`, que usan `Supplier.Create(SupplierDraft)` (alta) y `SupplierChanges.Create(SupplierEdit)` (edición, sin NIT). `AddSuppliers` registra sus puertos y handlers.

### Casos de uso de asociaciones (`add-suppliers`)
| Caso de uso | Tipo | Handler | Puerto | Implementación | Resultado |
|---|---|---|---|---|---|
| LinkProductSupplier | Comando | `LinkProductSupplierHandler` (`LinkProductSupplierCommand`) | `IProductSupplierWriteRepository` + `IClock` | `ProductSupplierWriteRepository` (Dapper, un lote con `INSERT ... SELECT ... WHERE EXISTS` y `UPDLOCK, HOLDLOCK` sobre el proveedor, y el `SELECT` de la asociación con los nombres) | `ProductSupplierDto`; el puerto devuelve `ProductSupplierLinkResult` y el handler lo traduce: 404 (`ProductNotFound`), 400 (`SupplierNotAvailable`) y 409 (`AlreadyLinked`, 2601/2627) |
| GetProductSupplier | Consulta | `GetProductSupplierHandler` (`GetProductSupplierQuery`) | `IProductSupplierReadRepository` | `ProductSupplierReadRepository` (EF Core, `join` con `Products` y `Suppliers` activos) | `ProductSupplierDto` de una pareja visible; 404 si no lo es |
| ListProductSuppliers | Consulta | `ListProductSuppliersHandler` (`ListProductSuppliersQuery`) | `IProductSupplierReadRepository` | `ProductSupplierReadRepository` (EF Core, `ExistsActiveProductAsync` y listado) | `PagedResult<ProductSupplierDto>` por `SupplierName` y luego `SupplierId` (BR-18); 404 si el producto no está activo |
| ListSupplierProducts | Consulta | `ListSupplierProductsHandler` (`ListSupplierProductsQuery`) | `IProductSupplierReadRepository` | `ProductSupplierReadRepository` (EF Core, `ExistsActiveSupplierAsync` y listado) | `PagedResult<ProductSupplierDto>` por `ProductName` y luego `ProductId` (BR-18); 404 si el proveedor no está activo |

El dominio valida la asociación con `ProductSupplierRules`, que usa `ProductSupplier.Create(ProductSupplierDraft)`. `ProductSuppliersController` no tiene acciones `PUT`, `PATCH` ni `DELETE`, y declara `GET /api/suppliers/{supplierId}/products` con una ruta absoluta. `AddProductSuppliers` registra sus puertos y handlers.

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
│   ├── Inventory.Application/     Abstractions/ Common/ Security/ Categories/ Products/ Movements/ Suppliers/
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
  - Los claims se leen con su nombre original (`azp`, `scope`, `sub`).
  - El token debe traer un claim `sub` no vacío. `OnTokenValidated` lo comprueba con `SubjectClaimRequirement` y, si falta, rechaza el token con 401 en todos los endpoints. `sub` es el autor (`RegisteredBy`) de los movimientos.
- Scopes: `ScopeSet` (`Application/Security/`) separa el claim `scope` y comprueba cada scope como elemento completo, distinguiendo mayúsculas. Las políticas `InventoryRead` e `InventoryWrite` (`Api/Authorization/ScopePolicies`) lo usan.
- Un token inválido o ausente recibe 401 y un token sin el scope exigido, 403, ambos con el cuerpo vacío (ver `api-contract.md`).
- Los errores del flujo OAuth2 (secreto incorrecto, scope inválido, etc.) los devuelve Keycloak, no la API.
- Secretos y cadena de conexión solo por variables de entorno.

## Manejo de errores
| Excepción de dominio | HTTP |
|---|---|
| ValidationException | 400 |
| NotFoundException | 404 |
| ConflictException e InsufficientStockException (hereda de ConflictException) | 409 |
| Error inesperado | 500, sin detalles internos |

Las respuestas de error usan `application/problem+json`, con el middleware propio.

## Despliegue local
`docker compose up -d --build` levanta `sqlserver` (con health check), luego `dbinit` (ejecuta `db/init.sql` una sola vez) y por último `api`. En paralelo arranca `keycloak` (`http://localhost:8081`), que importa `keycloak/inventory-realm.json`; `api` espera a que el contenedor haya arrancado, no a que Keycloak esté listo.