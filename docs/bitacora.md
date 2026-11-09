# Bitácora

## E1 · Proyecto, Docker y base de datos
- **Fecha:** [07-11-2026 – 07-11-2026]
- **Objetivo:** solución en cuatro capas, `docker compose up` funcional, base creada por `init.sql`.
- **Estado:** En curso
- **Cambio de OpenSpec:** ``
- **Hecho:**
- **Verificación:** [comandos ejecutados y resultado: `dotnet build`]
- **Problemas:** Validar que versiones descargar para los paquetes nugets. Problemas con sqlserver en docker debido a la lentitud del pc.
- **Aprendí:** Modificar valores de espera y reintentos en .yml
- **Evidencia:** ISSUE #9

## E2 · OAuth2 con Keycloak — tarea 3.4 (contenido del token)
- **Fecha:** 08-11-2026
- **Cambio de OpenSpec:** `add-keycloak-oauth`
- **Entorno:** `docker compose up -d --build` (Keycloak `26.8.1`). El primer intento falló en `dbinit` por *login timeout* de SQL Server; un segundo `docker compose up -d` terminó bien.
- **Comandos** (el secreto se lee de `.env` y no se imprime; el payload del JWT se decodifica con un script de Python):
  ```bash
  set -a && . ./.env && set +a
  URL=http://localhost:8081/realms/inventory/protocol/openid-connect/token
  curl -s -X POST $URL -d grant_type=client_credentials -d client_id=inventory-client \
    --data-urlencode "client_secret=$INVENTORY_CLIENT_SECRET" --data-urlencode "scope=inventory.read"
  # se repite con scope="inventory.read inventory.write", sin scope y con client_secret=wrong-secret
  ```
- **Resultados:**
  | Caso | iss | aud | azp | scope | Resultado |
  |---|---|---|---|---|---|
  | `scope=inventory.read` | `http://localhost:8081/realms/inventory` | `inventory-api` | `inventory-client` | `inventory.read` | OK |
  | `scope=inventory.read inventory.write` | igual | `inventory-api` | `inventory-client` | `inventory.read inventory.write` | OK |
  | sin scope | igual | `inventory-api` | `inventory-client` | `""` | OK |
  | secreto incorrecto | — | — | — | — | HTTP 401, `{"error":"unauthorized_client","error_description":"Invalid client or Invalid client credentials"}` → OK (spec ajustado, ver `docs/assumptions.md`) |
- **Metadata desde el contenedor `api`** (la imagen no trae `curl`, se usó `bash` con `/dev/tcp`):
  ```bash
  docker compose exec -T api bash -c 'exec 3<>/dev/tcp/keycloak/8080; printf "GET /realms/inventory/.well-known/openid-configuration HTTP/1.1\r\nHost: keycloak:8080\r\nConnection: close\r\n\r\n" >&3; cat <&3'
  ```
  `HTTP/1.1 200 OK`, `issuer = http://localhost:8081/realms/inventory`, `jwks_uri = http://keycloak:8080/realms/inventory/protocol/openid-connect/certs` → OK.
- **Problemas:** Keycloak 26.8.1 responde `unauthorized_client` (no `invalid_client`) ante un secreto incorrecto.
- **Decisión:** se ajustaron el spec `dev-identity-provider`, `design.md` y la tarea 3.4 para aceptar `unauthorized_client`, y se registró el supuesto en `docs/assumptions.md`.

