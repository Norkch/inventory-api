# Contrato de la API

Contrato HTTP de la API de inventario. Cada sección indica el cambio de OpenSpec que la define.

## Autenticación (`add-keycloak-oauth`)

### Cómo se obtiene un token

La API no emite tokens: solo los valida. Los tokens los emite Keycloak con el flujo OAuth2 `client_credentials`.

```bash
curl -s -X POST http://localhost:8081/realms/inventory/protocol/openid-connect/token \
  -d grant_type=client_credentials \
  -d client_id=inventory-client \
  -d client_secret=<INVENTORY_CLIENT_SECRET> \
  --data-urlencode "scope=inventory.read inventory.write"
```

El token se envía en cada petición con la cabecera `Authorization: Bearer <token>`.

| Scope | Concede |
|---|---|
| `inventory.read` | Endpoints de lectura |
| `inventory.write` | Endpoints de escritura |

Los dos scopes son opcionales: el token solo lleva los que se pidieron en `scope`.

### Configuración obligatoria

La API lee la configuración de autenticación solo de estas variables de entorno:

| Variable | Valor en `docker-compose.yml` |
|---|---|
| `Auth__MetadataAddress` | `http://keycloak:8080/realms/inventory/.well-known/openid-configuration` |
| `Auth__Issuer` | `http://localhost:8081/realms/inventory` |
| `Auth__Audience` | `${INVENTORY_API_AUDIENCE:-inventory-api}` |
| `Auth__SwaggerClientId` | `${INVENTORY_CLIENT_ID:-inventory-client}` (id que Swagger UI deja rellenado; no es un secreto) |

Si falta alguna variable o está vacía, la API no arranca. El error nombra la opción que falta, por ejemplo: `DataAnnotation validation failed for 'AuthOptions' members: 'Audience' with the error: 'The Audience field is required.'`. Con las cuatro variables informadas, la API arranca y atiende peticiones.

### Validación del token

Toda ruta bajo `/api` exige un token bearer válido. Un token es válido cuando cumple todo esto:

- La firma se verifica con las claves que publica el emisor (`jwks_uri` de la metadata).
- `iss` es igual a `Auth__Issuer`.
- `aud` contiene `Auth__Audience`.
- No ha caducado. Se tolera un desfase de reloj de 30 s después de `exp`.

Los claims se leen con el nombre que traen en el token, sin renombrarlos: `azp` es el id del cliente y `scope` es la lista de scopes.

### Comprobación de scopes

El claim `scope` es una cadena con los scopes separados por espacios. Un scope está concedido solo si aparece como elemento completo de esa lista. La comparación distingue mayúsculas y minúsculas.

| Claim `scope` | Scope comprobado | ¿Concedido? |
|---|---|---|
| `"profile inventory.read email"` | `inventory.read` | Sí |
| `"inventory.readonly"` | `inventory.read` | No (coincidencia parcial) |
| `"Inventory.Read"` | `inventory.read` | No (mayúsculas distintas) |
| `"  inventory.read   inventory.write "` | `inventory.write` | Sí (se ignoran los espacios repetidos y los de los extremos) |
| vacío o ausente | `inventory.read` | No |

### Respuestas 401 y 403

La API devuelve 401 y 403 **con el cuerpo vacío**. No usa ProblemDetails para estos códigos.

| Código | Cuándo | Cuerpo |
|---|---|---|
| 401 Unauthorized | Falta la cabecera `Authorization`, la firma no es válida, el token caducó, `iss` es de otro emisor o `aud` no contiene la audiencia configurada | Vacío. La cabecera `WWW-Authenticate: Bearer ...` indica el motivo |
| 403 Forbidden | El token es válido, pero no tiene el scope que exige el endpoint | Vacío |

Una regla rápida para diferenciarlos: 401 significa que la API no acepta el token, y 403 que lo acepta pero el token no tiene el permiso.

### Endpoints

| Método y ruta | Scope exigido | Éxito | Sin token o token inválido | Token sin el scope |
|---|---|---|---|---|
| `GET /api/auth/whoami` | Ninguno (basta un token válido) | 200 | 401 | No aplica |

