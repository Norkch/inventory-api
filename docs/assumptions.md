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