## E2 · OAuth2 con Keycloak — tarea 8.1 (verificación desde un clon limpio)
- **Fecha:** 08-11-2026
- **Cambio de OpenSpec:** `add-keycloak-oauth`
- **Clon limpio:** como el cambio aún no está en un commit, se copió a un directorio nuevo solo lo que Git versionaría (`git ls-files -co --exclude-standard`): sin `.env`, `bin/`, `obj/` ni `.vs/`. Se siguió el README paso a paso.
- **Paso 1 (`.env`):** `cp .env.example .env` y se sustituyeron los valores `change-me-...` por valores aleatorios. Sin `INVENTORY_CLIENT_SECRET`, `docker compose config` falla con `required variable INVENTORY_CLIENT_SECRET is missing a value: set INVENTORY_CLIENT_SECRET in .env` → OK.
- **Paso 2 (`docker compose up -d --build`):**
  - SQL Server quedó `unhealthy`: el health check (timeout de 5 s) no respondía mientras SQL Server y Keycloak arrancaban a la vez. Docker tiene 3,7 GB de RAM y un `SELECT 1` llegó a tardar 55 s.
  - Cuando bajó la carga, el health check pasó y un segundo `docker compose up -d` levantó `dbinit` (exit 0) y `api`.
  - Después Docker dejó de responder durante unos minutos (los puertos agotaban el tiempo de espera y `docker compose exec` se colgaba). Se recuperó solo.
- **Paso 3 (Keycloak listo):** `.well-known/openid-configuration` → 200.
- **Pruebas con `curl`** (tokens pedidos a Keycloak con `client_credentials`; el secreto se lee de `.env` y no se imprime):
  | Caso | Petición | Resultado |
  |---|---|---|
  | Sin token | `GET /api/auth/check-read` | 401, cuerpo vacío, `WWW-Authenticate: Bearer` → OK |
  | Sin token | `GET /api/auth/check-write` | 401, cuerpo vacío → OK |
  | Solo `inventory.read` | `GET /api/auth/check-read` | 200, `{"message":"Permiso de lectura concedido: ..."}` → OK |
  | Solo `inventory.read` | `GET /api/auth/check-write` | 403, cuerpo vacío → OK |
  | `inventory.read inventory.write` | `GET /api/auth/check-write` | 200, `{"message":"Permiso de escritura concedido: ..."}` → OK |
  | Solo `inventory.read` | `GET /api/auth/whoami` | 200, `{"clientId":"inventory-client","scopes":["inventory.read"]}` → OK |
  | Ambos scopes | `GET /api/auth/whoami` | 200, `{"clientId":"inventory-client","scopes":["inventory.write","inventory.read"]}` → OK |
  | Firma alterada (un carácter cambiado) | `GET /api/auth/check-read` | 401, cuerpo vacío, `error="invalid_token", error_description="The signature is invalid"` → OK |
- **CORS para Swagger:** el preflight `OPTIONS` al endpoint de token con `Origin: http://localhost:8080` responde 200 con `Access-Control-Allow-Origin: http://localhost:8080` → OK.
- **Swagger UI (comprobado sin navegador):** `/swagger/index.html` → 200; `client_id` rellenado con `inventory-client` y sin secreto; `swagger.json` con los `securitySchemes` `oauth2` y `Bearer` y los tres endpoints → OK.
- **Token caducado:** se esperó a `exp` + 40 s (vida del token 300 s, tolerancia 30 s) → 401, cuerpo vacío, `error="invalid_token", error_description="The token expired at '...'"` → OK.
- **`dotnet build` / `dotnet test`:** 0 advertencias, 0 errores; 8/8 pruebas correctas.
- **Swagger UI con navegador (09-11-2026, Edge, mismo stack del clon limpio):**
  - Opera no sirvió: la extensión conectaba, pero fallaba al crear su grupo de pestañas (`No group with id`).
  - Docker se bloqueó (no podía parar contenedores) y Keycloak dejó de responder. Se reinició Docker Desktop (`docker desktop restart`) y se arrancó solo Keycloak; SQL Server quedó parado porque `api/auth` no usa la base de datos.
  - El diálogo Authorize muestra `client_id = inventory-client`, el secreto vacío y los scopes descritos en español.

  | Caso | Endpoint | Resultado |
  |---|---|---|
  | Sin autorizar | `check-read` / `check-write` | 401, `content-length: 0`, `www-authenticate: Bearer` → OK |
  | oauth2 con un secreto incorrecto | Authorize | `unauthorized_client`, "Invalid client or Invalid client credentials" → OK |
  | oauth2 solo con `inventory.read` | `check-read` | 200 → OK |
  | oauth2 solo con `inventory.read` | `check-write` | 403, cuerpo vacío → OK |
  | oauth2 solo con `inventory.read` | `whoami` | 200, `{"clientId":"inventory-client","scopes":["inventory.read"]}` → OK |
  | oauth2 con ambos scopes | `check-read` / `check-write` | 200 / 200 → OK |
  | oauth2 con ambos scopes | `whoami` | 200, `scopes: ["inventory.write","inventory.read"]` → OK |
  | Logout de oauth2 | `check-read` | 401 → OK |
  | Bearer pegado con el prefijo `Bearer ` | los tres | 401 `invalid_token` (la cabecera queda `Bearer Bearer ...`; el campo avisa de no ponerlo) → OK |
  | Bearer pegado ya caducado | los tres | 401 `invalid_token` → OK |
  | Bearer pegado válido (oauth2 cerrado; token con ambos scopes) | `check-read` / `check-write` / `whoami` | 200 / 200 / 200 → OK |