Los scopes `inventory.read` e `inventory.write` se exigen en los endpoints de negocio, por ejemplo en los de [categorías](#categorías-add-categories).

#### `GET /api/auth/whoami`

Devuelve la identidad del cliente que llama. `clientId` viene del claim `azp` y `scopes`, del claim `scope`, ya separado en una lista y sin elementos vacíos.

Respuesta 200 con un token pedido con `scope=inventory.read`:

```json
{
  "clientId": "inventory-client",
  "scopes": ["inventory.read"]
}
```

Con un token sin scopes de inventario, la respuesta también es 200, y `scopes` no incluye ni `inventory.read` ni `inventory.write`:

```json
{
  "clientId": "inventory-client",
  "scopes": []
}
```

## Formato de errores (`add-categories`)

### ProblemDetails para 400, 404, 409 y 500

Los errores 400, 404, 409 y 500 se responden con `Content-Type: application/problem+json` (ASP.NET Core añade `; charset=utf-8`) y un cuerpo [ProblemDetails](https://www.rfc-editor.org/rfc/rfc7807) con estos campos:

| Campo | Contenido |
|---|---|
| `type` | URI de la sección de RFC 7231 que define el código |
| `title` | Resumen del tipo de error, en español |
| `status` | El mismo código HTTP de la respuesta |
| `detail` | Explicación del error concreto, en español |

| Código | `title` | Cuándo |
|---|---|---|
| 400 Bad Request | `La petición no es válida.` | Se incumple una regla de validación, el cuerpo no es un JSON válido o un parámetro no tiene el tipo esperado |
| 404 Not Found | `Recurso no encontrado.` | El recurso pedido no existe |
| 409 Conflict | `Conflicto con el estado actual del recurso.` | La petición choca con el estado actual, por ejemplo un nombre repetido |
| 500 Internal Server Error | `Error interno del servidor.` | Error no previsto |

#### 400: regla de validación incumplida

`POST /api/categories` con `{ "name": "   " }`:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "La petición no es válida.",
  "status": 400,
  "detail": "El nombre de la categoría es obligatorio."
}
```

#### 400: JSON mal formado

`POST /api/categories` con el cuerpo `{ "name": `:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "La petición no es válida.",
  "status": 400,
  "detail": "El cuerpo de la petición no es un JSON válido."
}
```

#### 400: parámetro con tipo incorrecto

`GET /api/categories?pageSize=diez`. El `detail` nombra el parámetro tal como lo escribe el cliente:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "La petición no es válida.",
  "status": 400,
  "detail": "El parámetro 'pageSize' debe ser un número entero."
}
```

Los mensajes de formato del framework (en inglés) no llegan nunca al cliente.

#### 404: recurso inexistente

`GET /api/categories/3f2504e0-4f89-11d3-9a0c-0305e82c3301` sin ninguna categoría activa con ese id:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Recurso no encontrado.",
  "status": 404,
  "detail": "No existe una categoría activa con el id '3f2504e0-4f89-11d3-9a0c-0305e82c3301'."
}
```

#### 409: conflicto

`POST /api/categories` con un nombre que ya usa otra categoría activa:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.8",
  "title": "Conflicto con el estado actual del recurso.",
  "status": 409,
  "detail": "Ya existe una categoría con el nombre 'Tornillería'."
}
```

#### 500: error inesperado

Por ejemplo, cuando la base de datos no responde mientras se atiende `GET /api/categories`:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "Error interno del servidor.",
  "status": 500,
  "detail": "Se produjo un error inesperado. Inténtalo de nuevo más tarde."
}
```

El `detail` es siempre el mismo. La respuesta no incluye el mensaje de la excepción, la traza de pila, sentencias SQL ni cadenas de conexión. La excepción completa solo se escribe en el log de la API.

### 401 y 403 sin ProblemDetails

