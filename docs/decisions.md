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

## DEC-002 · Borrado lógico de categorías

- **Fecha:** 2026-11-09
- **Estado:** Aceptada
- **Cambio de OpenSpec:** `add-categories`
- **Sustituye a:** la redacción anterior de BR-05 en `docs/business-rules.md` ("No se puede eliminar una categoría con productos activos ni inactivos")

### Contexto

`DELETE /api/categories/{id}` tiene que retirar una categoría que ya no se usa. Los productos (BR-03) apuntan a su categoría por `CategoryId`, y un producto eliminado no se borra: queda inactivo (BR-06) y conserva sus movimientos. La base no tiene llaves foráneas, así que la integridad entre productos y categorías la garantiza la aplicación.

Si una categoría se borrara de verdad, los productos inactivos que la usan quedarían apuntando a una fila que no existe. Si, para evitarlo, cualquier producto bloqueara el borrado, una categoría con un solo producto retirado no se podría eliminar nunca, porque los productos tampoco se borran.

### Decisión

Eliminar una categoría es un **borrado lógico**: `IsActive` pasa a `false` y la fila se conserva.

- Solo se permite si la categoría no tiene **productos activos**. Los productos inactivos no lo impiden.
- La comprobación y la desactivación van en una sola sentencia `UPDATE ... WHERE Id = @Id AND IsActive = 1 AND NOT EXISTS (productos activos)`, para que no se pueda crear un producto activo entre los dos pasos (`design.md`, D4).
- `DELETE` responde 204 si la desactiva, 404 si no existe o ya estaba desactivada, y 409 con un mensaje en español si tiene productos activos.
- Una categoría desactivada no aparece en `GET /api/categories`, y `GET /api/categories/{id}` responde 404.
- No hay endpoint para reactivarla.

Los detalles están en `openspec/changes/add-categories/design.md`, decisiones D3 y D4.

### Alternativas consideradas

| Alternativa | Por qué se descartó |
|---|---|
| **Borrado físico** (`DELETE` de la fila) | Los productos inactivos, que se conservan con sus movimientos, quedarían apuntando a una categoría que no existe. Sin llaves foráneas, nada en la base lo impediría. Además, se perdería el historial de qué categoría tenía cada producto. |
| **Bloquear el borrado con cualquier producto, activo o inactivo** (la redacción anterior de BR-05) | Como los productos tampoco se borran, una categoría que haya tenido un solo producto no se podría eliminar nunca. |

### Consecuencias

**Positivas**
- Ningún producto apunta a una categoría que no existe, aunque no haya llaves foráneas.
- Se conserva el historial: un producto inactivo sigue sabiendo a qué categoría pertenecía.
- El borrado es atómico y no compite con el alta de productos.

**Negativas y riesgos**
- **Índice filtrado:** el nombre debe ser único solo entre las categorías activas, así que `UX_Categories_Name` pasa a ser un índice filtrado (`WHERE IsActive = 1`). Los índices filtrados exigen `QUOTED_IDENTIFIER ON` en las sesiones que modifican la tabla (ver `docs/data-model.md`).
- **Nombre reutilizable:** al desactivar una categoría, su nombre queda libre. Se puede crear otra con el mismo nombre y un `id` distinto, así que puede haber varias filas con el mismo nombre (una activa como mucho).
- **Filtro en todas las lecturas:** cada consulta de categorías tiene que filtrar `IsActive = 1`, y el alta de productos (BR-03) tiene que comprobar que la categoría existe **y está activa**.
- **Migración:** `Categories` gana la columna `IsActive`, y `db/init.sql` la añade también a las bases que ya existían (`design.md`, D11).
