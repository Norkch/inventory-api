# Design

## Context

- `db/init.sql` ya crea `dbo.InventoryMovements` (con `SupplierId NULL`, los `CHECK` de tipo, cantidad y proveedor) e `IX_InventoryMovements_ProductId_OccurredAtUtc (ProductId, OccurredAtUtc DESC)`. `Products.StockQuantity` tiene `CHECK (StockQuantity >= 0)`. No hace falta tocar el esquema.
- Se reutilizan sin cambios `Guard`, las excepciones de dominio, `PageRequest`, `PagedResult<T>`, `IClock`, `SqlConnectionFactory`, `InventoryReadDbContext`, `ExceptionHandlingMiddleware`, `InvalidModelStateResponseFactory`, las políticas de scope y `CqrsSeparationTests`.
- `DeleteCategory` ya resuelve un caso parecido: un `UPDATE` condicional que puede fallar por dos motivos devuelve el enum `CategoryDeactivationResult` y el handler lo traduce a excepciones.
- `InvalidModelStateResponseFactory` responde cualquier error de deserialización del cuerpo con "El cuerpo de la petición no es un JSON válido.". Por eso un enum en el contrato daría un mensaje engañoso.
- No hay `JsonStringEnumConverter` configurado. `ConfigureJwtBearerOptions` usa `MapInboundClaims = false`, así que el claim llega como `sub`. El realm emite `sub` mediante el client scope `basic`, pero JwtBearer no lo exige.
- No hay `TestHost` ni proveedor en memoria: los repositorios y la concurrencia se verifican a mano con Docker, como en los cambios anteriores.
- Context7 no tiene documentación específica de ASP.NET Core 6. La de `OnTokenValidated` y `context.Fail` procede de la documentación de las versiones 3 a 7; se confirma con una prueba antes de dar la tarea por cerrada.

Motivación en `proposal.md`. Requisitos en `specs/inventory-movements` y `specs/api-auth`.

## Goals / Non-Goals

**Goals:**
- BR-12, BR-14 y BR-15 garantizados por la base de datos en una sola transacción, sin niveles de aislamiento especiales.
- Que el handler decida entre 404, 409 por stock y 409 por desbordamiento, y que esa decisión se pruebe con fakes.
- Mantener la separación CQRS: el handler de registro no usa el puerto de lectura y el de listado no usa el de escritura.

**Non-Goals:**
- Pruebas automáticas contra SQL Server del repositorio y de la concurrencia.
- Proveedor en el movimiento (BR-16). `SupplierId` se inserta siempre como `NULL`.
- Garantizar que el orden por `occurredAtUtc` coincida con el orden en que se aplicaron movimientos simultáneos del mismo producto (ver Riesgos).

## Decisions

### D1. Dónde vive cada pieza

```
          Comando (POST)                                   Consulta (GET)
               |                                                 |
   RegisterMovementHandler                           ListMovementsHandler
               |                                                 |
   IMovementWriteRepository                          IMovementReadRepository
               |                                                 |
   MovementWriteRepository (Dapper,                  MovementReadRepository (EF Core,
   transacción UPDATE + INSERT)                      AsNoTracking)
               \                                                /
                +--> dbo.Products, dbo.InventoryMovements <----+
```

```
Domain/          Entities/{InventoryMovement, MovementType}  MovementRules
                 Exceptions/InsufficientStockException
Application/     Movements/  RegisterMovementCommand + RegisterMovementHandler --> IMovementWriteRepository + IClock
                             ListMovementsQuery      + ListMovementsHandler    --> IMovementReadRepository
                             MovementRegistrationStatus, MovementRegistrationResult
                             MovementListCriteria, MovementDto, MovementMessages, MovementRegistrant
Infrastructure/  Persistence/Write/MovementWriteRepository
                 Persistence/Read/{MovementRecord, MovementReadRepository}
Api/             Controllers/MovementsController
                 Contracts/RegisterMovementRequest  (la paginación reutiliza PaginationRequest)
                 Configuration/MovementsServiceCollectionExtensions
```

