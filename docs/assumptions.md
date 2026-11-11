# Supuestos

Supuestos tomados durante la implementación cuando la especificación no fija un detalle. Cada entrada indica el cambio de OpenSpec que la originó.

## add-keycloak-oauth (2026-11-08)

### Imagen de Keycloak: `quay.io/keycloak/keycloak:26.8.1`

- Se fija el tag exacto `26.8.1` (publicado el 2026-10-31), no `26` ni `latest`, para que la importación del realm y la sustitución de placeholders no cambien entre versiones menores.
- Existe `26.8.2`, pero la documentación consultada en Context7 (`/keycloak/keycloak/26.8.1`) corresponde a `26.8.1`. Se usa la versión cuya documentación se verificó.
- Comportamientos confirmados en la documentación de `26.8.1`:
  - `start-dev --import-realm` importa al arrancar los archivos `.json` de `/opt/keycloak/data/import` (sin subdirectorios).
  - Los archivos del realm admiten placeholders `${VARIABLE}` que se resuelven con variables de entorno al importar. Así llega `INVENTORY_CLIENT_SECRET` al cliente `inventory-client`.
  - Hostname v2: `KC_HOSTNAME_BACKCHANNEL_DYNAMIC=true` exige que `KC_HOSTNAME` sea una URL completa (`http://localhost:8081`). El frontchannel y el `iss` usan esa URL, y las URL de backchannel se resuelven desde la petición (`keycloak:8080` dentro de la red de Docker).
- Si el realm ya existe en el servidor, la importación se omite. Como Keycloak no tiene volumen de datos, recrear el contenedor (`docker compose up --force-recreate` o `down` + `up`) vuelve a importar `inventory-realm.json`. Un `docker compose restart` conserva el realm que ya había.

### `ClockSkew` de 30 segundos en la validación de JWT

- La API valida el tiempo de vida de los tokens con una tolerancia de reloj de 30 s, no los 5 minutos que JwtBearer usa por defecto.
- Motivo: los tokens de Keycloak duran 5 minutos por defecto. Con la tolerancia por defecto, un token caducado se aceptaría hasta unos 10 minutos después de emitirse. 30 s cubren el desfase normal entre contenedores sin ocultar los tokens caducados.
- Es una constante del código, no una variable de entorno: no depende del despliegue.

### Client scope `basic` definido en `inventory-realm.json`

- El realm declara su propio array `clientScopes`. Se asume que, en ese caso, la importación no crea los client scopes integrados de Keycloak (`basic`, `profile`, `email`, `roles`, etc.).
- Por eso `basic` se define de forma explícita con los mismos mappers que trae Keycloak 26 (`sub` y `auth_time`), y es el único default client scope del realm y de `inventory-client` (D3).
- Las claves de configuración del mapper de audiencia (`included.custom.audience`, `access.token.claim`) no aparecen en la documentación consultada. Se toman del formato de exportación de Keycloak y se comprueban decodificando un token en la tarea 3.4.

### Error ante un secreto incorrecto: `unauthorized_client`

- Keycloak `26.8.1` responde 401 con `{"error":"unauthorized_client","error_description":"Invalid client or Invalid client credentials"}` cuando `inventory-client` pide un token con un secreto incorrecto. No devuelve `invalid_client`, que es lo que sugiere RFC 6749 §5.2.
- Se ajustó el escenario "Secreto incorrecto" de `specs/dev-identity-provider` para aceptar el comportamiento real del servidor de identidad: 401, `unauthorized_client` y ningún token emitido. Lo importante es que el cliente se rechaza; el código de error lo decide Keycloak, no la API.
- Si se cambia la versión de la imagen, se debe repetir la comprobación de la tarea 3.4.

### `AuthOptions` en `Inventory.Api/Configuration/`

- `design.md` (D6) no fija la ubicación de `AuthOptions`. Se coloca en `src/Inventory.Api/Configuration/AuthOptions.cs` porque solo la usan la validación JwtBearer y Swagger, que viven en Api.
- `ValidateDataAnnotations()` está en el framework compartido de ASP.NET Core, que Api ya tiene. Ponerla en Infrastructure exigiría añadir el paquete `Microsoft.Extensions.Options.DataAnnotations`.

### Pruebas automáticas de autenticación sin servidor de pruebas

- La revisión de Clean Code de la tarea 8.2 pidió pruebas para los escenarios de `specs/api-auth`. Se escriben con xUnit y fakes, sin paquetes nuevos. `Inventory.UnitTests` referencia ahora `Inventory.Api`.
- Los 401 por firma, caducidad (con la tolerancia de 30 s), emisor y audiencia se prueban validando tokens firmados en la prueba con los `TokenValidationParameters` que produce `ConfigureJwtBearerOptions`. La clave de prueba ocupa el lugar de las claves publicadas por Keycloak.
- Las políticas `InventoryRead` e `InventoryWrite` se prueban con el `IAuthorizationService` real. `AuthController` se prueba directamente, con un `ClaimsPrincipal` falso.
- Que 401 y 403 lleguen con el cuerpo vacío, que el pipeline HTTP completo los devuelva y que la API arranque y atienda peticiones con la configuración completa (escenario "Configuración completa"; las pruebas solo cubren el enlace de las cuatro opciones), solo se puede probar con un servidor de pruebas (`Microsoft.AspNetCore.TestHost`), que es un paquete nuevo. Queda como verificación manual, ya anotada en `docs/bitacora.md` (tarea 8.1).
- `check-read` y `check-write` devuelven ahora el record `AccessCheckResponse` en lugar de un objeto anónimo. El JSON no cambia (`{"message": "..."}`).

### Id del cliente en `INVENTORY_CLIENT_ID`, con valor por defecto