- **Aprendí:** el secreto que hay que escribir en Swagger es el del `.env` del stack que está corriendo; el `.env` del repositorio tenía otro valor y Keycloak lo rechazó. Además, si oauth2 y Bearer están autorizados a la vez no se sabe qué token envía Swagger; para probar el Bearer hay que cerrar antes la sesión de oauth2.
- **Aprendí:** en un equipo con poca memoria para Docker, el problema no es solo `dbinit`; SQL Server también puede quedar `unhealthy` si arranca a la vez que Keycloak. Se resuelve esperando y repitiendo `docker compose up -d`.

## E2 · OAuth2 con Keycloak — tarea 8.3 (id del cliente en `INVENTORY_CLIENT_ID`)
- **Fecha:** 09-11-2026
- **Cambio de OpenSpec:** `add-keycloak-oauth`
- **Contexto:** la revisión de Clean Code de la tarea 8.2 marcó `OAuthClientId("inventory-client")` como configuración escrita en el código (regla 5). El id se lee ahora de `INVENTORY_CLIENT_ID` (por defecto `inventory-client`), que alimenta tanto el `clientId` del realm como `Auth__SwaggerClientId`.
- **`docker compose config`:** sin la variable, `keycloak` y `api` reciben `inventory-client`; con `INVENTORY_CLIENT_ID=other-client`, los dos reciben `other-client` → OK.
- **Prueba en vivo** (solo `keycloak` y `api`, sin SQL Server: `docker compose up -d --build --no-deps --force-recreate keycloak api`; tokens con `client_credentials` y `scope=inventory.read`; el secreto se lee de `.env` y no se imprime):

  | `INVENTORY_CLIENT_ID` | `client_id` pedido | Token | Swagger `clientId` | `whoami` | Resultado |
  |---|---|---|---|---|---|
  | `other-client` | `other-client` | 200, `azp=other-client`, `aud=inventory-api`, `scope=inventory.read` | `other-client` | `{"clientId":"other-client","scopes":["inventory.read"]}` | OK |
  | `other-client` | `inventory-client` | 401 `invalid_client` | — | — | OK |
  | (sin definir) | `inventory-client` | 200, `azp=inventory-client`, `aud=inventory-api`, `scope=inventory.read` | `inventory-client` | `{"clientId":"inventory-client","scopes":["inventory.read"]}` | OK |
  | (sin definir) | `other-client` | 401 `invalid_client` | — | — | OK |
- **Aprendí:** si el cliente no existe en el realm, Keycloak responde `invalid_client`; si existe pero el secreto es incorrecto, responde `unauthorized_client`. Cambiar el id exige recrear Keycloak (`--force-recreate`), porque la importación se omite si el realm ya existe.

