# Registro de decisiones

Decisiones de arquitectura del proyecto. Cada entrada tiene fecha, contexto, decisión, alternativas y consecuencias. Las entradas no se reescriben: si una decisión cambia, se añade otra nueva que la sustituye.

## DEC-001 · OAuth2 con Keycloak como servidor de identidad

- **Fecha:** 2026-11-08
- **Estado:** Aceptada
- **Cambio de OpenSpec:** `add-keycloak-oauth`
- **Sustituye a:** la emisión propia de tokens que describían `docs/architecture.md` y `AGENTS.md` hasta el 2026-11-07

### Contexto

La API tiene que proteger sus endpoints con OAuth2 `client_credentials` antes de exponer los endpoints de negocio. Los clientes son servicios, no usuarios finales, y los permisos se expresan con dos scopes: `inventory.read` e `inventory.write`.

El plan inicial era que la API emitiera sus propios JWT HS256, firmados con una clave compartida (`Auth__SigningKey`). Para eso la API tenía que guardar las credenciales de los clientes (`Auth__ClientId`, `Auth__ClientSecret`), exponer un endpoint de token, decidir la duración de los tokens (`Auth__TokenLifetimeMinutes`) y devolver los errores de OAuth2 (`invalid_client`, `invalid_scope`, `unsupported_grant_type`). Es decir, la API iba a ser a la vez servidor de autorización y servidor de recursos.

También se quería probar todo el flujo desde Swagger UI, sin herramientas externas.

### Decisión

Keycloak emite los tokens y la API solo los valida.

- **Keycloak 26.8.1** corre en Docker en modo desarrollo e importa al arrancar el realm `inventory` desde `keycloak/inventory-realm.json`.
- **Cliente:** `inventory-client` es confidencial y solo usa `client_credentials`. Tiene `inventory.read` e `inventory.write` como scopes opcionales y un mapper que añade `aud: inventory-api` a todos sus tokens.
- **Secreto:** el del cliente llega por la variable `INVENTORY_CLIENT_SECRET`. No está escrito en el repositorio.
- **Validación en la API:** JwtBearer comprueba la firma con las claves de la metadata (`Auth__MetadataAddress`), el emisor (`Auth__Issuer`), la audiencia (`Auth__Audience`) y la caducidad, con 30 s de tolerancia. Los scopes se comprueban con `ScopeSet` y las políticas `InventoryRead` e `InventoryWrite`.
- **Swagger UI:** autoriza directamente contra el endpoint de token de Keycloak.

Los detalles (hostname, audiencia, scopes opcionales, 401 y 403 con el cuerpo vacío) están en `openspec/changes/add-keycloak-oauth/design.md`, decisiones D1 a D12.

### Alternativas consideradas

| Alternativa | Por qué se descartó |
|---|---|
| **Emisión propia con HS256** (el plan inicial) | La API tendría que guardar credenciales, firmar, emitir tokens y devolver los errores de OAuth2. Con una clave simétrica, quien puede validar un token también puede crearlo. Es código de seguridad propio que hay que mantener y probar. |
| **Servidor de identidad en la nube** (un proveedor gestionado) | El entorno ya no se levantaría solo con `docker compose up`: cada desarrollador necesitaría una cuenta, conexión a internet y configuración fuera del repositorio. |

### Consecuencias

**Positivas**
- La API tiene una sola responsabilidad en seguridad: validar tokens y comprobar scopes. No guarda secretos de clientes ni claves de firma.
- La firma es asimétrica: la API solo tiene la clave pública (de `jwks_uri`) y no puede emitir tokens.
- La única lógica propia es `ScopeSet`, que es pura y está cubierta con pruebas unitarias.
- Todo el flujo se prueba desde Swagger UI con el botón Authorize.
- El realm está versionado en `keycloak/inventory-realm.json`, así que un clon limpio tiene el mismo entorno.

**Negativas y riesgos**
- **Otro servicio:** hay un contenedor más que tarda entre 20 y 40 s en arrancar. Las primeras peticiones con token pueden fallar hasta que Keycloak responde. JwtBearer reintenta la descarga de la metadata.
- **Hostname:** hay que configurar el hostname con cuidado (`KC_HOSTNAME` y `KC_HOSTNAME_BACKCHANNEL_DYNAMIC`) para que `iss` sea siempre `http://localhost:8081/realms/inventory`.
- **Errores de Keycloak:** los errores del flujo OAuth2 los define Keycloak, no la API. Por ejemplo, con un secreto incorrecto Keycloak 26.8.1 responde `unauthorized_client` y no `invalid_client` (ver `docs/assumptions.md`).
- **Sin persistencia:** Keycloak no tiene volumen de datos. Los cambios hechos a mano en su consola se pierden al recrear el contenedor; la fuente de verdad es el JSON del realm.
- **Cambio de configuración (rompe compatibilidad):** desaparecen `Auth__SigningKey`, `Auth__ClientId`, `Auth__ClientSecret`, `Auth__TokenLifetimeMinutes`, `AUTH_SIGNING_KEY`, `AUTH_CLIENT_ID` y `AUTH_CLIENT_SECRET`. Aparecen `Auth__MetadataAddress`, `Auth__Issuer`, `Auth__Audience`, `Auth__SwaggerClientId`, `KC_BOOTSTRAP_ADMIN_USERNAME`, `KC_BOOTSTRAP_ADMIN_PASSWORD`, `INVENTORY_CLIENT_ID` (opcional, por defecto `inventory-client`), `INVENTORY_API_AUDIENCE` (opcional, por defecto `inventory-api`) e `INVENTORY_CLIENT_SECRET`. Cada desarrollador tiene que recrear su `.env` desde `.env.example`.
- **Solo desarrollo:** el modo de desarrollo de Keycloak, sin HTTPS, no sirve para producción. Producción está fuera del alcance de este cambio.