- El id del cliente que Swagger UI deja rellenado se lee de `Auth__SwaggerClientId` (`AuthOptions.SwaggerClientId`, `[Required]`), a raíz del hallazgo de la regla 5 en la revisión de la tarea 8.2. Si falta, la API no arranca y el error nombra `SwaggerClientId`.
- Para poder cambiarlo sin editar archivos versionados, el valor viene de `INVENTORY_CLIENT_ID` en `.env`. Esa misma variable se usa como `clientId` del realm (`${INVENTORY_CLIENT_ID}`) y como `Auth__SwaggerClientId`, para que no puedan quedar distintos.
- A diferencia de los secretos, lleva valor por defecto en compose (`${INVENTORY_CLIENT_ID:-inventory-client}`): no es un secreto, porque Swagger UI lo envía desde el navegador, y un `.env` antiguo sin la variable sigue funcionando.
- El valor por defecto no se pone en el realm (`${INVENTORY_CLIENT_ID:inventory-client}`), porque la documentación de Keycloak 26.8.1 solo describe esa sintaxis para `keycloak.conf`. Compose siempre pasa la variable al contenedor.
- Cambiar el id exige recrear Keycloak, porque la importación se omite si el realm ya existe.
- Por la misma razón, la audiencia sale de `INVENTORY_API_AUDIENCE` (por defecto `inventory-api`). Esa variable alimenta el mapper de audiencia del realm y `Auth__Audience`, y así nunca difieren la audiencia emitida y la validada. Se añadió tras la revisión final de la tarea 8.2.

### Hallazgos menores aceptados en la revisión de Clean Code de la tarea 8.2

La revisión de Clean Code de la tarea 8.2 dejó tres hallazgos menores. Se aceptan tal como están por estos motivos:

- **`RequireHttpsMetadata = false` fijo en `ConfigureJwtBearerOptions`.**
  - El único despliegue que existe es el de Docker en local. Ahí la API lee la metadata de `http://keycloak:8080`, sin TLS, como pide la tarea 4.2 (D5).
  - No es un secreto ni cambia entre los entornos que existen hoy.
  - Si se añade un entorno con HTTPS, se pasará a `AuthOptions` (`Auth__RequireHttpsMetadata`, con `true` por defecto) en ese cambio.
- **Líneas en blanco entre las instrucciones de nivel superior de `Program.cs`.**
  - La regla 2 se aplica a los métodos escritos a mano. `Program.cs` es la raíz de composición y no tiene métodos.
  - Las líneas en blanco separan el registro de servicios, la construcción de la aplicación y el pipeline.
- **`"scope"` y `"azp"` repetidos en `ScopePolicies` y `AuthController`.**
  - No es una de las siete reglas. Son dos constantes privadas, cada una en la clase que la usa.
  - Se moverán a una clase `TokenClaimTypes` cuando un tercer consumidor las necesite, por ejemplo los controladores del entregable 3.

## add-categories (2026-11-09)

### `Category.Create` recibe el nombre, la descripción y la fecha por separado

- La regla 7 pide pasar objetos en lugar de propiedades sueltas. `Category.Create(string? name, string? description, DateTime createdAtUtc)` recibe tres valores porque son los datos con los que nace la entidad, y Domain no puede conocer `CreateCategoryCommand` (las dependencias apuntan hacia adentro).
- No se crea un objeto intermedio en Domain solo para agrupar esos tres valores. Si el revisor de Clean Code lo marca, se reconsiderará en la tarea 4.2, que es la que llama a `Create`.
- `PageRequest.Create(int? page, int? pageSize)` (tarea 3.1) también recibe dos valores sueltos, pero por otro motivo: `PageRequest` vive en `Application/Common` y es genérico, así que no debe depender de `ListCategoriesQuery` ni de ninguna consulta concreta. Los productos, los proveedores y los movimientos lo reutilizarán con sus propias consultas. Lo llama `ListCategoriesHandler` (tarea 4.5). Redacción corregida tras la segunda revisión de Clean Code de la tarea 10.2.

### Tratamiento de la descripción de una categoría

- `specs/categories` exige que una descripción vacía o solo con espacios se guarde como `null` y que se recorte antes de comprobar el máximo de 500 caracteres.
- Se asume que una descripción de exactamente 500 caracteres es válida (el límite incluido), igual que los 100 del nombre.
- No se envía `description`, se envía `null` o se envía vacía o solo con espacios: en los cuatro casos se guarda y se devuelve `null`. La API no distingue entre "sin descripción" y "descripción vacía".
- La descripción no tiene que ser única ni se valida su contenido, solo su longitud después de recortarla.
- El recorte solo quita los espacios de los extremos (`string.Trim()`); los espacios intermedios se conservan tal cual. Lo mismo se aplica al nombre.
- Documentado en `docs/api-contract.md`, sección "Categorías" (tarea 8.3).

### Id que no es un GUID: 404 sin cuerpo (D10)

- `GET` y `DELETE` usan la ruta `api/categories/{id:guid}`. Un id que no tiene formato de GUID (por ejemplo `abc`) no coincide con ninguna ruta, y ASP.NET Core responde 404 con el cuerpo vacío, sin ProblemDetails ni mensaje en español.
- Se acepta porque el recurso no existe y no hace falta un mensaje. No se responde 400: el id no es un parámetro mal escrito de un recurso que existe, sino una ruta que no existe.
- Los escenarios de `specs/categories` con un 404 con mensaje en español usan siempre un GUID válido que no corresponde a ninguna categoría activa.
- Documentado en `docs/api-contract.md`, sección "Id que no es un GUID" (tarea 8.3).

### `createdAtUtc` sin sufijo `Z` en las lecturas

- Observado al documentar la tarea 8.3, sin cambiar el código. La respuesta 201 serializa `createdAtUtc` desde `DateTime.UtcNow` (`DateTimeKind.Utc`), con el sufijo `Z`. Las lecturas con EF Core devuelven la columna `datetime2` como `DateTimeKind.Unspecified`, y `System.Text.Json` la escribe sin sufijo.
- El valor es UTC en los dos casos, como exige `specs/categories`; solo cambia el formato.
- Queda pendiente de decidir si se unifica (por ejemplo, con un `ValueConverter` en `InventoryReadDbContext` que marque las fechas como UTC). Se comprobará en la tarea 10.1.
- Confirmado en la tarea 10.1: `POST` devuelve `"createdAtUtc":"2026-11-10T03:59:15.0566126Z"` y el `GET` de la misma categoría, `"createdAtUtc":"2026-11-10T03:59:15.0566126"`.