## E2 · OAuth2 con Keycloak — tarea 8.4 (audiencia en `INVENTORY_API_AUDIENCE`)
- **Fecha:** 09-11-2026
- **Cambio de OpenSpec:** `add-keycloak-oauth`
- **Contexto:** la revisión final de Clean Code de la tarea 8.2 señaló que `inventory-api` estaba escrito en dos sitios, el mapper de audiencia del realm y `Auth__Audience`. Si se cambiaba solo uno, todos los tokens daban 401. Ahora los dos salen de `INVENTORY_API_AUDIENCE` (por defecto `inventory-api`).
- **`docker compose config`:** sin la variable, `keycloak` y `api` reciben `inventory-api`; con `INVENTORY_API_AUDIENCE=other-api`, los dos reciben `other-api` → OK.
- **Prueba en vivo** (solo `keycloak` y `api`, `--no-deps --force-recreate`; token de `inventory-client` con `scope=inventory.read`; el secreto se lee de `.env` y no se imprime):

  | Audiencia en Keycloak | `Auth__Audience` en la API | `aud` del token | `whoami` | Resultado |
  |---|---|---|---|---|
  | `other-api` | `other-api` | `other-api` | 200, `{"clientId":"inventory-client","scopes":["inventory.read"]}` | OK |
  | `other-api` | `inventory-api` (solo se recreó `api`) | `other-api` | 401 | OK: la API rechaza una audiencia distinta |
  | `inventory-api` (por defecto) | `inventory-api` (por defecto) | `inventory-api` | 200 | OK |
- Al terminar se pararon `keycloak` y `api` (`docker compose stop`) para no agotar la memoria del equipo.
- **Aprendí:** el 401 con la audiencia desincronizada confirma por qué conviene una sola variable para las dos piezas.

## E2 · OAuth2 con Keycloak — tarea 8.2 (validación y revisión de Clean Code)
- **Fecha:** 09-11-2026
- **Cambio de OpenSpec:** `add-keycloak-oauth`
- **`openspec validate add-keycloak-oauth --strict`:** válido.
- **Revisiones de Clean Code** (`clean-code-review`, cada una en una sesión nueva):
  1. Rechazada: 3 hallazgos bloqueantes (`OAuthClientId` fijo, faltaban pruebas automáticas, `if` vacío en `Program.cs`) y 3 menores. Se quitó el código muerto y se añadieron pruebas de `AuthOptions`, de la validación de JWT, de las políticas y de `AuthController`, con fakes escritos a mano y sin paquetes nuevos.
  2. Rechazada: 1 bloqueante (`OAuthClientId` fijo). Se pasó a `Auth__SwaggerClientId` y luego a `INVENTORY_CLIENT_ID` (tarea 8.3).
  3. Aprobada, con 4 hallazgos menores: se ampliaron las pruebas de `AuthOptions` a las cuatro opciones, ausentes y vacías; la validación de prueba aplica `MapInboundClaims`; se corrigió `docs/assumptions.md`; y la audiencia pasó a `INVENTORY_API_AUDIENCE` (tarea 8.4).
- **Hallazgos aceptados en `docs/assumptions.md`:** `RequireHttpsMetadata = false` fijo, líneas en blanco en `Program.cs`, constantes `scope`/`azp` repetidas, y la verificación manual del cuerpo vacío de 401/403 y del arranque completo (requerirían `Microsoft.AspNetCore.TestHost`).
- **Fuera de alcance, para otro cambio:** `SA_PASSWORD` tiene un valor por defecto en `docker-compose.yml`; viene de `main`.
- **`dotnet build` / `dotnet test`:** 0 advertencias, 0 errores; 42/42 pruebas correctas.
- **Aprendí:** con Docker en marcha y poca memoria, `dotnet test` se colgaba antes de ejecutar ninguna prueba y un contenedor quedó zombi. Cerrando Docker, las pruebas pasaron en menos de un segundo.