- Una clase, record o enum por archivo. Los handlers reciben el comando o la consulta completos.
- `MovementsController` usa la ruta `api/products/{productId:guid}/movements` y no define acciones `PUT`, `PATCH` ni `DELETE` (BR-13).
- `MovementRegistrant` es el objeto que lleva el `sub` del token desde el controlador hasta el comando (regla 7: objetos, no valores sueltos).

### D2. Validación en el dominio y contrato de entrada
- `RegisterMovementRequest` declara `string? Type`, `int? Quantity` y `string? Reason`, como los contratos de productos: la regla vive solo en el dominio. System.Text.Json ignora `supplierId`, `stockAfter`, `registeredBy` y cualquier otro campo desconocido.
- `MovementRules` (Domain) tiene `ReasonMaxLength = 250`, `RegisteredByMaxLength = 100` y los métodos que validan con `Guard` y mensajes en español:
  - `ValidateType(string?)`: recorta, y compara con `"Entry"` y `"Exit"` con `StringComparison.OrdinalIgnoreCase`. **No usa `Enum.TryParse`**, que aceptaría `"1"` o `"3"` y devolvería un valor fuera del enum.
  - `ValidateQuantity(int?)`: obligatoria y mayor que 0.
  - `ValidateReason(string?)`: igual que `ProductRules.ValidateDescription`, con máximo 250.
  - `ValidateRegisteredBy(string?)`: obligatorio y con máximo 100. Es una última barrera: la autenticación (D6) ya garantiza el `sub`.
- `MovementType` es un enum con `Entry = 1` y `Exit = 2`, que coinciden con el `CHECK` de la tabla.
- `InventoryMovement.Create(...)` valida todos los campos y fija `Id` y `OccurredAtUtc` desde `IClock`. Expone el `delta` con signo (`+quantity` en una entrada, `-quantity` en una salida) que usa el repositorio. `StockAfter` lo asigna el resultado del repositorio, no el cliente.
- `MovementDto.Type` es `string` (`"Entry"` o `"Exit"`), para no depender de la serialización global de enums.

### D3. Registro: una transacción con `UPDATE` condicional
`MovementWriteRepository.RegisterAsync(InventoryMovement movement, ...)` abre una conexión y una transacción explícita (`BeginTransaction` en la conexión, pasada a cada `CommandDefinition`):

```sql
UPDATE dbo.Products
   SET StockQuantity = StockQuantity + @Delta
OUTPUT INSERTED.StockQuantity
 WHERE Id = @ProductId AND IsActive = 1
   AND StockQuantity >= -@Delta;
```

La condición se escribe `StockQuantity >= -@Delta` y no `StockQuantity + @Delta >= 0`. Las dos son equivalentes, pero la segunda suma dentro del `WHERE`, y SQL Server no garantiza en qué orden evalúa los predicados. Con la suma, una entrada sobre un producto **borrado** con stock alto podría lanzar el error 8115 y responder 409 en lugar de 404. Así, el único desbordamiento posible está en el `SET`, que solo se ejecuta sobre un producto activo.

```
1 fila  --> StockAfter = INSERTED.StockQuantity
            INSERT INTO dbo.InventoryMovements (..., SupplierId = NULL, StockAfter = @StockAfter, ...)
            COMMIT --> Registered(movement con StockAfter)
0 filas --> SELECT IsActive FROM dbo.Products WHERE Id = @ProductId   (misma transacción)
            ninguna fila o IsActive = 0 --> ROLLBACK --> ProductNotFound
            activo                      --> ROLLBACK --> InsufficientStock
SqlException 8115 (desbordamiento aritmético) --> ROLLBACK --> StockOverflow
```