### JwtBearer acepta también el emisor de la metadata, no solo `Auth__Issuer`

- Hallazgo de la tarea 10.1. Con `api` recreada con `Auth__Issuer=http://otro-emisor:8081/realms/inventory`, un token con `iss=http://localhost:8081/realms/inventory` respondió 200, no 401. El handler de JwtBearer acepta, además de `ValidIssuer`, el `issuer` del documento de metadata (`Auth__MetadataAddress`).
- Context7 solo tiene la documentación de ASP.NET Core 7 en adelante y de la rama `main` (otras versiones mayores), así que no pudo confirmar el comportamiento de la 6. El comportamiento se comprobó en vivo.
- **Se acepta por ahora:** la metadata sale del mismo realm `inventory`, así que en el despliegue actual los dos valores coinciden (`http://localhost:8081/realms/inventory`). Un token de otro emisor real se rechaza: un token del realm `master` respondió 401 con `The issuer 'http://localhost:8081/realms/master' is invalid`.
- **Consecuencia:** `docs/api-contract.md` dice "`iss` es igual a `Auth__Issuer`", y eso solo se cumple mientras `Auth__Issuer` y el emisor de la metadata coincidan. Si se configura mal `Auth__Issuer`, nada lo avisa.
- **Pendiente:** corregirlo en un cambio aparte (por ejemplo `fix-strict-issuer-validation`), con un validador de emisor que solo acepte `Auth__Issuer` y su prueba primero. Pertenece a `api-auth` (`add-keycloak-oauth`), no a `add-categories`.

### APIs de EF Core 6 y Dapper 2.1 confirmadas para la persistencia (tarea 6.1)

- **Aviso sobre la versión de EF Core:** Context7 (`/dotnet/entityframework.docs`) no separa la documentación por versión y sirve la rama `main`, que corresponde a una versión mayor posterior a la 6. Se ignoró todo lo que es propio de versiones posteriores (por ejemplo, `UseSqlServer(null)` de EF7 y los ejemplos de EF9). Cada API que se usará se comprobó además en los archivos XML de los paquetes `6.0.36` de la caché de NuGet (`microsoft.entityframeworkcore`, `.relational` y `.sqlserver`, `lib/net6.0`).
- **EF Core 6.0.36 (confirmadas en la documentación y en el paquete):**
  - `services.AddDbContext<TContext>((IServiceProvider sp, DbContextOptionsBuilder options) => ...)`: permite leer `IOptions<DatabaseOptions>` desde `sp` al configurar el contexto (D9).
  - `options.UseSqlServer(string connectionString)`.
  - `options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)`: todas las consultas del contexto van sin seguimiento por defecto (D5).
  - `queryable.AsNoTracking()`, que se llama además de forma explícita en cada consulta (D5).
  - `CountAsync(CancellationToken)`, `Skip`, `Take` y `ToListAsync` para la paginación.
  - `modelBuilder.Entity<CategoryRecord>().ToTable("Categories", "dbo")` y `HasKey(...)` en `OnModelCreating`, para mapear `CategoryRecord` a la tabla que crea `init.sql` sin migraciones.
- **Dapper:** Context7 tiene la documentación de `2.1.66` (`/dapperlib/dapper/2.1.66`), que es de la misma versión mayor y menor que el paquete `2.1.89` del proyecto. El paquete `2.1.89` no trae `lib/net6.0`, así que en net6.0 se usa `netstandard2.0`. Se confirmaron:
  - `ExecuteAsync` (devuelve las filas afectadas, que es lo que usa el borrado lógico atómico de D4), `ExecuteScalarAsync<T>` y `QueryFirstOrDefaultAsync<T>`, con sobrecargas `(string sql, object? param, ...)` y `(CommandDefinition)`.
  - `CommandDefinition(sql, parameters, ..., cancellationToken: ...)`, para pasar el `CancellationToken`.
  - `DbString { Value, Length, IsAnsi = false, IsFixedLength = false }` para los parámetros `nvarchar` con su longitud (100 para `Name` y 500 para `Description`), como pide la tarea 6.3. Otra opción es `DynamicParameters.Add(name, value, DbType.String, size: n)`.

## add-products (2026-11-10)

### APIs de EF Core 6 y Dapper 2.1 confirmadas para productos (tarea 1.1)

- **Aviso sobre la versión de EF Core:** igual que en `add-categories`, Context7 (`/dotnet/entityframework.docs`) sirve la rama `main`, de una versión mayor posterior a la 6. Su tabla de funciones de SQL Server dice que `string.Contains` se traduce a `LIKE N'%' + @value + N'%'`, pero no documenta cómo escapa los comodines, así que no se usa `Contains` para `search` (D6). Las firmas se confirmaron en los XML de los paquetes `6.0.36` de la caché de NuGet.
- **EF Core 6.0.36:**
  - `EF.Functions.Like(string matchExpression, string pattern, string escapeCharacter)` (`DbFunctionsExtensions`, paquete `microsoft.entityframeworkcore`). Context7 lo documenta como `LIKE @pattern ESCAPE @escapeCharacter`. No tiene implementación en memoria: si la consulta pasa a evaluarse en el cliente, lanza una excepción.
  - `EF.Functions.Collate<TProperty>(TProperty operand, string collation)` (`RelationalDbFunctionsExtensions`, paquete `.relational`). Context7 lo documenta como `@operand COLLATE @collation` y avisa de que impide usar índices sobre esa columna, que es lo que ya asume D6 para `Name`.
- **Dapper 2.1.89** (`lib/netstandard2.0`): `QuerySingleOrDefaultAsync<T>(IDbConnection, CommandDefinition)` para leer la fila de `UPDATE ... OUTPUT INSERTED.*` (D4), además de las APIs ya confirmadas en `add-categories` (`ExecuteAsync`, `ExecuteScalarAsync<T>`, `QueryFirstOrDefaultAsync<T>`).

### `PUT`: el 404 gana a la categoría (D4)

