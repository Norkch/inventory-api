# Design

## Context

- `Program.cs` tiene la plantilla por defecto: Swagger sin seguridad, `UseHttpsRedirection()` y `UseAuthorization()` sin autenticación.
- `docker-compose.yml` ya pasa a la API variables de la emisión propia que se descartó (`Auth__SigningKey`, `Auth__ClientId`, `Auth__ClientSecret`, `Auth__TokenLifetimeMinutes`). `docs/architecture.md` y `AGENTS.md` describen esa emisión propia.
- `Inventory.Api.csproj` ya referencia `Microsoft.AspNetCore.Authentication.JwtBearer 6.0.36` y `Swashbuckle.AspNetCore 6.5.0`. `Inventory.UnitTests` referencia solo `Inventory.Application`.
- Restricciones: .NET 6 y C# 10, ningún paquete nuevo, configuración solo por variables de entorno y una clase por archivo.

Motivación en `proposal.md` (Why); requisitos en `specs/api-auth` y `specs/dev-identity-provider`.

## Goals / Non-Goals

**Goals:**
- Que un clon limpio con `docker compose up` tenga Keycloak y la API listos para probar 401, 403 y 200 desde Swagger.
- Que la única lógica propia, la comprobación de un scope dentro del claim, esté cubierta con pruebas unitarias (TDD).

**Non-Goals:**
- Probar con pruebas automáticas la canalización de JwtBearer (firma, emisor, audiencia). Se verifica a mano con Docker, siguiendo la lista de `tasks.md`.
- Pruebas de integración con `WebApplicationFactory` (exigiría un paquete nuevo).
- Persistencia de Keycloak entre recreaciones del contenedor: el realm se reimporta en cada arranque.

## Decisions

### D1. Hostname y emisor de Keycloak
Imagen `quay.io/keycloak/keycloak` fijada a una versión 26.x, con `start-dev --import-realm`. Variables: `KC_HOSTNAME=http://localhost:8081` (URL completa, hostname v2) y `KC_HOSTNAME_BACKCHANNEL_DYNAMIC=true`. Puerto `127.0.0.1:8081:8080`.
- **Resultado:** el frontchannel (navegador, Swagger, `curl` desde el host) y el claim `iss` usan siempre `http://localhost:8081/realms/inventory`. Las peticiones que la API hace a `keycloak:8080` reciben una metadata con `jwks_uri` en `keycloak:8080`, que es accesible desde dentro de la red.
- **Alternativas descartadas:**
  - Sin `KC_HOSTNAME`: el `iss` cambia según quién pida el token (`localhost:8081` o `keycloak:8080`) y la validación del emisor falla.
  - Aceptar varios `ValidIssuers`: deja pasar dos emisores para resolver un problema de red.
  - `extra_hosts` o `network_mode: host`: no funciona igual en Windows, Linux y macOS.

### D2. Audiencia con un mapper en el cliente
Un protocol mapper `oidc-audience-mapper` en `inventory-client`, con `included.custom.audience = ${INVENTORY_API_AUDIENCE}` y `access.token.claim = true`. Compose pasa la variable a Keycloak con `${INVENTORY_API_AUDIENCE:-inventory-api}` y la misma variable al servicio `api` como `Auth__Audience`, de modo que la audiencia emitida y la validada no pueden quedar distintas (igual que `INVENTORY_CLIENT_ID`, D4).
- **Por qué en el cliente:** los scopes de inventario son opcionales. Si el mapper estuviera en ellos, un token pedido sin scopes llegaría sin `aud` y la API respondería 401 en vez de 403. Con el mapper en el cliente, todo token del cliente lleva la audiencia, y la falta de permisos se distingue (403) de un token ajeno (401).
- **Alternativa descartada:** un client scope por defecto con el mapper. Funciona igual, pero añade otro objeto al realm sin ningún beneficio.

### D3. Scopes opcionales y scopes por defecto mínimos
`inventory.read` e `inventory.write` son client scopes de tipo `openid-connect` con `include.in.token.scope = true`, asignados a `inventory-client` como **optional client scopes**. Como default client scopes, el cliente solo tiene `basic` (aporta `sub`), sin `profile`, `email`, `roles` ni `web-origins`.
- **Por qué:** el cliente elige el mínimo privilegio (pedir solo lectura). Si fueran scopes por defecto, todos los tokens tendrían escritura y el 403 no se podría ver.
- Quitar los scopes por defecto evita ruido en `whoami` y claims innecesarios en el token.

