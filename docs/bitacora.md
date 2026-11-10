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

## E3 · Categorías — tarea 5.1 (`init.sql` idempotente, D11)
- **Fecha:** 09-11-2026
- **Cambio de OpenSpec:** `add-categories`
- **Entorno:** proyecto de compose aparte (`docker compose -p inv-verify ...`) con su propio volumen, para no borrar `inventory-api_sqldata`. El `sqlserver` principal se paró durante la prueba y se volvió a arrancar al final; el proyecto `inv-verify` se eliminó con `down -v`. Las consultas a `sys.columns`, `sys.indexes` y `sys.default_constraints` se ejecutaron con `sqlcmd` dentro del contenedor (el secreto se lee de la variable del contenedor y no se imprime).
- **Resultados:**

  | Escenario | `dbinit` | `Name` (collation) | `UX_Categories_Name` | Segunda ejecución de `init.sql` | Resultado |
  |---|---|---|---|---|---|
  | Volumen nuevo | 0 | `SQL_Latin1_General_CP1_CI_AS` | único, `has_filter = 1`, `([IsActive]=(1))` | sin cambios (instantánea idéntica) | OK |
  | Volumen anterior (`init.sql` de `main` + 2 filas) | 0 | `SQL_Latin1_General_CP1_CI_AS` | `has_filter = 1` (antes, sin filtro) | sin cambios | OK; las filas quedan con `IsActive = 1` |
  | Volumen anterior con `Name` en `Latin1_General_CS_AS` y el índice sin filtro | 0 | pasa de `Latin1_General_CS_AS` a `SQL_Latin1_General_CP1_CI_AS` | se elimina y se recrea con `has_filter = 1` | sin cambios | OK; ejercita la rama de `ALTER COLUMN` |
- **`dotnet build` / `dotnet test`:** 0 advertencias, 0 errores; 95/95 pruebas correctas.
- **Aprendí:** la imagen de SQL Server ya usa `SQL_Latin1_General_CP1_CI_AS` por defecto, así que con un volumen anterior normal el `ALTER COLUMN` no se ejecuta. Para probar esa rama hubo que forzar otra collation. Además, el índice debe eliminarse también cuando solo la collation es incorrecta, porque SQL Server no permite cambiar la collation de una columna indexada. En Git Bash, `docker exec` necesita `MSYS_NO_PATHCONV=1` para no reescribir las rutas `/opt/...`.

## E3 · Categorías — tarea 10.1 (verificación de extremo a extremo)
- **Fecha:** 10-11-2026 (UTC)
- **Cambio de OpenSpec:** `add-categories`
- **Entorno:** `docker compose up -d --build` (`dbinit` terminó con 0). Tokens de `inventory-client` pedidos con `curl` a `http://localhost:8081/realms/inventory/protocol/openid-connect/token` (`grant_type=client_credentials`, con `scope=inventory.read`, `inventory.write` o los dos). El secreto se lee de `.env` y no se imprime. Las peticiones usan `curl -s -X <método> -H "Authorization: Bearer $TOKEN" http://localhost:8080<ruta>`; los cuerpos se envían con `--data-binary @body.json` y `Content-Type: application/json`.
- **Datos previos:** había una categoría de pruebas anteriores (`Herramientas`) y ningún producto. Se desactivó con `DELETE /api/categories/{id}` (204), sin borrar la fila, para partir de cero categorías activas.
- **Aviso sobre `curl` en Windows:** un cuerpo con tildes pasado como argumento (`-d '{ "name": "Tornillería" }'`) llega a la API en la página de códigos ANSI y responde 400 "El cuerpo de la petición no es un JSON válido.". Es un problema del cliente, no de la API: enviando el cuerpo desde un archivo UTF-8 (`--data-binary @body.json`), todos los casos dan el resultado esperado.

### `specs/categories`