- La spec fija el orden de los errores del alta, pero no el del `PUT`. Si el producto no existe (o está borrado) **y** la categoría tampoco, se responde 404 y no 400.
- Motivo: la URL identifica el recurso, y sin recurso no tiene sentido validar a qué categoría se mueve. El handler comprueba primero el producto y después la categoría, y lo prueba `UpdateProductHandlerTests`.

### `UpdatedAtUtc` también al borrar (D5)

- El borrado lógico fija `UpdatedAtUtc` con la fecha del borrado. La spec no lo pide; queda como registro del momento en que el producto dejó de estar activo.
- No es visible por la API, porque un producto borrado no se devuelve.

### Formato de las fechas de productos (D10)

- Como en categorías, la respuesta 201 escribe `createdAtUtc` con el sufijo `Z`, porque viene de `IClock` (`DateTimeKind.Utc`).
- Las lecturas y la respuesta 200 del `PUT` escriben `createdAtUtc` y `updatedAtUtc` **sin** sufijo, porque salen de columnas `datetime2` (la del `PUT`, del `OUTPUT INSERTED.*`) y llegan con `DateTimeKind.Unspecified`. Los valores siempre están en UTC.

### `Product.Restore` recibe un `ProductSnapshot` (regla 7)

- `Product.Create` recibe sus cinco valores sueltos, por el mismo motivo que `Category.Create`: son los datos con los que nace la entidad, y Domain no puede conocer `CreateProductCommand`.
- `Product.Restore`, en cambio, necesitaría nueve valores. Para no pasarlos sueltos, recibe el record `ProductSnapshot` (Domain), que es también el tipo al que Dapper mapea la fila de `OUTPUT INSERTED.*`. Por eso el orden de las columnas del `OUTPUT` coincide con el de los parámetros del record.
- `ProductChanges.Create` recibe sus cinco valores sueltos por el mismo motivo que `Product.Create`.

### Puertos con objetos de criterio (regla 7)

- `IProductReadRepository.ListActiveAsync` recibe `ProductListCriteria` (página, `categoryId` y `search` ya recortado), e `IProductWriteRepository.DeleteAsync` recibe `ProductDeletion` (id y fecha). Ninguno de los dos estaba en la lista de `design.md` (D1); se añadieron para no pasar valores sueltos.
- `FindActiveCategoryAsync` y `ExistsActiveAsync` reciben un `Guid`, como `DeactivateAsync` en categorías: es un solo valor y no hay ningún objeto que lo contenga sin arrastrar datos que el método no usa.

### Filtro y orden del listado sobre `ProductWithCategory` (D6)

- EF Core 6 no traduce filtros ni orden sobre propiedades de un DTO creado con constructor. El repositorio hace el `join` hacia `ProductWithCategory` (inicialización de miembros, que EF sí traduce), filtra y ordena sobre él, y proyecta a `ProductDto` al final.

### `ProductDeletion` en Application, `ProductChanges` en Domain (revisión de la tarea 9.2)

- La revisión de Clean Code señaló que los dos objetos de intención de escritura viven en capas distintas. Se acepta así: `ProductChanges` valida reglas de dominio (BR-02, BR-03 y la descripción) y por eso está en Domain, mientras que `ProductDeletion` solo transporta el id y la fecha hasta el puerto, sin ninguna regla que validar.

### Escenarios de base de datos sin prueba automática (revisión de la tarea 9.2)

- El orden y los filtros del listado, la búsqueda con `LIKE ... ESCAPE` y `COLLATE`, la collation de `init.sql`, el SKU de un producto borrado y los dos casos de concurrencia no tienen prueba automática. EF Core InMemory no traduce `EF.Functions.Like` con escape ni `Collate`, y un SQL Server de pruebas (Testcontainers u otro) exige un paquete nuevo, que hay que aprobar antes.
- Se verificaron todos a mano con Docker en la tarea 9.1 (`docs/bitacora.md`). Queda como deuda un proyecto de pruebas de integración en un cambio futuro.

## add-inventory-movements (2026-11-10)

### APIs de ASP.NET Core 6, JwtBearer 6, Dapper 2.1 y EF Core 6 confirmadas para movimientos (tarea 1.1)

- **Aviso sobre la versión de ASP.NET Core y EF Core:** Context7 (`/dotnet/aspnetcore.docs` y `/dotnet/entityframework.docs`) no separa la documentación por versión y sirve la rama `main`, de versiones mayores posteriores a la 6. Se ignoró lo que es propio de esas versiones; por ejemplo, los operadores `Order` y `OrderDescending` de EF Core 9, que no se usan. Cada firma se comprobó en los XML del paquete de referencia `Microsoft.AspNetCore.App.Ref` `6.0.36` (`ref/net6.0`) y del paquete `microsoft.aspnetcore.authentication.jwtbearer` `6.0.36` de la caché de NuGet.
- **JwtBearer 6.0.36 (D6):**
  - `JwtBearerOptions.Events` admite una instancia de `JwtBearerEvents` en la que solo se asignan los delegados que interesan.
  - `JwtBearerEvents.OnTokenValidated` se invoca después de que el token pase la validación y se haya generado el `ClaimsIdentity`. Es el punto en el que ya se pueden leer los claims.
  - `TokenValidatedContext` hereda de `ResultContext<JwtBearerOptions>`: `Principal` da el `ClaimsPrincipal` y `Fail(string failureMessage)` marca la autenticación como fallida. Context7 documenta este uso de `Fail` para la autenticación por certificado y `OnTokenValidated` en la documentación de las versiones 3 a 7.
  - Que `Fail` acabe en un 401 con el cuerpo vacío no está documentado en el XML. Se comprueba con la prueba de la tarea 2.2 y en vivo en la tarea 8.1.
