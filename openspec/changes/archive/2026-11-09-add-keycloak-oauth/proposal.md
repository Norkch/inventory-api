# Proposal

## Why

La API necesita proteger sus endpoints con OAuth2 `client_credentials` antes de exponer los endpoints de negocio. El plan anterior (la API emitía sus propios JWT HS256 con una clave compartida) obligaba a mantener credenciales, firma y emisión dentro de la API. Delegar la emisión en Keycloak deja a la API con una sola responsabilidad: validar tokens y comprobar scopes. Además, todo el flujo se puede probar desde Swagger.

## What Changes

- Nuevo servicio `keycloak` en `docker-compose.yml` (modo desarrollo) que importa al arrancar el realm `inventory` desde `./keycloak/inventory-realm.json`. El secreto del cliente llega por variable de entorno y no está escrito en el repositorio.
- Cliente de servicio `inventory-client` con dos client scopes opcionales, `inventory.read` e `inventory.write`, y un mapper de audiencia que añade `aud: inventory-api`.
- Emisor fijo `http://localhost:8081/realms/inventory` con `KC_HOSTNAME` y `KC_HOSTNAME_BACKCHANNEL_DYNAMIC=true`. La API consulta la metadata por la red interna (`keycloak:8080`).
- La API valida los JWT con JwtBearer (`Auth__MetadataAddress`, `Auth__Issuer`, `Auth__Audience`), sin mapeo automático de claims. Swagger UI deja rellenado el id del cliente que lee de `Auth__SwaggerClientId`. No arranca si falta alguna de esas variables.
- Políticas por scope: las lecturas exigen `inventory.read` y las escrituras `inventory.write`. Sin token, o con un token inválido, caducado, de otro emisor o de otra audiencia, la respuesta es 401. Si falta el scope, 403. Ambas respuestas van con cuerpo vacío.
- `ScopeSet` en `Inventory.Application/Security/` (lógica pura, hecha con TDD) y `ScopePolicies` en `Inventory.Api/Authorization/`.
- Swagger UI con el botón Authorize (OAuth2 client credentials contra Keycloak y, como alternativa, pegar un token Bearer).
- Nuevos endpoints `GET /api/auth/whoami`, `GET /api/auth/check-read` y `GET /api/auth/check-write`. Los dos últimos son temporales.
- **BREAKING** (solo configuración): se eliminan la emisión propia de tokens y las variables `Auth__SigningKey`, `Auth__ClientId`, `Auth__ClientSecret`, `Auth__TokenLifetimeMinutes`, `AUTH_SIGNING_KEY`, `AUTH_CLIENT_ID` y `AUTH_CLIENT_SECRET`.
- Se elimina el paquete `Swashbuckle.AspNetCore.ApiTesting` y la llamada `app.UseHttpsRedirection()`.
- Se actualiza la documentación (`docs/architecture.md`, `docs/api-contract.md`, `docs/decisions.md`, `AGENTS.md` y `openspec/config.yaml`) para que deje de hablar del "servicio de tokens" propio.

## Capabilities

### New Capabilities
- `api-auth`: validación de tokens bearer emitidos por el servidor de identidad, políticas por scope (`inventory.read` e `inventory.write`), respuestas 401 y 403, y endpoints de identidad y diagnóstico bajo `/api/auth`.
- `dev-identity-provider`: servidor de identidad Keycloak de desarrollo en Docker (realm, cliente de servicio, scopes, audiencia, emisor) y su uso desde Swagger UI.

### Modified Capabilities
- Ninguna (no hay specs previos).

## Impact

- **Código:** `src/Inventory.Application/Security/ScopeSet.cs` (nuevo); `src/Inventory.Api/Authorization/`, `Controllers/AuthController.cs`, opciones de autenticación y `Program.cs`; `tests/Inventory.UnitTests/Security/`.
- **Paquetes:** se quita `Swashbuckle.AspNetCore.ApiTesting`. Se mantienen `Microsoft.AspNetCore.Authentication.JwtBearer 6.0.36` y `Swashbuckle.AspNetCore 6.5.0`. No se añade ningún paquete.
- **Infraestructura:** `docker-compose.yml` (servicio `keycloak` y variables de la API), `keycloak/inventory-realm.json` (nuevo) y `.env.example`.
- **Documentación:** `docs/architecture.md`, `docs/api-contract.md` y `docs/decisions.md` (los dos últimos aún no existen y se crean), `AGENTS.md` y `openspec/config.yaml`.
- **Fuera de alcance:** usuarios finales, roles, refresh tokens, HTTPS, Keycloak en modo producción y base de datos externa para Keycloak.