| Escenario | Petición | Resultado | ¿OK? |
|---|---|---|---|
| Alta correcta | `POST` `{ "name": "Tornillería", "description": "Tornillos y tuercas" }` (write) | 201, `createdAtUtc` con `Z`, `Location: /api/categories/f5f0df04-…` igual al `id` | Sí |
| Alta sin descripción | `POST` `{ "name": "Pinturas" }` | 201, `"description": null` | Sí |
| Token sin `inventory.write` | `POST` con token de solo read | 403, cuerpo vacío | Sí |
| Nombre con espacios en los extremos | `POST` `{ "name": "  Adhesivos  " }` | 201, `"name": "Adhesivos"` | Sí |
| Nombre ausente | `POST` `{ "description": "Sin nombre" }` | 400, "El nombre de la categoría es obligatorio." | Sí |
| Nombre solo con espacios | `POST` `{ "name": "     " }` | 400, "El nombre de la categoría es obligatorio." | Sí |
| Nombre de 100 caracteres | `POST` con 100 × `a` | 201 | Sí |
| Nombre de 101 caracteres | `POST` con `"  " + 101 × b + "  "` | 400, "… no puede superar los 100 caracteres." | Sí |
| Nombre repetido con otras mayúsculas | `POST` `{ "name": "TORNILLERÍA" }` | 409, "Ya existe una categoría con el nombre 'TORNILLERÍA'." | Sí |
| Nombre repetido con espacios | `POST` `{ "name": " Tornillería " }` | 409 | Sí |
| Altas simultáneas con el mismo nombre | 5 rondas de dos `POST` en paralelo (`&` + `wait`) con `"Pinturas 1"` … `"Pinturas 5"` | Las 5 rondas: `201 409`; una sola fila por nombre en la base | Sí |
| Índice como defensa final | `sqlcmd`: `SET QUOTED_IDENTIFIER ON; INSERT … N'PINTURAS 1' …, IsActive = 1` | `Msg 2601 … unique index 'UX_Categories_Name'` | Sí |
| Reutilizar el nombre de una desactivada | `DELETE` de `Tornillería` y luego `POST` `{ "name": "Tornillería" }` | 201 con `id` `8b72e81c-…`, distinto de `f5f0df04-…`; en la base, una fila inactiva y una activa | Sí |
| Descripción solo con espacios | `POST` `{ "name": "Electricidad", "description": "   " }` | 201, `"description": null` | Sí |
| Descripción de 501 caracteres | `POST` con `"  " + 501 × d + "  "` | 400, "La descripción de la categoría no puede superar los 500 caracteres." | Sí |
| Categoría activa por id | `GET /api/categories/{id}` (read) | 200, `"name": "Tornillería"` | Sí |
| Id inexistente (GET) | `GET /api/categories/3f2504e0-4f89-11d3-9a0c-0305e82c3301` | 404, "No existe una categoría activa con el id '…'." | Sí |
| Categoría desactivada (GET) | `GET` del `id` de `Tornillería` desactivada | 404 | Sí |
| Token sin `inventory.read` (GET por id) | `GET /api/categories/{id}` con token de solo write | 403, cuerpo vacío | Sí |
| Id que no es un GUID (D10) | `GET /api/categories/abc` y `DELETE /api/categories/abc` | 404 con el cuerpo vacío en los dos | Sí |
| Sin categorías | `GET /api/categories` con 0 activas | 200, `{"items":[],"page":1,"pageSize":20,"totalCount":0,"totalPages":0}` | Sí |
| Excluye las desactivadas | 11 activas y 1 desactivada (`Herramientas`) en la base | `totalCount` 11 y `Herramientas` no aparece en `items` | Sí |
| Orden por nombre | `GET /api/categories?pageSize=100` | `aaa…`, `Adhesivos`, `Electricidad`, `Jardinería`, `Pinturas`, `Pinturas 1` … `Pinturas 5`, `Tornillería` | Sí |
| Primera página por defecto | 25 activas, `GET /api/categories` | 200, 20 elementos, `page` 1, `pageSize` 20, `totalCount` 25, `totalPages` 2 | Sí |
| Página fuera de rango | `GET /api/categories?page=9&pageSize=20` | 200, `items` vacío, `page` 9, `totalCount` 25, `totalPages` 2 | Sí |
| `page=0` | `GET /api/categories?page=0` | 400, "El parámetro 'page' debe ser un número entero mayor o igual que 1." | Sí |
| `pageSize=0` | `GET /api/categories?pageSize=0` | 400, "El parámetro 'pageSize' debe ser un número entero entre 1 y 100." | Sí |
| `pageSize=101` | `GET /api/categories?pageSize=101` | 400, mismo mensaje | Sí |
| `pageSize=100` | `GET /api/categories?pageSize=100` | 200, `"pageSize":100` | Sí |
| Parámetro que no es un número | `GET /api/categories?page=abc` | 400, "El parámetro 'page' debe ser un número entero." | Sí |
| Desactivar sin productos | `DELETE` de `Tornillería` | 204; después `GET` por id da 404 y no aparece en el listado | Sí |
| Desactivar con un producto activo | `sqlcmd`: `INSERT dbo.Products` con 1 activo y 1 inactivo en `Con producto activo`; `DELETE` | 409, "No se puede eliminar la categoría porque tiene productos activos."; `GET` por id sigue dando 200 | Sí |
| Desactivar con solo productos inactivos | `sqlcmd`: 2 productos inactivos en `Solo productos inactivos`; `DELETE` | 204 | Sí |
| Categoría ya desactivada | `DELETE` otra vez de `Tornillería` | 404 con mensaje | Sí |
| Id inexistente (DELETE) | `DELETE /api/categories/3f2504e0-…` | 404, "No existe una categoría activa con el id '…'." | Sí |
| Token sin `inventory.write` (DELETE) | `DELETE` con token de solo read | 403, cuerpo vacío | Sí |