- **Respuesta 201 sin `Location` (D7):** el XML de `ControllerBase.Created(string uri, object value)` y de `CreatedResult(string location, object value)` no documenta qué ocurre con una URI nula. Por eso no se usa `Created`, sino `ControllerBase.StatusCode(int statusCode, object value)`, que crea un `ObjectResult` con ese código y ese cuerpo y no añade ninguna cabecera. La prueba del controlador (tarea 7.1) comprueba que no hay `Location`.
- **Dapper 2.1.89** (`lib/netstandard2.0`; Context7 `/dapperlib/dapper/2.1.66`, misma versión mayor y menor) (D3):
  - `CommandDefinition(string commandText, object? parameters = null, IDbTransaction? transaction = null, ..., CancellationToken cancellationToken = default)`: la transacción se pasa en cada comando.
  - La transacción se abre con `DbConnection.BeginTransactionAsync` de ADO.NET, no con Dapper. Está en el XML de `Microsoft.NETCore.App.Ref` `6.0.36` (`System.Data.Common`). `QuerySingleOrDefaultAsync<T>(CommandDefinition)` lee el `OUTPUT INSERTED.StockQuantity` y `ExecuteAsync(CommandDefinition)` ejecuta el `INSERT`, como ya se confirmó en `add-products`.
  - El código de error `8115` (desbordamiento aritmético) es de SQL Server, no de Dapper. Se lee de `SqlException.Number`, como el 2601/2627 del SKU, y se comprueba en vivo en la tarea 8.1.
- **EF Core 6.0.36 (D5):**
  - `OrderByDescending`, `ThenByDescending`, `Skip` y `Take` se traducen a SQL, igual que el `OrderBy`/`ThenBy` que ya usa `ProductReadRepository`.
  - El proveedor de SQL Server asocia `byte` a `tinyint` (Context7 lo muestra en la tabla de conversiones, `Convert.ToByte` a `CONVERT(tinyint, ...)`). `MovementRecord.MovementType` se declara `byte` para leer la columna `TINYINT`. Se comprueba con la base en la tarea 8.1.

### `sub` como autor del movimiento y 401 sin `sub` en todos los endpoints (tarea 2.2)

- Con `client_credentials`, Keycloak emite como `sub` el UUID del usuario de servicio del cliente (`service-account-inventory-client`), no el id legible del cliente (`azp`). `RegisteredBy` guardará ese UUID, porque así lo pide la propuesta. Si se quiere el nombre del cliente habrá que decidirlo en otro cambio: los movimientos ya registrados no se pueden corregir.
- `OnTokenValidated` rechaza los tokens sin `sub`, o con un `sub` vacío o solo con espacios, con `context.Fail(...)`. Se aplica a **todos** los endpoints bajo `/api`, incluidas las lecturas y `whoami`, no solo a los movimientos (D6). Con el realm actual todos los tokens traen `sub` (client scope `basic`), así que ningún cliente existente queda fuera. Un cliente nuevo sin ese scope recibiría 401 en cualquier endpoint.
- El mensaje de `Fail` está en inglés porque no llega al cliente: el 401 se responde con el cuerpo vacío.

### El autor del movimiento no se recorta (tarea 3.1)

- `MovementRules.ValidateRegisteredBy` comprueba que el autor no esté en blanco y que no supere 100 caracteres, pero lo devuelve **tal cual**, sin recortarlo. A diferencia de `type` y `reason`, no lo escribe el cliente: es el `sub` del token, y el libro debe guardar la identidad exactamente como la emitió Keycloak.
- `MovementType` declara `byte` como tipo subyacente, con `Entry = 1` y `Exit = 2`, para coincidir con la columna `TINYINT` y su `CHECK IN (1, 2)`.

### `InventoryMovement.Create` recibe un `MovementDraft` (tarea 3.2, regla 7)

- `Create` necesitaría seis valores sueltos (producto, tipo, cantidad, motivo, autor y fecha). Para no pasarlos sueltos recibe el record de dominio `MovementDraft`, igual que `Product.Restore` recibe `ProductSnapshot`. El handler de la tarea 5.1 lo construye a partir del comando, del `MovementRegistrant` y de `IClock`.
- El `Id` se genera dentro de `Create` con `Guid.NewGuid()`, como en `Product.Create`. La fecha sí llega en el draft.
- `StockAfter` es `null` hasta que el repositorio aplica el movimiento. `WithStockAfter(int)` devuelve una copia con el saldo y deja intacto el original, para que la entidad siga siendo inmutable.

### `ConflictException` deja de ser `sealed` (tarea 3.3)

- D4 pide que `InsufficientStockException` herede de `ConflictException` para que `ExceptionHandlingMiddleware` la traduzca a 409 sin cambios. Para eso `ConflictException` pasa de `sealed` a `public class`. Las demás excepciones de dominio siguen selladas, igual que `InsufficientStockException`.
- La alternativa era una excepción independiente con un caso nuevo en el `switch` del middleware. Se descartó porque obligaba a tocar Api por una regla del dominio, y porque "stock insuficiente" es, por definición, un conflicto con el estado del recurso.

### Condición del `UPDATE` de stock sin suma (tarea 6.1)

- `MovementWriteRepository` usa `WHERE Id = @ProductId AND IsActive = 1 AND StockQuantity >= -@Delta`, no `StockQuantity + @Delta >= 0` como decía al principio D3 (ya corregido en `design.md`).
- Motivo: SQL Server no garantiza el orden en que evalúa los predicados de un `WHERE`. Con la suma, una entrada sobre un producto borrado con stock cercano a `int.MaxValue` podría lanzar el error 8115 antes de descartar la fila por `IsActive`, y respondería 409 en lugar del 404 que fija la spec. `-@Delta` no se desborda nunca, porque `quantity` es como máximo `int.MaxValue`.
- La transacción se abre de forma explícita (`OpenAsync` y `BeginTransactionAsync`). Solo se confirma cuando el resultado es `Registered`. En cualquier otro caso (rechazo, error 8115 o fallo del `INSERT`), al liberar la transacción sin `Commit` se hace rollback.

### El tipo del movimiento se convierte a texto en memoria (tarea 6.2)

- `MovementReadRepository.ListAsync` filtra, ordena (`OccurredAtUtc DESC`, `Id DESC`), cuenta y pagina en SQL. Después materializa los `MovementRecord` de esa página y los convierte a `MovementDto` en memoria, con `((MovementType)record.MovementType).ToString()`.
- Se hace así para no depender de que EF Core 6 traduzca a SQL la conversión de un `byte` a enum y después a texto. Como solo se convierte una página, de 100 elementos como máximo, el coste es despreciable.
- `ExistsActiveProductAsync` consulta `Products` con `AnyAsync`, que se traduce a `EXISTS`.