Las respuestas 401 y 403 se mantienen **con el cuerpo vacío**, tal como se describe en [Respuestas 401 y 403](#respuestas-401-y-403). Por ejemplo, `POST /api/categories` sin cabecera `Authorization` responde 401 sin cuerpo. Estos códigos los producen la autenticación y la autorización sin lanzar excepciones, así que no pasan por el formato ProblemDetails.

## Categorías (`add-categories`)

Las categorías agrupan los productos. Solo se pueden crear, consultar y desactivar: no hay endpoint para editarlas (sin `PUT`) ni para reactivarlas.

### Endpoints

| Método y ruta | Scope exigido | Éxito | Errores con ProblemDetails | Sin token o token inválido | Token sin el scope |
|---|---|---|---|---|---|
| `POST /api/categories` | `inventory.write` | 201 | 400, 409 | 401 | 403 |
| `GET /api/categories?page&pageSize` | `inventory.read` | 200 | 400 | 401 | 403 |
| `GET /api/categories/{id}` | `inventory.read` | 200 | 404 | 401 | 403 |
| `DELETE /api/categories/{id}` | `inventory.write` | 204 | 404, 409 | 401 | 403 |

- 401 y 403 llegan con el cuerpo vacío (ver [Respuestas 401 y 403](#respuestas-401-y-403)). Un token con solo `inventory.read` recibe 403 en `POST` y `DELETE`, y uno con solo `inventory.write` recibe 403 en los dos `GET`.
- Los errores 400, 404 y 409 siguen el [formato de errores](#formato-de-errores-add-categories).
- Los nombres de los campos JSON van en camelCase.

### Representación de una categoría

```json
{
  "id": "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
  "name": "Tornillería",
  "description": "Tornillos y tuercas",
  "createdAtUtc": "2026-11-09T10:15:30.1234567Z"
}
```

| Campo | Tipo | Contenido |
|---|---|---|
| `id` | GUID | Lo genera la API al crear la categoría |
| `name` | string | Nombre recortado, de 1 a 100 caracteres |
| `description` | string o `null` | Descripción recortada, de 1 a 500 caracteres, o `null` si no se envió o solo tenía espacios |
| `createdAtUtc` | fecha y hora ISO 8601 | Momento del alta, en UTC, fijado por la API |

Las respuestas solo devuelven categorías activas, así que no incluyen `isActive`.

`createdAtUtc` siempre está en UTC. La respuesta 201 lo escribe con el sufijo `Z`; las lecturas (`GET`) lo escriben sin sufijo, porque la columna `datetime2` no guarda la zona (ver `docs/assumptions.md`).

### `POST /api/categories`

Crea una categoría activa. Cuerpo de la petición:

```json
{
  "name": "Tornillería",
  "description": "Tornillos y tuercas"
}
```

| Campo | Obligatorio | Reglas (BR-04) |
|---|---|---|
| `name` | Sí | Se recortan los espacios de los extremos. Después de recortarlo no puede quedar vacío ni superar los 100 caracteres. Es único entre las categorías activas |
| `description` | No | Se recortan los espacios de los extremos. Vacía o solo con espacios se guarda como `null`. Después de recortarla no puede superar los 500 caracteres |

Respuesta 201 con la categoría creada (ver la [representación](#representación-de-una-categoría)) y la cabecera `Location: /api/categories/{id}`, con el mismo `id` del cuerpo.

| Cuerpo enviado | Respuesta |
|---|---|
| `{ "name": "Tornillería", "description": "Tornillos y tuercas" }` | 201 con los dos valores tal cual |
| `{ "name": "Tornillería" }` | 201 con `"description": null` |
| `{ "name": "  Tornillería  " }` | 201 con `"name": "Tornillería"` |
| `{ "name": "Tornillería", "description": "   " }` | 201 con `"description": null` |
| Nombre de exactamente 100 caracteres | 201 |
| `{ "description": "Sin nombre" }` | 400: `El nombre de la categoría es obligatorio.` |
| `{ "name": "     " }` | 400: `El nombre de la categoría es obligatorio.` |
| Nombre de 101 caracteres después de recortarlo | 400: `El nombre de la categoría no puede superar los 100 caracteres.` |
| Descripción de 501 caracteres después de recortarla | 400: `La descripción de la categoría no puede superar los 500 caracteres.` |
| Cuerpo que no es un JSON válido | 400: `El cuerpo de la petición no es un JSON válido.` |
| Cuerpo vacío o `null` | 400: `El cuerpo de la petición no es un JSON válido.` |

#### Unicidad y reutilización del nombre (BR-04)

- El nombre es único **entre las categorías activas**, sin distinguir mayúsculas. Se compara después de recortarlo, así que `"TORNILLERÍA"` y `" Tornillería "` chocan con una categoría activa `"Tornillería"`.
- Sí distingue acentos: `"Tornilleria"` y `"Tornillería"` son nombres distintos.
- Un nombre repetido responde 409. El `detail` cita el nombre enviado, ya recortado:

  ```json
  {
    "type": "https://tools.ietf.org/html/rfc7231#section-6.5.8",
    "title": "Conflicto con el estado actual del recurso.",
    "status": 409,
    "detail": "Ya existe una categoría con el nombre 'TORNILLERÍA'."
  }
  ```

- Si dos peticiones crean a la vez una categoría con el mismo nombre, una responde 201 y la otra 409. Lo garantiza el índice único filtrado de la base (`UX_Categories_Name ... WHERE IsActive = 1`), además de la comprobación previa.
- El nombre de una categoría **desactivada** queda libre: se puede crear otra con el mismo nombre, que recibe un `id` nuevo, distinto del de la desactivada.

### `GET /api/categories`

Lista las categorías activas, paginadas (BR-18).

| Parámetro de consulta | Tipo | Por defecto | Rango válido |
|---|---|---|---|
| `page` | entero | 1 | Desde 1 |
| `pageSize` | entero | 20 | De 1 a 100 |

Respuesta 200:

```json
{
  "items": [
    { "id": "8c1d6f0a-2b9e-4c71-9a43-5e2f8d7b1a60", "name": "Adhesivos", "description": null, "createdAtUtc": "2026-11-09T10:20:00" },
    { "id": "1b7e4c2d-9f3a-4e85-b6d0-7a2c9e8f4b13", "name": "Pinturas", "description": "Pinturas y barnices", "createdAtUtc": "2026-11-09T10:18:00" },
    { "id": "3f2504e0-4f89-11d3-9a0c-0305e82c3301", "name": "Tornillería", "description": "Tornillos y tuercas", "createdAtUtc": "2026-11-09T10:15:30.1234567" }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 3,
  "totalPages": 1
}
```

| Campo | Contenido |
|---|---|
| `items` | Las categorías activas de la página pedida |
| `page` | La página pedida, aunque esté fuera de rango |
| `pageSize` | El tamaño de página aplicado |
| `totalCount` | Número total de categorías activas, no solo las de la página |
| `totalPages` | `ceil(totalCount / pageSize)`. Es 0 si no hay categorías activas |

- **Solo activas:** las categorías desactivadas no aparecen en `items` ni cuentan en `totalCount`. Con dos activas y una desactivada, `items` tiene dos elementos y `totalCount` es 2.
- **Orden:** por `name` ascendente, sin distinguir mayúsculas, y a igualdad de nombre por `id`. `"Pinturas"`, `"Adhesivos"` y `"Tornillería"` se devuelven como `"Adhesivos"`, `"Pinturas"`, `"Tornillería"`.
- **Primera página por defecto:** con 25 categorías activas, `GET /api/categories` devuelve 20 elementos, `page` 1, `pageSize` 20, `totalCount` 25 y `totalPages` 2.
- **Página fuera de rango:** con 25 categorías activas, `page=9&pageSize=20` responde 200 (no 404) con `items` vacío, `page` 9, `totalCount` 25 y `totalPages` 2.
- **Sin categorías:** responde 200 con `items` vacío, `totalCount` 0 y `totalPages` 0.
- **`pageSize=100`:** es válido y la respuesta trae `pageSize` 100.

Parámetros inválidos (400, el `detail` nombra el parámetro):

| Petición | `detail` |
|---|---|
| `?page=0` | `El parámetro 'page' debe ser un número entero mayor o igual que 1.` |
| `?pageSize=0` | `El parámetro 'pageSize' debe ser un número entero entre 1 y 100.` |
| `?pageSize=101` | `El parámetro 'pageSize' debe ser un número entero entre 1 y 100.` |
| `?page=abc` | `El parámetro 'page' debe ser un número entero.` |

### `GET /api/categories/{id}`

Devuelve una categoría activa. Respuesta 200 con la [representación de una categoría](#representación-de-una-categoría).

Si no hay ninguna categoría activa con ese id (no existe o está desactivada), responde 404:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Recurso no encontrado.",
  "status": 404,
  "detail": "No existe una categoría activa con el id '3f2504e0-4f89-11d3-9a0c-0305e82c3301'."
}
```

### `DELETE /api/categories/{id}`

Desactiva la categoría (**borrado lógico**, BR-05): la fila no se borra, se marca como inactiva. Desde ese momento no aparece en `GET /api/categories`, `GET /api/categories/{id}` responde 404 y su nombre queda libre para otra categoría.

| Situación | Respuesta |
|---|---|
| Categoría activa sin productos | 204, sin cuerpo |
| Categoría activa cuyos productos están todos inactivos | 204, sin cuerpo |
| Categoría activa con al menos un producto activo | 409; la categoría sigue activa |
| Categoría que ya estaba desactivada | 404 |
| Id con formato válido que no corresponde a ninguna categoría | 404 |

Respuesta 409:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.8",
  "title": "Conflicto con el estado actual del recurso.",
  "status": 409,
  "detail": "No se puede eliminar la categoría porque tiene productos activos."
}
```

El 404 (inexistente o ya desactivada) tiene el mismo cuerpo que en `GET /api/categories/{id}`.

La comprobación de productos activos y la desactivación se hacen en una sola sentencia, así que no se puede crear un producto activo entre los dos pasos.

### Id que no es un GUID

`GET /api/categories/{id}` y `DELETE /api/categories/{id}` solo coinciden con un `id` con formato de GUID (ruta `{id:guid}`). Con cualquier otro valor, por ejemplo `GET /api/categories/abc`, ninguna ruta coincide y la respuesta es **404 con el cuerpo vacío**, sin ProblemDetails. Los 404 con mensaje solo se dan con un GUID válido que no corresponde a ninguna categoría activa.