### `specs/api-errors`

| Escenario | Petición | Resultado | ¿OK? |
|---|---|---|---|
| Error de validación | `POST` `{ "name": "     " }` | 400, `application/problem+json`, `status` 400, `detail` en español | Sí |
| Recurso inexistente | `GET /api/categories/3f2504e0-…` | 404, `application/problem+json`, `status` 404 | Sí |
| Conflicto | `POST` `{ "name": "TORNILLERÍA" }` | 409, `application/problem+json`, `status` 409 | Sí |
| JSON mal formado | `POST` con el cuerpo `{ "name": ` | 400, "El cuerpo de la petición no es un JSON válido." | Sí |
| Parámetro con tipo incorrecto | `GET /api/categories?pageSize=diez` | 400, "El parámetro 'pageSize' debe ser un número entero." | Sí |
| Fallo de la base de datos | `docker compose stop sqlserver` y `GET /api/categories` | 500 tras unos 17 s, `detail` "Se produjo un error inesperado. Inténtalo de nuevo más tarde."; el cuerpo no contiene `exception`, `   at `, `sql`, `password` ni `server=`; el log de `api` registra el error de conexión de EF Core. Después, `docker compose start sqlserver` | Sí |
| Petición sin token | `POST /api/categories` sin `Authorization` | 401, cuerpo vacío | Sí |

Los 400 del dominio y de la paginación llegan con `application/problem+json`, y los de la fábrica de modelo inválido con `application/problem+json; charset=utf-8`. Los dos cumplen la especificación.

### `specs/api-auth` (MODIFIED)

| Escenario | Petición | Resultado | ¿OK? |
|---|---|---|---|
| Petición sin token | `GET /api/categories` sin `Authorization` | 401, cuerpo vacío | Sí |
| Token con firma inválida | Token con el primer carácter de la firma cambiado | 401, `error_description="The signature is invalid"` | Sí |
| Token caducado | Token de 300 s (`exp − iat`) usado a los 340 s | 200 recién emitido; 401 a los 340 s, `error_description="The token expired at '11/10/2026 04:05:55'"` | Sí |
| Token de otro emisor | Token del realm `master` (`admin-cli`, credenciales de `.env` sin imprimir), `iss=http://localhost:8081/realms/master` | 401, `error_description="The issuer 'http://localhost:8081/realms/master' is invalid"` | Sí |
| Token de otra audiencia | `api` recreada con `Auth__Audience=otra-api` (override temporal, `--no-deps --force-recreate`) | 401, `error_description="The audience 'inventory-api' is invalid"` | Sí |
| Solo lectura en un endpoint de lectura | `GET /api/categories` (read) | 200 | Sí |
| Solo lectura en un endpoint de escritura | `POST /api/categories` (read) | 403, cuerpo vacío | Sí |
| Solo escritura en un endpoint de lectura | `GET /api/categories` (write) | 403, cuerpo vacío | Sí |
| Token con ambos scopes | `POST` `{ "name": "Jardinería" }` (read + write) | 201 | Sí |
| Diagnóstico eliminado (REMOVED) | `GET /api/auth/check-read` | 404; `whoami` sigue dando 200 con `{"clientId":"inventory-client","scopes":["inventory.read"]}` | Sí |