- **Por qué es atómico (BR-15):** el `UPDATE` toma un bloqueo exclusivo sobre la fila del producto hasta el `COMMIT`. Una segunda salida simultánea espera ese bloqueo y vuelve a evaluar la condición `StockQuantity >= -@Delta` con el stock ya descontado, así que la que pierde afecta 0 filas. El `CHECK (StockQuantity >= 0)` queda como última barrera.
- **Por qué la segunda lectura no tiene carrera:** si el `UPDATE` afectó 0 filas porque el producto estaba activo pero sin stock, un borrado posterior solo cambiaría el diagnóstico de 409 a 404, y los dos son correctos para ese instante. No hace falta bloquear la fila en la lectura.
- **Borrado simultáneo:** `DeleteProduct` hace un `UPDATE` de la misma fila, así que se serializa con el registro. O el movimiento se aplica antes del borrado, o ve `IsActive = 0` y responde 404.
- **BR-14:** si el `INSERT` falla, la excepción provoca `ROLLBACK` (al liberar la transacción sin `Commit`) y el stock vuelve a su valor. El middleware responde 500.
- **No toca `UpdatedAtUtc`:** el `UPDATE` solo cambia `StockQuantity`.
- **Alternativas descartadas:**
  - Leer el stock en el handler y escribir después: deja la carrera de BR-15.
  - `SERIALIZABLE` o `UPDLOCK` en una lectura previa: hacen lo mismo con más código.
  - Comprobar el desbordamiento en el `WHERE` (`StockQuantity <= 2147483647 - @Delta`): evita la excepción, pero mezcla tres motivos de 0 filas en una sola rama. Se prefiere traducir el error 8115, como ya se traduce el 2601/2627 del SKU.

### D4. Resultado tipado del puerto de escritura
- `MovementRegistrationStatus` (enum): `Registered`, `ProductNotFound`, `InsufficientStock`, `StockOverflow`.
- `MovementRegistrationResult` (record): `Status` e `InventoryMovement?` con `StockAfter` cuando el estado es `Registered`.
- `RegisterMovementHandler`:

  ```
  InventoryMovement.Create(command, registrant, clock)  --> ValidationException            (400)
  writeRepo.RegisterAsync(movement)                     --> ProductNotFound:  NotFoundException          (404)
                                                            InsufficientStock: InsufficientStockException (409)
                                                            StockOverflow:     ConflictException          (409)
  return MovementDto.From(result.Movement)
  ```

  El orden 400, 404, 409 que pide la spec sale de este flujo: el dominio valida antes de llamar al repositorio, y el repositorio comprueba el producto antes que el stock.
- `InsufficientStockException` hereda de `ConflictException`, así que el middleware ya la traduce a 409 sin cambios. Es la que nombra `docs/architecture.md`.
- **Alternativa descartada:** que el repositorio lance directamente las excepciones de dominio. Movería a Infrastructure la decisión de negocio entre BR-11 y BR-12, y un fake que lanza la excepción haría pasar la prueba del handler aunque el handler no hiciera nada.

### D5. Listado
- `ListMovementsHandler`:

  ```
  PageRequest.Create(query.Page, query.PageSize)                    --> ValidationException (400)
  readRepo.ExistsActiveProductAsync(productId)                      --> false: NotFoundException (404)
  readRepo.ListAsync(new MovementListCriteria(productId, pageRequest))
  ```

- Hay dos consultas porque una lista vacía no distingue entre "producto sin movimientos" (200) y "producto inexistente o borrado" (404). Entre las dos no hay bloqueo: en el peor caso se ve el historial de un producto que se está borrando en ese mismo instante.
- `MovementReadRepository.ListAsync` ordena con `OrderByDescending(OccurredAtUtc).ThenByDescending(Id)` y pagina con `Skip`/`Take`, como `ProductReadRepository`. El desempate por `Id` es arbitrario pero estable entre consultas.
- `MovementRecord` se mapea en `InventoryReadDbContext` como `InventoryMovements` (`dbo`) con `MovementType` como `byte`. El repositorio lo convierte a `MovementDto`.
- `ExistsActiveProductAsync` vive en el puerto de lectura de movimientos, no en `IProductReadRepository`, para que el handler dependa de un solo puerto.