### 201 sin `Location` al registrar un movimiento (tarea 7.3)

- `POST /api/products/{productId}/movements` responde 201 **sin** cabecera `Location`, a diferencia de categorías y productos. No existe un `GET` de un movimiento suelto, y la propuesta no lo pide. RFC 9110 permite un 201 sin `Location`.
- Se descartaron dos alternativas. Apuntar `Location` a la colección no identifica el recurso creado. Apuntarla a `/movements/{id}` daría un enlace que responde 404.
- Si un cambio futuro añade el `GET` por id, se podrá añadir la cabecera sin romper a ningún cliente. El cuerpo ya trae `id` y `stockAfter`.

### El orden por fecha puede no reflejar el orden de aplicación (tarea 7.3, D5)

- `occurredAtUtc` lo fija `IClock` en el handler, **antes** de que el `UPDATE` obtenga el bloqueo de la fila del producto. Si dos movimientos del mismo producto llegan a la vez, el que espera el bloqueo puede tener una hora anterior y, aun así, aplicarse después. En el listado aparecería como "más antiguo", aunque su `stockAfter` corresponda a un estado posterior.
- Se acepta porque la ventana es de milisegundos, solo afecta a movimientos simultáneos del mismo producto, y el saldo autoritativo sigue siendo `stockAfter` de cada movimiento y `Products.StockQuantity`. La alternativa, fijar la hora en SQL con `SYSUTCDATETIME()` después del bloqueo, rompía la convención de que `IClock` da la fecha a los comandos y quitaba a las pruebas el control sobre la hora.
- El desempate por `Id DESC` es arbitrario (los GUID son aleatorios) pero estable: la misma consulta devuelve siempre el mismo orden, y la paginación no repite ni salta movimientos.

### Hallazgos aceptados en la revisión de Clean Code de la tarea 8.2

- **Ayudas de prueba con valores sueltos (regla 7):** `CreateCommand(type, quantity, reason)` en `RegisterMovementHandlerTests` y `CreateDraft(type, quantity, reason)` en `InventoryMovementTests` reciben tres valores. Se acepta, como en las pruebas de productos, porque son ayudas de prueba y cada prueba varía justo esos tres valores. Cuando hace falta cambiar otro campo se usa `with` sobre el objeto que devuelven.
- **Escenarios sin prueba automática:** estos escenarios dependen de SQL Server y solo se verifican a mano (tarea 8.1, `docs/bitacora.md`, E5):
  - el orden `OccurredAtUtc DESC, Id DESC` y el filtro por producto;
  - la paginación sobre datos reales;
  - la atomicidad (BR-14) y las salidas simultáneas (BR-15);
  - la traducción del error 8115 a 409;
  - que `updatedAtUtc` no cambia;
  - los 405 y 404 de `DELETE`, `PATCH` y `PUT`;
  - los 400 de `type` numérico y `quantity` decimal, que produce el deserializador.

  `MovementWriteRepository` y `MovementReadRepository` no tienen prueba unitaria. Es la misma deuda que ya registró `add-products`: un proyecto de pruebas de integración contra SQL Server exige un paquete nuevo, que hay que aprobar antes.
- **Prueba BR-15 eliminada:** `BR15_Rejects_ExitThatLostTheRaceForTheStock` era idéntica a la de BR-12 y su nombre prometía una concurrencia que no probaba. BR-15 la garantiza el `UPDATE` condicional en la base, no el handler, así que su verificación es la de la tarea 8.1 (5 rondas de salidas simultáneas, siempre `[201, 409]`).

## add-suppliers (2026-11-10)

### APIs de .NET 6, ASP.NET Core 6, Dapper 2.1 y EF Core 6 confirmadas para proveedores (tarea 1.1)

- **Aviso sobre la versión:** Context7 (`/dotnet/docs`, `/dotnet/aspnetcore.docs` y `/dotnet/entityframework.docs`) no separa la documentación por versión y sirve la rama `main`, de versiones mayores posteriores a la 6. Se ignoró lo que es propio de esas versiones; en particular, `[GeneratedRegex]` (generadores de código fuente de .NET 7 y propiedades parciales de .NET 9), que **no** se usa. Cada firma se comprobó en los XML de `Microsoft.NETCore.App.Ref` `6.0.36` y `Microsoft.AspNetCore.App.Ref` `6.0.36` (`ref/net6.0`), de `microsoft.entityframeworkcore` `6.0.36` (`lib/net6.0`) y de `dapper` `2.1.89` (`lib/netstandard2.0`).
- **Regex en .NET 6 (D2):** `Regex(string pattern, RegexOptions options, TimeSpan matchTimeout)` existe, igual que `RegexOptions.CultureInvariant` y `RegexMatchTimeoutException`. El correo y el teléfono se validan con instancias `static readonly` creadas con ese constructor y un tiempo máximo. Context7 muestra ese patrón como el anterior a los generadores de .NET 7. No se usa `RegexOptions.Compiled`, porque los patrones son cortos.
- **ASP.NET Core 6 (D8):**
  - Una plantilla de ruta de una acción que empieza por `/` no se combina con la del controlador (Context7, `mvc/controllers/routing.md`). `HttpGetAttribute(string template)` existe en el XML 6.0.36. Así, `GET /api/suppliers/{supplierId:guid}/products` puede vivir en `ProductSuppliersController`.
  - `ControllerBase.CreatedAtAction(string actionName, object routeValues, object value)` existe en el XML 6.0.36. Responde 201 y construye `Location` con la acción y los valores de ruta, que pueden ser dos (`productId` y `supplierId`).
