# Tasks

## 1. Limpieza de la plantilla

- [x] 1.1 Quitar el paquete `Swashbuckle.AspNetCore.ApiTesting` de `src/Inventory.Api/Inventory.Api.csproj` y eliminar `app.UseHttpsRedirection()` de `Program.cs` (D11). Verificar: `dotnet build` y `dotnet test` en verde, y `grep ApiTesting src` sin resultados.

## 2. ScopeSet (TDD)

- [x] 2.1 Crear `InventoryScopes` (constantes `Read` y `Write`) en `src/Inventory.Application/Security/`. Escribir primero las pruebas que fallan en `tests/Inventory.UnitTests/Security/ScopeSetTests.cs`, una por escenario del requisito "Comprobación de scope dentro del claim scope" (scope presente, coincidencia parcial, mayúsculas distintas, espacios repetidos o en los extremos, claim vacío o nulo). Después, implementar `ScopeSet` en `src/Inventory.Application/Security/ScopeSet.cs` (D7). Verificar: `dotnet test` con todas las pruebas de `ScopeSetTests` en verde y `dotnet build` sin advertencias nuevas.
- [x] 2.2 Añadir una prueba que compruebe que `ScopeSet` expone la lista de scopes sin vacíos ni duplicados de espacios (la usa `whoami`). Verificar: `dotnet test` en verde.

## 3. Keycloak en Docker

- [x] 3.1 Consultar Context7 con Keycloak 26.x (importación de realm, placeholders, hostname v2) y fijar el tag de la imagen. Registrar la versión y el `ClockSkew` de 30 s en `docs/assumptions.md` (crearlo si no existe). Verificar: `docs/assumptions.md` contiene el tag exacto y el supuesto de `ClockSkew`.
- [x] 3.2 Crear `keycloak/inventory-realm.json` con el realm `inventory`, los client scopes opcionales `inventory.read` e `inventory.write` (`include.in.token.scope = true`) y el cliente confidencial `inventory-client`: solo `serviceAccountsEnabled`, `standardFlowEnabled = false`, `directAccessGrantsEnabled = false`, `"secret": "${INVENTORY_CLIENT_SECRET}"`, `webOrigins: ["http://localhost:8080"]`, default scopes solo `basic`, optional scopes los dos de inventario y un mapper `oidc-audience-mapper` con `inventory-api` (D2, D3, D4, D9). Verificar: el JSON es válido (`python -m json.tool keycloak/inventory-realm.json`) y no contiene ningún secreto literal.
- [x] 3.3 Añadir el servicio `keycloak` a `docker-compose.yml` (`start-dev --import-realm`, volumen `./keycloak:/opt/keycloak/data/import:ro`, `127.0.0.1:8081:8080`, `KC_HOSTNAME=http://localhost:8081`, `KC_HOSTNAME_BACKCHANNEL_DYNAMIC=true`, `KC_BOOTSTRAP_ADMIN_USERNAME`, `KC_BOOTSTRAP_ADMIN_PASSWORD` e `INVENTORY_CLIENT_SECRET` con `${VAR:?}`). En `api`: quitar `Auth__SigningKey`, `Auth__ClientId`, `Auth__ClientSecret` y `Auth__TokenLifetimeMinutes`; añadir `Auth__MetadataAddress`, `Auth__Issuer=http://localhost:8081/realms/inventory` y `Auth__Audience=inventory-api`; añadir `depends_on: keycloak: service_started` (D1, D4, D12). Actualizar `.env.example`: quitar `AUTH_SIGNING_KEY`, `AUTH_CLIENT_ID` y `AUTH_CLIENT_SECRET` y añadir las tres variables nuevas con valores `change-me-...`. Verificar: `docker compose config` falla sin `INVENTORY_CLIENT_SECRET` y nombra la variable; con `.env` completo, `docker compose up -d keycloak` deja `http://localhost:8081/realms/inventory/.well-known/openid-configuration` respondiendo 200.
- [x] 3.4 Verificar el contenido del token con `curl`: con `scope=inventory.read`, el token decodificado tiene `iss=http://localhost:8081/realms/inventory`, `aud` con `inventory-api`, `azp=inventory-client` y `scope` con `inventory.read` sin `inventory.write`; con ambos scopes aparecen los dos; sin scopes sigue apareciendo `aud`; con un secreto incorrecto la respuesta es 401 `unauthorized_client` (lo que devuelve Keycloak 26.8.1); desde el contenedor `api`, la metadata de `keycloak:8080` tiene `issuer` en `localhost:8081` y `jwks_uri` en `keycloak:8080`. Verificar: los comandos y sus resultados se anotan en `docs/bitacora.md`.