- **Hallazgo:** con `api` recreada con `Auth__Issuer=http://otro-emisor:8081/realms/inventory`, un token del realm `inventory` respondió 200, no 401. JwtBearer acepta también el emisor de la metadata. Se registra en `docs/assumptions.md` y queda pendiente para un cambio aparte. Antes, la misma prueba dio 500 porque SQL Server aún estaba arrancando tras el `start`; repetida con la base lista, dio 200.
- Al terminar, `api` se recreó con la configuración normal (`docker compose up -d --no-deps --force-recreate api`): `GET /api/categories` dio 200.

### `specs/dev-identity-provider` (MODIFIED)

- **Comprobado con `curl`:** `swagger.json` publica `oauth2` con `clientCredentials`, el `tokenUrl` `http://localhost:8081/realms/inventory/protocol/openid-connect/token` y los dos scopes, además de la opción `Bearer`. Las rutas son `/api/auth/whoami`, `/api/categories` (`get`, `post`) y `/api/categories/{id}` (`get`, `delete`). El preflight `OPTIONS` al endpoint de token con `Origin: http://localhost:8080` responde 200 con `Access-Control-Allow-Origin: http://localhost:8080`.

| Escenario | Pasos en Swagger UI | Resultado | ¿OK? |
|---|---|---|---|
| Autorizar con client credentials | Authorize → `oauth2`, `inventory-client`, su secreto y solo `inventory.read`; `GET /api/categories` y `POST /api/categories` `{ "name": "Swagger" }` | `GET` 200 y `POST` 403 | Sí |
| Sin autorizar | Logout; `GET` y `POST /api/categories` | 401 en los dos | Sí |
| Pegar un token bearer | Token con `inventory.write` obtenido con `curl`, pegado en `Bearer`; `POST /api/categories` `{ "name": "Swagger Bearer" }` | 201 | Sí |

Los tres pasos en Swagger UI los hizo a mano la persona responsable del proyecto en el navegador, para que el secreto del cliente no pasara por el asistente. El token del tercer paso se generó con `curl`.

- **Aprendí:** el `curl.exe` de Windows no envía en UTF-8 los argumentos con tildes; hay que mandar el cuerpo desde un archivo. `sqlcmd` sin `-I` no activa `QUOTED_IDENTIFIER`, y entonces cualquier `INSERT` en `Categories` falla por el índice filtrado: la advertencia de `docs/data-model.md` es real. Y cambiar la configuración de la API es la forma de descubrir qué valida de verdad: el emisor de la metadata se acepta aunque `Auth__Issuer` diga otra cosa.