### D4. Secretos por variable de entorno sin valor por defecto
- En `inventory-realm.json`, `"secret": "${INVENTORY_CLIENT_SECRET}"`: la importación al arrancar sustituye los placeholders con variables de entorno (lo confirma la documentación de Keycloak 26).
- En `docker-compose.yml`, `INVENTORY_CLIENT_SECRET: ${INVENTORY_CLIENT_SECRET:?...}` para que compose falle si falta. Lo mismo para el administrador: `KC_BOOTSTRAP_ADMIN_USERNAME` y `KC_BOOTSTRAP_ADMIN_PASSWORD`.
- `.env.example` documenta las tres variables con valores de ejemplo `change-me-...`.
- **Alternativa descartada:** `${VAR:-valor}`, que dejaría un secreto real escrito en el repositorio.
- El id del cliente no es un secreto: `"clientId": "${INVENTORY_CLIENT_ID}"` en el realm y `INVENTORY_CLIENT_ID: ${INVENTORY_CLIENT_ID:-inventory-client}` en compose. El servicio `api` recibe la misma variable como `Auth__SwaggerClientId`, así que el realm y Swagger UI no pueden quedar desincronizados. El valor por defecto lo pone compose, porque la sintaxis `${VAR:defecto}` solo está documentada para `keycloak.conf`, no para la importación del realm.

### D5. Validación JwtBearer en la API
`AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer` con:
- `MetadataAddress = Auth__MetadataAddress` (`http://keycloak:8080/realms/inventory/.well-known/openid-configuration`)
- `RequireHttpsMetadata = false` (solo HTTP, porque HTTPS está fuera de alcance)
- `MapInboundClaims = false` (se leen `azp` y `scope` con su nombre original)
- `TokenValidationParameters`: `ValidIssuer = Auth__Issuer` y `ValidAudience = Auth__Audience`, validación de emisor, audiencia, tiempo de vida y firma activada, y `ClockSkew = 30 s`.

Sobre `ClockSkew`: el valor por defecto de 5 minutos alargaría la vida de un token de Keycloak (5 minutos por defecto) hasta unos 10. Con 30 s se tolera algo de desfase entre contenedores sin ocultar los tokens caducados. Es una constante del código, no una variable de configuración, y se registra en `docs/assumptions.md`.

Antes de implementar, consultar Context7 con JwtBearer 6 (`MapInboundClaims`, `RequireHttpsMetadata`, `TokenValidationParameters`).

### D6. Arranque que falla rápido
La clase `AuthOptions` (MetadataAddress, Issuer, Audience, SwaggerClientId) se enlaza a la sección `Auth`, con `[Required]` en cada propiedad, `ValidateDataAnnotations()` y `ValidateOnStart()`. Si falta una variable, la API falla al arrancar con el nombre de la opción en el mensaje. JwtBearer recibe los valores de `AuthOptions`, no de `IConfiguration` directamente.
- **Alternativa descartada:** comprobar a mano en `Program.cs`. Repite lógica que ya ofrece el sistema de opciones y alarga el método.

### D7. ScopeSet en Application, políticas en Api
- `Inventory.Application/Security/ScopeSet.cs`: se construye desde el valor del claim `scope` (puede ser nulo), separa por espacios ignorando vacíos, compara de forma ordinal (distingue mayúsculas) y expone `Contains(string scope)` y la lista de scopes. No depende de paquetes web.
- `Inventory.Application/Security/InventoryScopes.cs`: constantes `Read = "inventory.read"` y `Write = "inventory.write"`.
- `Inventory.Api/Authorization/ScopePolicies.cs`: nombres de política `InventoryRead` e `InventoryWrite`, y su registro con `RequireAuthenticatedUser()` + `RequireAssertion`, que crea un `ScopeSet` a partir del claim `scope` del usuario.
- **Por qué en Application:** es lógica pura que se prueba sin ASP.NET, y `Inventory.UnitTests` ya referencia Application. Las políticas son preocupación de borde y se quedan en Api.
- **Alternativa descartada:** `RequireClaim("scope", ...)`. Compara el valor completo del claim, y Keycloak envía todos los scopes en una sola cadena.

