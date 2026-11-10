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

## DEC-003 · SKU reservado para siempre

- **Fecha:** 2026-11-10
- **Estado:** Aceptada
- **Cambio de OpenSpec:** `add-products`

### Contexto

BR-01 pide un SKU único sin distinguir mayúsculas, y BR-06 hace que borrar un producto sea lógico: la fila y sus movimientos se conservan. Para las categorías se eligió lo contrario (DEC-002): su nombre queda libre al desactivarlas. Hacía falta decidir qué pasa con el SKU de un producto borrado.

### Decisión

El SKU es único entre **todos** los productos, activos o borrados. El SKU de un producto borrado no se puede reutilizar: crear otro con ese SKU responde 409, con el mismo mensaje que para un producto activo.

- `UX_Products_Sku` se mantiene **sin filtro**.
- `Products.Sku` declara la collation `SQL_Latin1_General_CP1_CI_AS`, para que "sin distinguir mayúsculas" no dependa de la instancia. `db/init.sql` la aplica también a las bases existentes (`design.md`, D7).

### Alternativas consideradas

| Alternativa | Por qué se descartó |
|---|---|
| **Índice filtrado `WHERE IsActive = 1`**, como el nombre de las categorías | Los movimientos del producto borrado siguen existiendo y el SKU es lo que lo identifica fuera del sistema. Dos productos con el mismo SKU, uno borrado y otro activo, harían ambiguo el historial. |
| **Comparar con `UPPER()` en la aplicación**, sin collation explícita | El índice único no protegería contra dos altas simultáneas con distintas mayúsculas si la instancia distinguiera mayúsculas. |

### Consecuencias

**Positivas**
- Un SKU identifica a un único producto durante toda la vida del inventario, también en el historial de movimientos.
- El índice único resuelve las altas simultáneas sin lógica adicional.

**Negativas y riesgos**
- Un SKU mal escrito y borrado queda inutilizable. Como no se puede reactivar un producto ni cambiar su SKU, la única salida es usar otro SKU.
- Si un volumen antiguo tuviera dos SKU que solo difieren en mayúsculas, la migración de la collation fallaría al recrear el índice. Hasta este cambio no había endpoint de productos, así que solo puede pasar con datos puestos a mano.

## DEC-004 · Borrado de productos con stock

- **Fecha:** 2026-11-10
- **Estado:** Aceptada
- **Cambio de OpenSpec:** `add-products`

### Contexto

BR-06 hace que borrar un producto sea lógico y conserve sus movimientos, pero no dice si se puede borrar un producto que todavía tiene existencias. Por BR-11, no se pueden registrar movimientos sobre un producto inactivo.

### Decisión

Se puede borrar un producto aunque tenga stock. `DELETE /api/products/{id}` responde 204 y el stock **queda congelado**: la fila conserva `StockQuantity` y sus movimientos, pero el producto ya no se puede consultar ni mover por la API.

### Alternativas consideradas

| Alternativa | Por qué se descartó |
|---|---|
| **Exigir stock 0 para borrar (409)** | Sería una regla nueva que BR-06 no pide, y obligaría a registrar una salida ficticia antes de retirar un producto del catálogo. |

### Consecuencias

**Positivas**
- Retirar un producto del catálogo no depende de su stock.
- El libro de movimientos sigue cuadrando con el `StockQuantity` guardado.

**Negativas y riesgos**
- El stock de un producto borrado no aparece en ningún endpoint. Si un informe futuro necesita el valor del inventario retirado, tendrá que leer los productos inactivos.
- Como no hay reactivación, las existencias de un producto borrado por error no se pueden volver a mover.