## E3 · Categorías — tarea 10.2 (revisión de Clean Code y cobertura)
- **Fecha:** 10-11-2026 (UTC)
- **Cambio de OpenSpec:** `add-categories`
- **Alcance revisado:** `git diff main -- src tests` (base `3260e24`, 42 archivos) más los 18 archivos nuevos sin seguimiento de `src` y `tests`.
- **Revisiones** (subagente `code-reviewer`, cada una con un contexto nuevo, sin el de la sesión que implementó):
  1. **Rechazada:** 1 hallazgo bloqueante y 2 menores, más un posible fallo sin verificar.
     - Bloqueante, regla 2: líneas en blanco entre la acción y el `Assert` en `DomainExceptionsTests` (3) y `GuardTests` (4). Se quitaron.
     - Menor, regla 7: `PagedResult.CountPagesRoundingUp` recibía `pageSize` suelto. Ahora recibe `PageRequest`, igual que `CategoryReadRepository.CalculateOffset`.
     - Menor, regla 5: las pruebas usaban cadenas de conexión parecidas a las reales (`Server=sqlserver;Database=InventoryDb`, `Password=secret`). Ahora usan `Server=test-server;Database=test-db` y `Password=not-a-real-password`.
     - **Fallo confirmado:** `POST /api/categories` con el cuerpo vacío o `null` respondía 500. ASP.NET Core registra ese error con la clave vacía del `ModelState`, y `DescribeInvalidParameter` hacía `key[0]` (`IndexOutOfRangeException` en el log de `api`). Corregido con TDD: primero `[InlineData("")]` en `InvalidModelStateResponseFactoryTests`, que falló con la misma excepción; después `IsRequestBodyKey`, que trata la clave vacía como un error del cuerpo. En vivo, tras `docker compose up -d --build --no-deps api`: cuerpo vacío → 400 y `null` → 400, los dos con "El cuerpo de la petición no es un JSON válido."; JSON mal formado y `pageSize=diez` siguen dando 400. Se añadió la fila al contrato (`docs/api-contract.md`).
  2. **Aprobada:** 0 hallazgos. Confirma las cuatro correcciones. Tres observaciones:
     - Los repositorios no tienen pruebas automáticas; ya lo acepta D13 y se verificaron en la tarea 10.1. Queda como propuesta un proyecto de pruebas de integración en un cambio futuro.
     - La redacción del supuesto de `PageRequest.Create` en `docs/assumptions.md` daba un motivo que no aplicaba. Se corrigió.
     - Errata `0- **WHEN**` en el escenario "Nombre de 100 caracteres" de `specs/categories`. Se corrigió. La contraseña por defecto de `SA_PASSWORD` en `docker-compose.yml` ya estaba anotada como fuera de alcance (viene de `main`).
- **`dotnet build` / `dotnet test`:** 0 advertencias, 0 errores; 115/115 pruebas correctas (114 + el caso de la clave vacía).
- **Cobertura con Coverlet** (`dotnet test --collect:"XPlat Code Coverage"`, `coverlet.collector` 3.1.2, formato Cobertura):

  | Ensamblado | Líneas | Ramas |
  |---|---|---|
  | `Inventory.Domain` | 100,0 % | 100,0 % |
  | `Inventory.Application` | 100,0 % | 100,0 % |
  | `Inventory.Api` | 68,8 % | 82,1 % |
  | `Inventory.Infrastructure` | 3,7 % | 0,0 % |
  | **Total** | **65,0 % (347/534)** | **74,2 %** |

  Lo que queda sin cubrir es composición o persistencia: `Program`, `CategoriesServiceCollectionExtensions` y `ConfigureSwaggerGenOptions` en Api, y `CategoryReadRepository`, `CategoryWriteRepository`, `InventoryReadDbContext`, `CategoryRecord`, `SqlConnectionFactory` y `SystemClock` en Infrastructure. D13 deja sus pruebas fuera de las unitarias y se verificaron con Docker en la tarea 10.1. Los controladores, handlers, middleware, la fábrica de modelo inválido, la paginación y las reglas de dominio están cubiertos.
- **Aprendí:** la primera revisión apuntó un posible fallo que no pudo ejecutar. Probarlo en vivo antes de decidir convirtió una sospecha en un 500 real, que ninguna de las pruebas de la 10.1 había tocado porque todos los casos de formato inválido llevaban algún cuerpo.