### D8. 401 y 403 con cuerpo vacío
Se mantiene el comportamiento por defecto de JwtBearer: 401 con `WWW-Authenticate: Bearer ...` y 403 sin cuerpo. No se personalizan `OnChallenge` ni `OnForbidden` y no se usa ProblemDetails para estos códigos. Se documenta en `docs/api-contract.md`.

### D9. Swagger con dos esquemas de seguridad
- `oauth2`: flujo `ClientCredentials`, `TokenUrl` = `{Auth__Issuer}/protocol/openid-connect/token` (la URL que ve el navegador) y scopes `inventory.read` e `inventory.write` con descripción en español.
- `Bearer`: esquema HTTP `bearer` con formato `JWT`, para pegar un token.
- Un requisito de seguridad global con ambos esquemas. Swagger UI deja rellenado el `client_id` con el valor de `Auth__SwaggerClientId` (en compose, `${INVENTORY_CLIENT_ID:-inventory-client}`), configurado con `ConfigureSwaggerUIOptions`, pero nunca el secreto.
- **CORS:** el navegador llama directamente al endpoint de token de Keycloak. `inventory-client` declara `webOrigins: ["http://localhost:8080"]`. Si falta, Authorize falla en silencio con un error de CORS.

### D10. Endpoints de /api/auth
`AuthController` (`[Authorize]` a nivel de clase, ruta `api/auth`):
- `whoami`: sin política de scope. Devuelve `WhoAmIResponse { clientId, scopes }`, con `clientId` del claim `azp` y `scopes` de `ScopeSet`.
- `check-read`: `[Authorize(Policy = InventoryRead)]`. Devuelve un mensaje en español.
- `check-write`: `[Authorize(Policy = InventoryWrite)]`. Devuelve un mensaje en español.

**Destino de los diagnósticos:** `check-read` y `check-write` se mantienen hasta cerrar el entregable 3. Se eliminan en la tarea de limpieza de ese cambio, cuando `POST /api/categories` permita verificar el 403 con un endpoint de negocio. `whoami` se queda.

### D11. Limpieza de la plantilla
Se eliminan `app.UseHttpsRedirection()` (el contenedor solo escucha HTTP en 8080) y el paquete `Swashbuckle.AspNetCore.ApiTesting`. Se añade `app.UseAuthentication()` antes de `UseAuthorization()`.

### D12. Orden de arranque en compose
`api` depende de `keycloak` con `condition: service_started`, sin healthcheck: la imagen de Keycloak no trae `curl` y el endpoint de salud requiere el puerto de gestión 9000. JwtBearer descarga la metadata la primera vez que valida un token y reintenta si falla, así que la API arranca aunque Keycloak no esté listo.

## Risks / Trade-offs

- [Keycloak tarda en arrancar (entre 20 y 40 s) y las primeras peticiones con token pueden fallar mientras no hay metadata] → El README indica esperar a que responda `/.well-known/openid-configuration`. JwtBearer reintenta solo.
- [La sustitución de placeholders o el "allowed system variables" de Keycloak 26.x cambia entre versiones menores] → Versión de imagen fijada. Una tarea verifica que el secreto importado funciona (401 `unauthorized_client` con un secreto incorrecto, 200 con el correcto). Keycloak 26.8.1 devuelve `unauthorized_client` y no `invalid_client` ante un secreto incorrecto (verificado en la tarea 3.4).
- [`azp` no aparece si se quita `basic`] → `basic` se mantiene por defecto. Una tarea de verificación decodifica el token y comprueba `aud`, `azp` y `scope`.
- [Error de CORS en Authorize si cambia el puerto de la API] → `webOrigins` documentado junto al puerto en `design.md` y en el README.
- [Sin pruebas automáticas de la canalización JWT] → Lista de verificación manual en `tasks.md` que cubre cada escenario 401/403/200 de los specs.
- [El realm se reimporta en cada arranque y se pierden los cambios hechos a mano en la consola] → Esperado en desarrollo: la fuente de verdad es `inventory-realm.json`.

## Migration Plan

1. Quitar de `docker-compose.yml` y `.env.example` las variables de la emisión propia y añadir las de Keycloak y `Auth__MetadataAddress`.
2. Cada desarrollador recrea su `.env` desde `.env.example`.
3. Rollback: revertir el merge. No hay datos persistentes involucrados.

## Open Questions

- Tag exacto de la imagen de Keycloak 26.x: se fija al implementar la tarea de Docker con la última 26.x estable y se registra en `docs/assumptions.md`. No cambia specs ni tareas.