### D6. Token sin `sub`: 401 en la autenticación
- `ConfigureJwtBearerOptions` añade `options.Events.OnTokenValidated`. Si el principal no tiene un claim `sub` con un valor que no esté en blanco, llama a `context.Fail(...)`. JwtBearer responde entonces 401 con el cuerpo vacío, igual que con cualquier otro token inválido.
- La comprobación es una clase pequeña que se puede probar sin servidor (por ejemplo `SubjectClaimRequirement`, con un método que recibe el `ClaimsPrincipal`). El evento solo la llama.
- Se aplica a todos los endpoints bajo `/api`, incluido `whoami`. Es el delta de `api-auth`.
- `MovementsController` lee `User.FindFirst("sub")` y construye `MovementRegistrant`. Gracias a D6, el valor siempre existe.
- **Alternativas descartadas:**
  - Política de autorización que exige `sub`: respondería 403, que significa "sé quién eres pero no puedes", justo lo contrario.
  - Validarlo en el handler: daría 400, y el cuerpo de la petición no tiene ningún error.
  - Un valor por defecto (`azp`, `"unknown"`): ensuciaría un libro que no se puede corregir.

### D7. Respuesta 201 sin `Location`
- `MovementsController.Register` responde con `StatusCode(StatusCodes.Status201Created, dto)`, sin cabecera `Location`, porque no existe un recurso individual de movimiento. Antes de implementarlo hay que confirmar en Context7 o con una prueba que `ControllerBase.Created` no admite una URI nula en ASP.NET Core 6.
- `occurredAtUtc` se escribe con sufijo `Z` en el 201, porque sale de `IClock`, y sin sufijo en el `GET`, porque sale de `datetime2`. Es el mismo comportamiento documentado de `createdAtUtc`.
- **Alternativas descartadas:** `Location` a la colección (no identifica el recurso creado), `Location` a una ruta que no existe (404) y añadir un `GET` por id (no lo pide la propuesta).

## Risks / Trade-offs

- [El orden por `occurredAtUtc` puede no coincidir con el orden en que se aplicaron dos movimientos simultáneos del mismo producto, porque la hora se toma en el handler antes de conseguir el bloqueo] → Se documenta como supuesto en `docs/assumptions.md`. La ventana es de milisegundos, y el saldo autoritativo sigue siendo `StockAfter` de cada movimiento y `Products.StockQuantity`.
- [El 401 por falta de `sub` afecta a todos los endpoints; un cliente mal configurado en Keycloak dejaría de funcionar también en las lecturas] → Con el realm actual todos los tokens llevan `sub`. Se añade una nota en `docs/assumptions.md` y una comprobación manual con un token real.
- [Un número JSON en `type` o un decimal en `quantity` reciben el mensaje genérico de JSON no válido, que no nombra el campo] → Es el comportamiento actual de toda la API y sigue siendo un 400 correcto. Mejorarlo es un cambio aparte en `InvalidModelStateResponseFactory`.
- [La concurrencia (BR-15) y la atomicidad (BR-14) no tienen prueba automática] → Se verifican a mano contra SQL Server en Docker, ejecutando dos salidas a la vez y forzando un fallo del `INSERT`, y el resultado se anota en `docs/bitacora.md`, como en `add-products`.
- [`sub` es el UUID del usuario de servicio de Keycloak, no el id legible del cliente] → Es lo que pide la propuesta. Se registra como supuesto. Si se quiere el nombre del cliente habrá que decidirlo en otro cambio, porque los movimientos ya registrados no se pueden corregir.

## Migration Plan

- Sin cambios de esquema ni variables nuevas. Se despliega con `docker compose up -d --build`.
- Antes de desplegar se comprueba que los tokens del realm actual traen `sub`, decodificando uno, para que D6 no deje fuera a los clientes existentes.
- Rollback: volver a la imagen anterior de la API. Los movimientos ya registrados se conservan, y el stock sigue cuadrando con ellos.