## 4. Validación de tokens en la API

- [x] 4.1 Consultar Context7 con JwtBearer 6 y Microsoft.Extensions.Options 6 (`ValidateOnStart`). Crear `AuthOptions` (sección `Auth`, `[Required]` en MetadataAddress, Issuer, Audience y SwaggerClientId) con `ValidateDataAnnotations().ValidateOnStart()` (D6). Verificar: `dotnet build` y `dotnet test` en verde; `dotnet run` sin `Auth__Audience` falla al arrancar con un mensaje que nombra `Audience`.
- [x] 4.2 Registrar `AddAuthentication().AddJwtBearer` con los valores de `AuthOptions`, `RequireHttpsMetadata = false`, `MapInboundClaims = false`, validación de emisor, audiencia, tiempo de vida y firma, y `ClockSkew` de 30 s. Añadir `app.UseAuthentication()` antes de `UseAuthorization()` (D5, D11). Verificar: `dotnet build` y `dotnet test` en verde.
- [x] 4.3 Crear `ScopePolicies` en `src/Inventory.Api/Authorization/` con las políticas `InventoryRead` e `InventoryWrite` (`RequireAuthenticatedUser` + `RequireAssertion` con `ScopeSet`) y registrarlas en `Program.cs` (D7). Verificar: `dotnet build` y `dotnet test` en verde.

## 5. Endpoints de /api/auth

- [x] 5.1 Crear `WhoAmIResponse` y `AuthController` (`[Authorize]`, ruta `api/auth`) con `whoami` (`azp` + `ScopeSet`), `check-read` (política `InventoryRead`) y `check-write` (política `InventoryWrite`), con mensajes en español (D10). Verificar: `dotnet build` y `dotnet test` en verde.
- [x] 5.2 Documentar en `docs/api-contract.md` (crearlo si no existe) los tres endpoints, los scopes que exige cada uno, el formato de `whoami`, que 401 y 403 se devuelven con el cuerpo vacío (D8) y que `check-read` y `check-write` son temporales hasta el entregable 3. Verificar: el documento cubre todos los escenarios de `specs/api-auth`.

## 6. Swagger

- [x] 6.1 Consultar Context7 con Swashbuckle 6.5. Configurar en `AddSwaggerGen` el esquema `oauth2` (ClientCredentials, `TokenUrl` derivada de `AuthOptions.Issuer`, scopes `inventory.read` e `inventory.write` con descripción en español), el esquema `Bearer` (HTTP bearer, JWT) y un requisito de seguridad global; en Swagger UI, `OAuthClientId` con `AuthOptions.SwaggerClientId` (`Auth__SwaggerClientId`) y sin secreto (D9). Verificar: `dotnet build` en verde y `/swagger/v1/swagger.json` contiene ambos `securitySchemes`.

## 7. Documentación