## E4 · Productos — tarea 5.1 (collation explícita de `Products.Sku`, D7)
- **Fecha:** 10-11-2026
- **Cambio de OpenSpec:** `add-products`
- **Entorno:** proyecto de compose aparte (`docker compose -p invverify ...`) con su propio volumen, para no tocar `inventory-api_sqldata`. El proyecto principal no estaba levantado. `sqlcmd` se ejecutó dentro del contenedor, con el secreto leído de la variable del contenedor y sin imprimirlo. Al final, `docker compose -p invverify down -v`.
- **Resultados:**

  | Escenario | `init.sql` | `Sku` (collation) | `UX_Products_Sku` | Segunda ejecución | Resultado |
  |---|---|---|---|---|---|
  | Volumen anterior (`init.sql` de `HEAD`), `Sku` forzado a `Latin1_General_CS_AS` y 1 producto `TOR-001` | 0 | pasa de `Latin1_General_CS_AS` a `SQL_Latin1_General_CP1_CI_AS` | se elimina y se recrea, `has_filter = 0` | código 0, sin cambios | OK; la fila se conserva, y un `INSERT` de `tor-001` falla con 2601 en `UX_Products_Sku` |
  | Volumen nuevo, servicio `dbinit` | 0 | `SQL_Latin1_General_CP1_CI_AS` | `has_filter = 0` | `dbinit` otra vez con código 0, sin cambios | OK |
- **`dotnet build` / `dotnet test`:** 0 advertencias, 0 errores; 179/179 pruebas correctas.
- **Aprendí:** igual que con `Categories.Name`, la rama de `ALTER COLUMN` solo se ejecuta si se fuerza otra collation, porque la imagen ya usa `SQL_Latin1_General_CP1_CI_AS`. El índice recreado debe seguir sin filtro: es lo que mantiene reservado el SKU de un producto borrado.

## E4 · Productos — tarea 9.1 (verificación de extremo a extremo)
- **Fecha:** 10-11-2026
- **Cambio de OpenSpec:** `add-products`
- **Entorno:** `docker compose up -d --build` (el `dbinit` aplicó el `init.sql` nuevo sobre el volumen `inventory-api_sqldata` con código 0). Tokens de `inventory-client` pedidos a Keycloak con `curl` para tres combinaciones de scopes (`read write`, solo `read`, solo `write`); el secreto se leyó de `.env` sin imprimirlo. Los datos de prueba llevan el sufijo `4002` en nombres y SKU. El stock y el movimiento del borrado con stock se pusieron a mano con `sqlcmd`, porque este cambio no tiene endpoint de movimientos.
- **Nota sobre los datos de prueba:** quedan en la base de desarrollo, incluido un movimiento de inventario de prueba (`RegisteredBy = 'e2e'`). Los movimientos son inmutables, así que no se borró; se elimina con `docker compose down -v`.

### `specs/products`