- **Dapper 2.1.89 (D4, D6):** `QuerySingleOrDefaultAsync<T>(IDbConnection, CommandDefinition)` devuelve la única fila o `default` si no hay ninguna. Sirve para el `UPDATE ... OUTPUT INSERTED.*` de la edición (`null` da 404) y para leer los nombres tras el `INSERT ... SELECT ... WHERE EXISTS` de la asociación. `CommandDefinition` acepta la transacción y el `CancellationToken`, como en movimientos.
- **EF Core 6.0.36 (D7):**
  - `AsNoTracking<T>(IQueryable<T>)` y `CountAsync<T>(IQueryable<T>, CancellationToken)` existen en el XML.
  - Context7 documenta `join ... on ... equals ...` como `INNER JOIN` en el servidor. Las tres tablas se unen así, sin propiedades de navegación, porque no hay llaves foráneas.
  - Context7 recomienda un orden totalmente único con `Skip`/`Take` (`OrderBy` más `ThenBy` por un id). Es lo que ya piden las specs para los tres listados.

### Longitud antes que formato en el correo y el teléfono (tarea 3.1, D2)

- `SupplierRules.ValidateEmail` y `ValidatePhone` comprueban primero la longitud y después el formato. Un correo o un teléfono que supera el máximo recibe el mensaje de longitud aunque además tenga un formato inválido. Así lo piden los escenarios "Correo de 201 caracteres" y "Teléfono de 31 caracteres".
- El teléfono incumple el formato en dos casos, que dan el mismo mensaje: si tiene caracteres distintos de un `+` inicial, dígitos, espacios y guiones, o si no tiene entre 7 y 15 dígitos. Los dígitos se cuentan con `char.IsDigit`. Un dígito Unicode que no sea `0-9` también cuenta, pero el patrón ya rechaza ese teléfono, así que el resultado no cambia.
- Las expresiones regulares se evalúan con un tiempo máximo de 100 ms. Los patrones no tienen cuantificadores anidados, así que una `RegexMatchTimeoutException` no debería ocurrir. Si ocurriera, el middleware la respondería como un 500.

### `Supplier` con `SupplierDraft`, `SupplierSnapshot`, `SupplierEdit` y `SupplierChanges` (tarea 3.2, regla 7)

- `Supplier.Create` recibe un `SupplierDraft` y `Supplier.Restore` un `SupplierSnapshot`, igual que `InventoryMovement.Create(MovementDraft)` y `Product.Restore(ProductSnapshot)`. `Restore` no estaba en la tarea, pero lo necesita el `UPDATE ... OUTPUT` de la edición (tarea 5.1).
- La edición usa la variante inmutable que admite la tarea: `SupplierChanges.Create(SupplierEdit)` valida nombre, correo y teléfono, y devuelve un objeto sin `Nit` ni `CreatedAtUtc`. El repositorio aplica esos cambios en el `UPDATE`, como hace `ProductChanges`. No existe un `Supplier.ApplyChanges`, porque la entidad nunca se modifica en memoria.
- `SupplierEdit` es un tipo que no aparece en D1. Agrupa los datos de entrada de la edición para no pasar cinco valores sueltos.
- Las pruebas de `SupplierChanges` van en `SupplierChangesTests.cs`, no en `SupplierTests.cs`. Así se sigue la convención de `ProductChangesTests` (una clase de prueba por clase).

### Puertos de proveedores (tarea 4.1)

- `DeactivateAsync` recibe un `SupplierDeactivation` (id y fecha). Así el repositorio fija `UpdatedAtUtc` con la hora de `IClock` (D6), igual que `ProductDeletion` en el borrado de productos.
- `CreateAsync` no devuelve nada. Si el índice único rechaza el NIT en una carrera (2601/2627), el repositorio lanza `ConflictException` con `SupplierMessages.NitAlreadyExists` (D3). `IProductWriteRepository.AddAsync`, en cambio, devuelve `bool`, porque su `INSERT` también puede fallar si la categoría no está activa. El alta de un proveedor no tiene esa segunda condición.
- `SupplierListCriteria` solo lleva el `PageRequest`, porque el listado no tiene filtros. Existe para que el puerto reciba un objeto de criterio, como los demás listados (regla 7).
- `FakeSupplierWriteRepository.CreateException` permite simular la carrera del NIT en las pruebas del handler (tarea 4.2).

### `Created` con la ruta en lugar de `CreatedAtAction` (tarea 6.1, D8)

- D8 proponía `CreatedAtAction` hacia `GET {id}`. `SuppliersController.Create` usa en cambio `Created($"/api/suppliers/{id}", dto)`, igual que `ProductsController` y `CategoriesController`. El resultado HTTP es el mismo (201 con `Location: /api/suppliers/{id}`). La diferencia es que la prueba unitaria puede comprobar `Location` sin enrutamiento: `CreatedAtActionResult` solo calcula la URL cuando se ejecuta dentro de la canalización de MVC.
- La lista de proveedores reutiliza `PaginationRequest`, con un método nuevo `ToListSuppliersQuery`, como hacen categorías y movimientos.
- Hasta que la tarea 6.2 no registre `AddSuppliers`, el controlador existe pero no tiene sus handlers en el contenedor. Una petición a `/api/suppliers` en ese estado respondería 500. No se despliega entre las dos tareas.

### Validación de la asociación (tarea 7.1)

- `ProductSupplierRules.ValidateSupplierId` trata `Guid.Empty` como ausente, igual que `ProductRules.ValidateCategoryId`, y responde `El proveedor es obligatorio.`
- `ProductSupplier.Create` valida `supplierId` antes que `supplierSku`. Las dos son reglas de formato (el primer paso del orden de errores de la spec), así que el orden entre ellas solo decide qué mensaje sale cuando fallan las dos.
- El mensaje de longitud de `supplierSku` es `El código del producto en el proveedor no puede superar los 50 caracteres.`
- `ProductSupplier` no tiene `Restore`, porque la asociación nunca se edita: el alta devuelve el DTO del puerto (D4) y las lecturas usan EF Core.

### Puertos de asociaciones (tarea 7.2)