- [x] 7.1 Actualizar `docs/architecture.md`: Keycloak emite los tokens y la API solo los valida (MetadataAddress, emisor, audiencia y scopes); quitar "emisión de tokens" de Infrastructure y "validación de credenciales OAuth2" de Application; ubicar `ScopeSet` en `Application/Security/` y `ScopePolicies` en `Api/Authorization/`; añadir `keycloak/` a la estructura del repositorio; quitar los errores `invalid_client`, `invalid_scope` y `unsupported_grant_type` emitidos por la API. Verificar: `grep -n "HS256\|emisión de tokens\|SigningKey" docs/architecture.md` sin resultados.
- [x] 7.2 Registrar en `docs/decisions.md` (crearlo si no existe) la decisión de usar OAuth2 con Keycloak como servidor de identidad en lugar de la emisión propia, con alternativas y consecuencias. Verificar: hay una entrada fechada con contexto, decisión y consecuencias.
- [x] 7.3 Actualizar `AGENTS.md` (quitar "servicio de tokens" de Infrastructure e indicar que la autenticación la valida Api contra Keycloak) y el `context` de `openspec/config.yaml` (OAuth2 client_credentials con Keycloak; la API valida JWT, no los emite). Verificar: `grep -n "servicio de tokens" AGENTS.md` sin resultados y `openspec list --json` sigue funcionando.
- [x] 7.4 Añadir al README, en "Cómo ejecutarlo", los pasos para crear `.env`, levantar con `docker compose up -d --build`, esperar a Keycloak y autorizar en Swagger (`http://localhost:8080/swagger`) con `inventory-client`. Verificar: los pasos se pueden seguir tal cual en la tarea 8.1.

## 8. Verificación integrada

- [x] 8.1 Desde un clon limpio, con `.env` creado desde `.env.example`, ejecutar `docker compose up -d --build` y comprobar en Swagger: sin autorizar, `check-read` y `check-write` dan 401; autorizado solo con `inventory.read`, `check-read` da 200 y `check-write` da 403; con ambos scopes, `check-write` da 200; `whoami` da 200 con `clientId = inventory-client` y los scopes pedidos; un token pegado como Bearer funciona. Con `curl`: un token con la firma alterada da 401 y un token caducado (esperar a que venza más 30 s) da 401. Verificar: resultados anotados en `docs/bitacora.md`, `dotnet build` y `dotnet test` en verde.
- [x] 8.2 Ejecutar `openspec validate add-keycloak-oauth --strict` y una revisión de Clean Code (`clean-code-review`) del diff en una sesión nueva. Verificar: la validación pasa sin errores y la revisión no deja hallazgos sin resolver.
- [x] 8.3 Leer el id del cliente de `INVENTORY_CLIENT_ID` (por defecto `inventory-client`): placeholder `${INVENTORY_CLIENT_ID}` en `keycloak/inventory-realm.json`, la variable en los servicios `keycloak` y `api` (`Auth__SwaggerClientId`) de `docker-compose.yml` y en `.env.example` (D4, D9). Verificar: `docker compose config` resuelve el valor por defecto y uno distinto; con Keycloak recreado con `INVENTORY_CLIENT_ID=other-client`, el token tiene `azp=other-client` e `inventory-client` es rechazado; los resultados se anotan en `docs/bitacora.md`.
- [x] 8.4 Leer la audiencia de `INVENTORY_API_AUDIENCE` (por defecto `inventory-api`): placeholder `${INVENTORY_API_AUDIENCE}` en el mapper de audiencia de `keycloak/inventory-realm.json`, la variable en los servicios `keycloak` y `api` (`Auth__Audience`) de `docker-compose.yml` y en `.env.example` (D2). Verificar: `docker compose config` resuelve el valor por defecto y uno distinto; con Keycloak y la API recreados con `INVENTORY_API_AUDIENCE=other-api`, el token tiene `aud=other-api` y `whoami` responde 200; los resultados se anotan en `docs/bitacora.md`.

## Workflow follow-up

- Archivar el cambio cuando el pull request esté aprobado y fusionado.
- En el entregable 3, la tarea de limpieza elimina `check-read` y `check-write` y actualiza `docs/api-contract.md`.