| Requisito / escenario | Petición | Resultado |
|---|---|---|
| Alta correcta | `POST` válido | 201, `stockQuantity` 0, `updatedAtUtc` `null`, `categoryName`, `createdAtUtc` con `Z`, `Location: /api/products/{id}` |
| Alta sin descripción, `stockQuantity: 50`, SKU con espacios | `"sku": "  TUE-4002  "`, `"stockQuantity": 50` | 201, `sku` recortado, `description` `null`, `stockQuantity` 0 |
| Token sin `inventory.write` / sin token | `POST` | 403 y 401, cuerpo vacío |
| SKU ausente / solo espacios | | 400 `El SKU del producto es obligatorio.` |
| SKU de 50 / 51 caracteres | | 201 / 400 `... no puede superar los 50 caracteres.` |
| SKU repetido con otras mayúsculas / con espacios | `tor-4002`, `" TOR-4002 "` | 409 `Ya existe un producto con el SKU '...'.` |
| SKU de un producto borrado | `STK-4002` tras borrarlo | 409 con el mismo mensaje |
| Altas simultáneas con el mismo SKU | 5 parejas de `POST` en paralelo | las 5 parejas dieron `[201 409]` |
| Nombre ausente / 200 / 201 / repetido | | 400 / 201 / 400 / 201 |
| Descripción solo espacios / 1000 / 1001 | | 201 con `null` / 201 / 400 |
| `categoryId` ausente / inexistente / desactivada | | 400 `La categoría del producto es obligatoria.` / 400 `La categoría indicada no existe o no está activa.` (los dos últimos) |
| `"categoryId": "abc"` en el cuerpo | | 400 `El cuerpo de la petición no es un JSON válido.` |
| Orden: nombre vacío + categoría inexistente | | 400 por el nombre |
| Orden: categoría inexistente + SKU repetido | | 400 por la categoría |
| Alta simultánea con la desactivación de la categoría | ver abajo | se serializan en los dos órdenes |
| `GET /{id}` activo / inexistente / borrado / no GUID / solo `write` | | 200 con `categoryName` / 404 / 404 / 404 sin cuerpo / 403 |
| Listado: orden, solo activos, `categoryName` | categoría con 6 productos | 200, orden `Arandela`, `Caja 500`, `Descuento 50%`, `Perno`, `Tornillo 3/8`, `Tuerca` |
| Listado sin parámetros | | `page` 1, `pageSize` 20 (16 productos activos en la base: `totalPages` 1) |
| Paginación | `pageSize=2&page=2` | 2 elementos, `totalCount` 6, `totalPages` 3 |
| `pageSize=101` / `page=abc` / solo `write` | | 400 sobre `pageSize` / 400 `El parámetro 'page' debe ser un número entero.` / 403 |
| Filtro por categoría / inexistente / `categoryId=abc` | | solo esa categoría / 200 vacío / 400 `El parámetro 'categoryId' debe ser un identificador (GUID) válido.` |
| `search=TORN` / `search=p-tor` (SKU `LP-TOR-4002`, nombre `Perno`) | | solo `Tornillo 3/8` / solo `Perno` |
| `search=50%` | con `Descuento 50%` y `Caja 500` | solo `Descuento 50%` |
| `search` solo espacios / combinado con `categoryId` | | los 6 / solo el de esa categoría |
| `PUT` correcto con `sku` y `stockQuantity` en el cuerpo | otra categoría, `description: null`, `"sku": "OTRO-4002"`, `"stockQuantity": 99` | 200, nueva categoría y `categoryName`, `description` `null`, `sku` y `stockQuantity` sin cambios, `updatedAtUtc` sin `Z` |
| `PUT` nombre vacío / categoría inexistente / categoría desactivada | | 400 con su mensaje; un `GET` posterior muestra el producto sin cambios |
| `PUT` id inexistente / producto borrado / solo `read` | | 404 / 404 / 403 |
| `DELETE` con stock 15 y un movimiento | | 204; la fila queda con `StockQuantity` 15, `IsActive` 0 y `UpdatedAtUtc` informado; el movimiento se conserva |
| `DELETE` ya borrado / inexistente / solo `read` | | 404 / 404 / 403 |

### BR-05 de `specs/categories`, ahora por la API

| Escenario | Resultado |
|---|---|
| Desactivar una categoría con un producto activo | 409 `No se puede eliminar la categoría porque tiene productos activos.` |
| Borrar su único producto y desactivarla | 204 y 204 |

### Concurrencia entre el alta y la desactivación de la categoría (D3)

- **Por la API:** una transacción de `sqlcmd` desactivó una categoría vacía y esperó 8 s antes del `COMMIT`. Un `POST` en esa categoría, lanzado 2 s después, esperó unos 7 s y respondió 400. La espera se produce ya en la comprobación previa (`FindActiveCategoryAsync`), porque con READ COMMITTED la lectura también espera a la fila bloqueada.
- **Con las sentencias de D3 directamente** (para comprobar el bloqueo de la sentencia, no el de la comprobación previa):
  - Desactivación confirmada primero (transacción abierta 6 s): el `INSERT ... WHERE EXISTS (... WITH (UPDLOCK, HOLDLOCK))` esperó unos 5 s e insertó **0 filas**.
  - Alta confirmada primero (transacción abierta 6 s): el `UPDATE` de `DeactivateAsync` esperó unos 5 s y desactivó **0 filas**. Después, `DELETE /api/categories/{id}` respondió 409.
- En ningún caso quedó un producto activo en una categoría desactivada.

### `specs/api-errors`

- `GET /api/categories?pageSize=diez` sigue respondiendo `El parámetro 'pageSize' debe ser un número entero.`, con `PaginationRequest` resuelto por `ActionParameterTypeResolver`.

- **`dotnet build` / `dotnet test`:** 0 advertencias, 0 errores; 208/208 pruebas correctas.
- **Aprendí:** la prueba con `sqlcmd` contra la API no bastaba para validar D3, porque la espera ocurría antes de llegar a la sentencia. Ejecutar las dos sentencias directamente, en los dos órdenes, fue lo que demostró que el `UPDLOCK, HOLDLOCK` las serializa.