- `FindVisibleAsync` recibe un `ProductSupplierPair` (producto y proveedor), y los dos listados reciben `ProductSuppliersCriteria` y `SupplierProductsCriteria`. Así se evita pasar ids sueltos (regla 7).
- `ProductSupplierLinkResult` sigue el patrón de `MovementRegistrationResult`: un estado y el DTO cuando el estado es `Linked`. El puerto devuelve el DTO, con los nombres del producto y del proveedor, para que el handler de comando no tenga que usar el puerto de lectura (D4).
- Mensajes: `El proveedor indicado no existe o no está activo.` (400), `El proveedor ya está asociado al producto.` (409) y `El proveedor no está asociado al producto.` (404 de la pareja). El 404 de un producto o un proveedor no activo en la ruta de un listado reutiliza `ProductMessages.ActiveProductNotFound` y `SupplierMessages.ActiveSupplierNotFound`.

### Alta de la asociación en un solo lote (tarea 8.1, D4)

- `ProductSupplierWriteRepository.LinkAsync` envía en un solo comando el `INSERT ... SELECT ... WHERE EXISTS` y el `SELECT` que lee la asociación con los nombres. El `INSERT` no devuelve ningún conjunto de resultados, así que `QuerySingleOrDefaultAsync<ProductSupplierDto>` lee el del `SELECT`. Si viene vacío, no se insertó nada. Así se evita una segunda ida a la base y no hace falta un `OUTPUT`, que no puede unir con `Products` ni `Suppliers`.
- El `INSERT` se ejecuta en modo autocommit. Por eso el `UPDLOCK, HOLDLOCK` sobre la fila del proveedor dura lo que dura la sentencia, y basta para que una desactivación simultánea espere o se vea (BR-09).
- El `SELECT` posterior no filtra por `IsActive`. Si el producto o el proveedor se desactivan justo después del `INSERT`, la respuesta sigue siendo 201 con sus nombres, que es correcto para el instante del alta.
- Cuando no se inserta nada, el diagnóstico comprueba el producto: si no está activo es 404 y, si lo está, 400 por el proveedor. Esa lectura no se bloquea, por la misma razón que en D3 de `add-inventory-movements`: un cambio posterior solo altera el diagnóstico, y los dos resultados son correctos para ese instante.
- Que Dapper asigne bien las columnas al constructor de `ProductSupplierDto` y que el lote devuelva la fila se comprueba con SQL Server en la tarea 11.1.

### `createdAtUtc` sin `Z` en el 201 de la asociación (tarea 8.4)

- Al revés que en el resto de las altas, la respuesta 201 de `POST /api/products/{productId}/suppliers` no se construye en memoria: sale del `SELECT` que lee la asociación en el mismo lote (D4). Por eso `createdAtUtc` llega sin sufijo `Z`, igual que en las lecturas, porque la columna es `datetime2`. El valor sigue estando en UTC. Se comprobó en vivo en la tarea 8.3.

### Proveedor en las entradas (tareas 9.1 a 9.4, D5)

- **El `UPDLOCK` serializa las entradas con el mismo proveedor:** la comprobación del proveedor toma `UPDLOCK, HOLDLOCK` sobre su fila y lo mantiene hasta el `COMMIT`.
  - Dos entradas simultáneas que indican el **mismo** proveedor se esperan una a otra, aunque sean de productos distintos. Las altas de asociación con ese proveedor también esperan.
  - Se aceptó por coherencia con el bloqueo de la categoría en productos, porque la transacción es corta (tres o cuatro sentencias).
  - Si el volumen lo pidiera, un `HOLDLOCK` sin `UPDLOCK` (bloqueo compartido) dejaría las entradas en paralelo y seguiría frenando la desactivación.
- **El NIT solo se recorta:** `900.123.456-7` y `900123456-7` son NIT distintos. La propuesta no pide normalizarlo.
- **Un `supplierId` igual a `Guid.Empty` no es ausente** en un movimiento, a diferencia de la asociación. En una entrada responde 400, porque ese proveedor no existe, y en una salida responde 400 por llevar proveedor.
- **`FakeMovementWriteRepository` no cambió:** reconstruye el movimiento con `WithStockAfter`, que ya conserva `SupplierId`.
- La comprobación del proveedor se probó en vivo en la tarea 9.3 (entrada con un proveedor asociado, no asociado o desactivado; salida con `supplierId`; producto borrado). La concurrencia con la desactivación se verifica en la tarea 11.1.

### Revisión de Clean Code de `add-suppliers` (tarea 10.1)

La revisión se hizo con el subagente `code-reviewer`, en un contexto distinto del de la implementación. El veredicto fue **aprobado**: 0 hallazgos bloqueantes y 6 menores.

- **Corregidos:**
  1. `LockSupplierOrDiagnoseAsync` pasa a llamarse `LockSupplierAndFindRejectionAsync`, porque bloquea el proveedor y devuelve el posible rechazo.
  2. `BR09_Create_Rejects_MissingSupplier` pasa a llamarse `BR09_Create_Rejects_MissingSupplier_BeforeValidatingTheSku`, porque también prueba ese orden.
  3. El constructor privado de `ProductSupplier` recibe un único `ProductSupplierDraft` ya validado (`draft with { ... }`), como `SupplierChanges` (regla 7).
  5. `ProductSuppliersController` construye `Location` con la constante `LinkLocationFormat` y no repite la ruta a mano.
  6. En `InventoryMovementTests`, el auxiliar `CreateDraft` vuelve al final de la clase.
- **Aceptado (regla 7):** `MovementRules.ValidateSupplier(MovementType type, Guid? supplierId)` recibe dos valores sueltos. `type` ya viene validado por `ValidateType`, y pasar el `MovementDraft` obligaría a validarlo otra vez o a recibir el tipo sin validar. Es el mismo motivo por el que `PageRequest.Create` recibe valores sueltos.
- **Observaciones que no se corrigen en este cambio:**
  - `CalculateOffset`, `IsUniqueViolation` y las constantes 2601/2627 se repiten en varios repositorios. Es un patrón previo a este cambio; extraerlos a un helper común sería un cambio de refactorización aparte.
  - Los escenarios que dependen de SQL Server o de la canalización HTTP (orden de los listados, concurrencia, 405, `supplierId` que no es un GUID) se verifican a mano en la tarea 11.1. Es la misma deuda que en los cambios anteriores.