## E4 · Productos — tarea 9.2 (revisión de Clean Code y cobertura)
- **Fecha:** 10-11-2026
- **Cambio de OpenSpec:** `add-products`
- **Alcance revisado:** `git diff -- src tests` (5 archivos modificados) más los 45 archivos nuevos de `src` y `tests`.
- **Revisión** (subagente `code-reviewer`, con un contexto nuevo, sin el de la sesión que implementó): **aprobada**, 0 hallazgos bloqueantes y 6 menores. La revisión confirma la separación CQRS y que las justificaciones de la regla 7 de `docs/assumptions.md` se sostienen.
  1. **Corregido, reglas 3 y 4:** `UpdateProductHandler` ocultaba el diagnóstico del rechazo detrás de `UpdateAsync(...) ?? await ThrowFor...()`, que declaraba `Task<Product>` sin devolver nunca nada. Ahora `UpdateWhileCategoryIsActiveAsync` guarda el resultado, diagnostica si es `null` y lo devuelve si no.
  2. **Corregido, regla 3:** `LikePattern.Contains` se leía como un predicado. Se renombró a `LikePattern.ForContains`, también en `design.md` y `tasks.md`.
  3. **Corregido, legibilidad:** el `new ProductSnapshot(...)` de `Product.Create` y de tres pruebas usa ahora argumentos con nombre.
  4. **Corregido con TDD, fallo real:** `ActionParameterTypeResolver` podía convertir un 400 en 500 (`AmbiguousMatchException` si dos propiedades solo difieren en mayúsculas). Además buscaba propiedades en parámetros simples (`string.Length` se habría tomado como entero) y no aceptaba claves con prefijo (`request.categoryId`). Primero se escribieron las 5 pruebas de `ActionParameterTypeResolverTests`: 3 fallaron. Después se corrigió: búsqueda sin ambigüedad, solo en parámetros complejos y quitando el prefijo. En vivo, tras `docker compose up -d --build --no-deps api`, `categoryId=abc`, `pageSize=diez` (categorías), `search=50%` y el `PUT` correcto y con categoría inexistente dan lo mismo que en la tarea 9.1.
  5. **Aceptado:** `ProductDeletion` vive en Application y `ProductChanges` en Domain (ver `docs/assumptions.md`).
  6. **Aceptado como deuda:** los escenarios que dependen de SQL Server (orden, filtros, búsqueda, collation, concurrencia) no tienen prueba automática. D11 lo decide así y la tarea 9.1 los verificó todos con Docker. Queda propuesto un proyecto de pruebas de integración para un cambio futuro, que exigirá aprobar un paquete nuevo.
- **`dotnet build` / `dotnet test`:** 0 advertencias, 0 errores; 213/213 pruebas correctas.
- **Cobertura con Coverlet** (`dotnet test --collect:"XPlat Code Coverage"`, formato Cobertura):

  | Ensamblado | Líneas | Ramas |
  |---|---|---|
  | `Inventory.Domain` | 100,0 % | 96,2 % |
  | `Inventory.Application` | 100,0 % | 100,0 % |
  | `Inventory.Api` | 73,9 % | 86,0 % |
  | `Inventory.Infrastructure` | 7,7 % | 11,1 % |
  | **Total** | **65,9 % (681/1033)** | **72,6 %** |

  Lo que queda sin cubrir es composición o persistencia: `Program`, las extensiones `AddPersistence`, `AddCategories` y `AddProducts`, `ConfigureSwaggerGenOptions`, y los repositorios Dapper y EF Core. Los repositorios se verificaron con Docker en la tarea 9.1. `LikePattern`, que es la única lógica propia de Infrastructure, está cubierta al 100 %.
- **Aprendí:** el resolutor de tipos funcionaba en todos los casos de la spec, pero la revisión encontró tres entradas que la spec no cubre y que podían dar un 500 o un mensaje equivocado. Una prueba por cada entrada rara salió más barata que razonar sobre todas.
