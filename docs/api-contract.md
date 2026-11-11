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
- Trae un claim `sub` no vacío (`add-inventory-movements`). Con el realm actual todos los tokens lo traen. Un token sin `sub`, o con un `sub` vacío o solo con espacios, recibe 401 en **todos** los endpoints, porque `sub` es el autor de los movimientos.

Los claims se leen con el nombre que traen en el token, sin renombrarlos: `azp` es el id del cliente, `scope` es la lista de scopes y `sub` es el identificador del usuario de servicio del cliente (un UUID).

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
| 401 Unauthorized | Falta la cabecera `Authorization`, la firma no es válida, el token caducó, `iss` es de otro emisor, `aud` no contiene la audiencia configurada o falta el claim `sub` | Vacío. La cabecera `WWW-Authenticate: Bearer ...` indica el motivo |
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

El texto depende del tipo del parámetro (`add-products`, D8):

| Tipo del parámetro | `detail` | Ejemplo |
|---|---|---|
| Entero | `El parámetro '{nombre}' debe ser un número entero.` | `?pageSize=diez` |
| GUID | `El parámetro '{nombre}' debe ser un identificador (GUID) válido.` | `GET /api/products?categoryId=abc` |
| Otro | `El parámetro '{nombre}' no tiene un formato válido.` | |

Un campo con tipo incorrecto **dentro del cuerpo** (por ejemplo `"categoryId": "abc"` en `POST /api/products`) da siempre `El cuerpo de la petición no es un JSON válido.`

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

## Productos (`add-products`)

Los productos pertenecen a una categoría activa y llevan su stock. Los endpoints de productos solo leen el stock: únicamente lo cambian los [movimientos de inventario](#movimientos-de-inventario-add-inventory-movements).

### Endpoints

| Método y ruta | Scope exigido | Éxito | Errores con ProblemDetails | Sin token o token inválido | Token sin el scope |
|---|---|---|---|---|---|
| `POST /api/products` | `inventory.write` | 201 | 400, 409 | 401 | 403 |
| `GET /api/products?page&pageSize&categoryId&search` | `inventory.read` | 200 | 400 | 401 | 403 |
| `GET /api/products/{id}` | `inventory.read` | 200 | 404 | 401 | 403 |
| `PUT /api/products/{id}` | `inventory.write` | 200 | 400, 404 | 401 | 403 |
| `DELETE /api/products/{id}` | `inventory.write` | 204 | 404 | 401 | 403 |

- 401 y 403 llegan con el cuerpo vacío, igual que en categorías.
- `{id}` solo coincide con un GUID (`{id:guid}`). Cualquier otro valor responde 404 con el cuerpo vacío.

### Representación de un producto

La misma en todas las respuestas (201, 200 del `PUT`, `GET` por id y cada elemento del listado):

```json
{
  "id": "6a1f0c3e-8d2b-4f7a-9e15-2c4b7d9e0f31",
  "sku": "TOR-001",
  "name": "Tornillo 3/8",
  "description": "Acero inoxidable",
  "categoryId": "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
  "categoryName": "Tornillería",
  "stockQuantity": 0,
  "createdAtUtc": "2026-11-10T09:30:00.1234567Z",
  "updatedAtUtc": null
}
```

| Campo | Tipo | Contenido |
|---|---|---|
| `id` | GUID | Lo genera la API al crear el producto |
| `sku` | string | SKU recortado, de 1 a 50 caracteres. No cambia nunca |
| `name` | string | Nombre recortado, de 1 a 200 caracteres |
| `description` | string o `null` | Descripción recortada, de 1 a 1000 caracteres, o `null` |
| `categoryId` | GUID | Categoría activa a la que pertenece |
| `categoryName` | string | Nombre de esa categoría |
| `stockQuantity` | entero | Stock actual. Empieza en 0 y solo lo cambian los movimientos (BR-07) |
| `createdAtUtc` | fecha y hora ISO 8601 | Momento del alta, en UTC |
| `updatedAtUtc` | fecha y hora ISO 8601 o `null` | Última edición, en UTC; `null` si nunca se ha editado |

Como en categorías, las respuestas solo devuelven productos activos y no incluyen `isActive`. La respuesta 201 escribe `createdAtUtc` con el sufijo `Z`; las lecturas y la respuesta del `PUT` escriben las fechas sin sufijo, porque salen de columnas `datetime2` (ver `docs/assumptions.md`).

### `POST /api/products`

Crea un producto activo con stock 0. Cuerpo de la petición:

```json
{
  "sku": "TOR-001",
  "name": "Tornillo 3/8",
  "description": "Acero inoxidable",
  "categoryId": "3f2504e0-4f89-11d3-9a0c-0305e82c3301"
}
```

| Campo | Obligatorio | Reglas |
|---|---|---|
| `sku` | Sí | BR-01. Se recorta; no puede quedar vacío ni superar los 50 caracteres. Único entre **todos** los productos, también los borrados, sin distinguir mayúsculas |
| `name` | Sí | BR-02. Se recorta; no puede quedar vacío ni superar los 200 caracteres. No tiene que ser único |
| `description` | No | Se recorta. Vacía o solo con espacios se guarda como `null`. Máximo 1000 caracteres |
| `categoryId` | Sí | BR-03. Debe ser una categoría activa |
| `stockQuantity` | No aplica | No forma parte del contrato. Si se envía, se ignora y el producto empieza con 0 (BR-07) |

Respuesta 201 con el producto (ver la [representación](#representación-de-un-producto)) y la cabecera `Location: /api/products/{id}`.

| Cuerpo enviado | Respuesta |
|---|---|
| Producto válido | 201 con `stockQuantity` 0 y `updatedAtUtc` `null` |
| Producto válido con `"stockQuantity": 50` | 201 con `stockQuantity` 0 |
| `"sku": "  TOR-001  "` | 201 con `"sku": "TOR-001"` |
| Sin `sku`, o `"sku": "   "` | 400: `El SKU del producto es obligatorio.` |
| SKU de 51 caracteres después de recortarlo | 400: `El SKU del producto no puede superar los 50 caracteres.` |
| Sin `name`, o vacío | 400: `El nombre del producto es obligatorio.` |
| Nombre de 201 caracteres | 400: `El nombre del producto no puede superar los 200 caracteres.` |
| Descripción de 1001 caracteres | 400: `La descripción del producto no puede superar los 1000 caracteres.` |
| Sin `categoryId` | 400: `La categoría del producto es obligatoria.` |
| `categoryId` inexistente o de una categoría desactivada | 400: `La categoría indicada no existe o no está activa.` |
| `"categoryId": "abc"` | 400: `El cuerpo de la petición no es un JSON válido.` |
| SKU de otro producto, activo o borrado | 409: `Ya existe un producto con el SKU 'TOR-001'.` |

#### Orden de los errores

Si un alta incumple varias reglas, se responde solo el primer error en este orden:

1. Formato de `sku`, `name`, `description` y `categoryId` ausente (400).
2. Categoría inexistente o desactivada (400).
3. SKU repetido (409).

Por ejemplo, un SKU repetido con una categoría inexistente responde 400 por la categoría.

#### SKU reservado para siempre (BR-01)

- El SKU se compara después de recortarlo y sin distinguir mayúsculas: `"tor-001"` y `" TOR-001 "` chocan con `"TOR-001"`.
- Un SKU **no se libera** al borrar el producto. Crear otro producto con el SKU de uno borrado responde 409 con el mismo mensaje, que no dice si el otro está borrado.
- Si dos peticiones crean a la vez un producto con el mismo SKU, una responde 201 y la otra 409. Lo garantiza el índice único `UX_Products_Sku`, sin filtro.

#### Categoría activa también frente a la concurrencia (BR-03)

El alta comprueba la categoría en la misma sentencia que inserta el producto, bloqueando la fila de la categoría. Si a la vez se desactiva esa categoría, o el alta responde 201 y la desactivación 409, o la desactivación responde 204 y el alta 400. Nunca queda un producto activo en una categoría desactivada.

### `GET /api/products`

Lista los productos activos, paginados (BR-18) y ordenados por `name` y, a igualdad, por `id`.

| Parámetro de consulta | Tipo | Por defecto | Reglas |
|---|---|---|---|
| `page` | entero | 1 | Desde 1 |
| `pageSize` | entero | 20 | De 1 a 100 |
| `categoryId` | GUID | Ninguno | Solo los productos de esa categoría. Una categoría inexistente o desactivada da 200 con `items` vacío |
| `search` | texto | Ninguno | Productos cuyo `name` o `sku` **contiene** el texto, sin distinguir mayúsculas. Se recorta; vacío o solo con espacios es como no enviarlo |

La respuesta tiene la misma forma que el listado de categorías (`{ items, page, pageSize, totalCount, totalPages }`), y cada elemento de `items` es un producto completo. `totalCount` cuenta los productos activos que cumplen los filtros.

- **Búsqueda literal:** `%`, `_` y `[` se buscan como texto. `search=50%` encuentra `"Descuento 50%"` y no `"Caja 500"`.
- **Búsqueda en el SKU:** `search=r-00` encuentra el producto con SKU `"TOR-001"` aunque su nombre no lo contenga.
- **Combinación:** `categoryId` y `search` se aplican a la vez.
- **Borrados:** no aparecen ni cuentan en `totalCount`.

Parámetros inválidos (400): `page` y `pageSize` como en categorías, y `?categoryId=abc` con `El parámetro 'categoryId' debe ser un identificador (GUID) válido.`

### `GET /api/products/{id}`

Responde 200 con el producto activo. Un GUID que no corresponde a ningún producto activo, incluido uno borrado, responde 404 con `No existe un producto activo con el id '{id}'.`

### `PUT /api/products/{id}`

Reemplaza los campos editables de un producto activo y responde 200 con el producto actualizado. Cuerpo:

```json
{
  "name": "Tornillo 1/2",
  "description": null,
  "categoryId": "9b2e5d10-7c4a-4e3b-8f61-0d5a2c7e9b44"
}
```

- `name`, `description` y `categoryId` siguen las mismas reglas que en el alta.
- Es un reemplazo completo: un `description` ausente o `null` deja la descripción en `null`.
- **`sku` y `stockQuantity` se ignoran** si vienen en el cuerpo: el SKU y el stock no cambian nunca con un `PUT` (BR-01, BR-07).
- La respuesta trae `updatedAtUtc` con el momento de la edición. `sku`, `stockQuantity` y `createdAtUtc` no cambian.
- La nueva categoría se comprueba en la misma sentencia que actualiza el producto, igual que en el alta.

| Situación | Respuesta |
|---|---|
| Producto activo y datos válidos | 200 con el producto actualizado |
| Cuerpo con `"sku": "OTRO-001"` o `"stockQuantity": 99` | 200; `sku` y `stockQuantity` no cambian |
| `name` vacío u otra regla de formato | 400 con el mensaje de la regla; el producto no cambia |
| Producto inexistente o borrado | 404: `No existe un producto activo con el id '{id}'.` |
| Producto activo y categoría inexistente o desactivada | 400: `La categoría indicada no existe o no está activa.` |

Si el producto no existe **y** la categoría tampoco, gana el 404 (ver `docs/assumptions.md`).

### `DELETE /api/products/{id}`

Borrado lógico (BR-06): el producto pasa a inactivo y la fila se conserva.

| Situación | Respuesta |
|---|---|
| Producto activo, con o sin stock | 204, sin cuerpo |
| Producto que ya estaba borrado | 404 |
| GUID que no corresponde a ningún producto | 404 |

- **El stock queda congelado:** un producto borrado con `stockQuantity` 15 conserva ese valor en la base, pero ya no se puede consultar por la API.
- **Los movimientos se conservan:** el borrado no toca el libro de movimientos.
- **El SKU no se libera** (ver arriba).
- Borrar el único producto activo de una categoría permite desactivar después esa categoría (BR-05).

## Proveedores (`add-suppliers`)

Los proveedores se crean activos y se desactivan, nunca se borran (BR-09). Un proveedor desactivado es invisible para la API y no se puede reactivar.

### Endpoints

| Método y ruta | Scope exigido | Éxito | Errores con ProblemDetails | Sin token o token inválido | Token sin el scope |
|---|---|---|---|---|---|
| `POST /api/suppliers` | `inventory.write` | 201 | 400, 409 | 401 | 403 |
| `GET /api/suppliers?page&pageSize` | `inventory.read` | 200 | 400 | 401 | 403 |
| `GET /api/suppliers/{id}` | `inventory.read` | 200 | 404 | 401 | 403 |
| `PUT /api/suppliers/{id}` | `inventory.write` | 200 | 400, 404 | 401 | 403 |
| `DELETE /api/suppliers/{id}` | `inventory.write` | 204 | 404 | 401 | 403 |

- 401 y 403 llegan con el cuerpo vacío, igual que en categorías.
- `{id}` solo coincide con un GUID (`{id:guid}`). Cualquier otro valor responde 404 con el cuerpo vacío.

### Representación de un proveedor

La misma en todas las respuestas (201, 200 del `PUT`, `GET` por id y cada elemento del listado):

```json
{
  "id": "8c0e4f2a-1b3d-4e5f-9a7b-6c5d4e3f2a1b",
  "name": "Ferretería Andina",
  "nit": "900123456-7",
  "email": "ventas@andina.com",
  "phone": "+57 300 123 4567",
  "createdAtUtc": "2026-11-10T09:30:00.1234567Z",
  "updatedAtUtc": null
}
```

| Campo | Tipo | Contenido |
|---|---|---|
| `id` | GUID | Lo genera la API al crear el proveedor |
| `name` | string | Nombre recortado, de 1 a 200 caracteres |
| `nit` | string | NIT recortado, de 1 a 30 caracteres. No cambia nunca |
| `email` | string o `null` | Correo recortado, tal como se envió, o `null` |
| `phone` | string o `null` | Teléfono recortado, tal como se envió, o `null` |
| `createdAtUtc` | fecha y hora ISO 8601 | Momento del alta, en UTC |
| `updatedAtUtc` | fecha y hora ISO 8601 o `null` | Última edición o desactivación, en UTC; `null` si nunca se ha editado |

Como en productos, las respuestas solo devuelven proveedores activos y no incluyen `isActive`. La respuesta 201 escribe `createdAtUtc` con el sufijo `Z`, y las lecturas y la respuesta del `PUT` sin sufijo.

### `POST /api/suppliers`

Crea un proveedor activo. Cuerpo de la petición:

```json
{
  "name": "Ferretería Andina",
  "nit": "900123456-7",
  "email": "ventas@andina.com",
  "phone": "+57 300 123 4567"
}
```

| Campo | Obligatorio | Reglas |
|---|---|---|
| `name` | Sí | BR-08. Se recorta; no puede quedar vacío ni superar los 200 caracteres. No tiene que ser único |
| `nit` | Sí | BR-08. Se recorta; no puede quedar vacío ni superar los 30 caracteres. Único entre **todos** los proveedores, también los desactivados, sin distinguir mayúsculas |
| `email` | No | Se recorta. Vacío o solo con espacios se guarda como `null`. Máximo 200 caracteres. Debe tener la forma `algo@dominio.ext`: sin espacios, una sola `@` y al menos un punto después de ella. Se guarda tal cual, sin cambiar mayúsculas |
| `phone` | No | Se recorta. Vacío o solo con espacios se guarda como `null`. Máximo 30 caracteres. Solo un `+` inicial opcional, dígitos, espacios y guiones, con entre 7 y 15 dígitos. Se guarda tal cual |

Respuesta 201 con el proveedor (ver la [representación](#representación-de-un-proveedor)) y la cabecera `Location: /api/suppliers/{id}`.

| Cuerpo enviado | Respuesta |
|---|---|
| Proveedor válido | 201 con `updatedAtUtc` `null` |
| Solo `name` y `nit` | 201 con `email` y `phone` `null` |
| `"nit": "  900123456-7  "` | 201 con `"nit": "900123456-7"` |
| `"email": "  Juan@Mail.com  "` | 201 con `"email": "Juan@Mail.com"` |
| Sin `name`, o vacío | 400: `El nombre del proveedor es obligatorio.` |
| Nombre de 201 caracteres | 400: `El nombre del proveedor no puede superar los 200 caracteres.` |
| Sin `nit`, o vacío | 400: `El NIT del proveedor es obligatorio.` |
| NIT de 31 caracteres | 400: `El NIT del proveedor no puede superar los 30 caracteres.` |
| `"email": "juan@mail"`, `"juan.mail.com"`, `"juan perez@mail.com"` o `"juan@@mail.com"` | 400: `El correo del proveedor no tiene un formato válido.` |
| Correo de 201 caracteres | 400: `El correo del proveedor no puede superar los 200 caracteres.` |
| `"phone": "123-456"` (6 dígitos), `"1234567890123456"` (16), `"300 ABC 4567"` o `"300+1234567"` | 400: `El teléfono del proveedor no tiene un formato válido.` |
| Teléfono de 31 caracteres | 400: `El teléfono del proveedor no puede superar los 30 caracteres.` |
| NIT de otro proveedor, activo o desactivado | 409: `Ya existe un proveedor con el NIT '900123456-7'.` |

#### Orden de los errores

Si un alta incumple varias reglas, se responde solo el primer error en este orden:

1. Formato de `name`, `nit`, `email` y `phone`, en ese orden (400). Dentro del correo y del teléfono, la longitud se comprueba antes que el formato.
2. NIT repetido (409).

Por ejemplo, un NIT repetido con `"email": "juan@mail"` responde 400 por el correo.

#### NIT reservado para siempre (BR-08)

- El NIT se compara después de recortarlo y sin distinguir mayúsculas: `"abc-123"` choca con `"ABC-123"`. No se quitan puntos ni guiones: `900.123.456-7` y `900123456-7` son NIT distintos.
- Un NIT **no se libera** al desactivar el proveedor. Crear otro con el NIT de uno desactivado responde 409 con el mismo mensaje, que no dice si el otro está desactivado.
- Si dos peticiones crean a la vez un proveedor con el mismo NIT, una responde 201 y la otra 409. Lo garantiza el índice único `UX_Suppliers_Nit`, sin filtro.

### `GET /api/suppliers`

Lista los proveedores activos, paginados (BR-18) y ordenados por `name` y, a igualdad, por `id`. No admite búsqueda ni filtros.

| Parámetro de consulta | Tipo | Por defecto | Reglas |
|---|---|---|---|
| `page` | entero | 1 | Desde 1 |
| `pageSize` | entero | 20 | De 1 a 100 |

La respuesta tiene la misma forma que el listado de categorías (`{ items, page, pageSize, totalCount, totalPages }`), y cada elemento de `items` es un proveedor completo. Los desactivados no aparecen ni cuentan en `totalCount`. Los errores de `page` y `pageSize` son los mismos que en categorías.

### `GET /api/suppliers/{id}`

Responde 200 con el proveedor activo. Un GUID que no corresponde a ningún proveedor activo, incluido uno desactivado, responde 404 con `No existe un proveedor activo con el id '{id}'.`

### `PUT /api/suppliers/{id}`

Reemplaza los campos editables de un proveedor activo y responde 200 con el proveedor actualizado. Cuerpo:

```json
{
  "name": "Andina S.A.S.",
  "email": "compras@andina.com",
  "phone": "6011234567"
}
```

- `name`, `email` y `phone` siguen las mismas reglas que en el alta.
- Es un reemplazo completo: un `email` o un `phone` ausente o `null` los deja en `null`.
- **`nit` se ignora** si viene en el cuerpo, sin validarlo: el NIT no cambia nunca con un `PUT` (BR-08).
- La respuesta trae `updatedAtUtc` con el momento de la edición. `nit` y `createdAtUtc` no cambian.

| Situación | Respuesta |
|---|---|
| Proveedor activo y datos válidos | 200 con el proveedor actualizado |
| Cuerpo con `"nit": "800000000-1"` o `"nit": ""` | 200; `nit` no cambia |
| Cuerpo con solo `name` | 200 con `email` y `phone` `null` |
| `name` vacío u otra regla de formato | 400 con el mensaje de la regla; el proveedor no cambia |
| Proveedor inexistente o desactivado | 404: `No existe un proveedor activo con el id '{id}'.` |

Si el cuerpo es inválido **y** el proveedor no existe, gana el 400, porque el formato se comprueba antes de buscar el proveedor.

### `DELETE /api/suppliers/{id}`

Desactivación (BR-09): el proveedor pasa a inactivo y la fila se conserva, con `updatedAtUtc` igual al momento de la desactivación.

| Situación | Respuesta |
|---|---|
| Proveedor activo, con o sin asociaciones y entradas | 204, sin cuerpo |
| Proveedor que ya estaba desactivado | 404 |
| GUID que no corresponde a ningún proveedor | 404 |

- **No hay condiciones previas:** se puede desactivar un proveedor asociado a productos o usado en entradas.
- **Se conservan sus asociaciones** y los movimientos que lo indican. Las asociaciones dejan de ser visibles.
- **No se reactiva:** no existe ningún endpoint para volver a activarlo, y su NIT queda reservado (ver arriba).
- Después de desactivarlo, `GET`, `PUT` y `DELETE` sobre su id responden 404.

## Asociaciones producto-proveedor (`add-suppliers`)

Un producto puede tener varios proveedores y un proveedor varios productos (BR-10). Cada asociación se identifica por la pareja (producto, proveedor) y **no se edita ni se borra**. Crear un producto no le asocia proveedores.

### Endpoints

| Método y ruta | Scope exigido | Éxito | Errores con ProblemDetails | Sin token o token inválido | Token sin el scope |
|---|---|---|---|---|---|
| `POST /api/products/{productId}/suppliers` | `inventory.write` | 201 | 400, 404, 409 | 401 | 403 |
| `GET /api/products/{productId}/suppliers/{supplierId}` | `inventory.read` | 200 | 404 | 401 | 403 |
| `GET /api/products/{productId}/suppliers?page&pageSize` | `inventory.read` | 200 | 400, 404 | 401 | 403 |
| `GET /api/suppliers/{supplierId}/products?page&pageSize` | `inventory.read` | 200 | 400, 404 | 401 | 403 |

- 401 y 403 llegan con el cuerpo vacío, igual que en categorías.
- `{productId}` y `{supplierId}` solo coinciden con un GUID. Cualquier otro valor responde 404 con el cuerpo vacío.
- **No existen `PUT`, `PATCH` ni `DELETE`.** `PUT` y `DELETE` sobre `/api/products/{productId}/suppliers/{supplierId}` responden 405 y la asociación no cambia.

### Representación de una asociación

La misma en las cuatro respuestas (201, `GET` de la pareja y cada elemento de los dos listados):

```json
{
  "productId": "ed3ebe13-2aef-450a-8546-bd4679741842",
  "productName": "Tornillo 3/8",
  "supplierId": "13a9832e-d0f3-4e73-8075-bfd69bd44013",
  "supplierName": "Ferretería Andina",
  "supplierSku": "FA-778",
  "createdAtUtc": "2026-11-11T01:07:32.1830097"
}
```

| Campo | Tipo | Contenido |
|---|---|---|
| `productId` | GUID | Producto de la asociación |
| `productName` | string | Nombre actual del producto |
| `supplierId` | GUID | Proveedor de la asociación |
| `supplierName` | string | Nombre actual del proveedor |
| `supplierSku` | string o `null` | Código del producto en el catálogo del proveedor, recortado, de 1 a 50 caracteres, o `null` |
| `createdAtUtc` | fecha y hora ISO 8601 | Momento del alta, en UTC |

`createdAtUtc` se escribe **sin** sufijo `Z` también en el 201, porque la respuesta del alta se lee de la base en la misma operación (ver `docs/assumptions.md`).

### `POST /api/products/{productId}/suppliers`

Asocia un proveedor al producto de la ruta. Cuerpo:

```json
{
  "supplierId": "13a9832e-d0f3-4e73-8075-bfd69bd44013",
  "supplierSku": "FA-778"
}
```

| Campo | Obligatorio | Reglas |
|---|---|---|
| `supplierId` | Sí | BR-09. Debe ser un proveedor activo |
| `supplierSku` | No | Se recorta. Vacío o solo con espacios se guarda como `null`. Máximo 50 caracteres. No tiene que ser único |

Respuesta 201 con la asociación y la cabecera `Location: /api/products/{productId}/suppliers/{supplierId}`.

| Situación | Respuesta |
|---|---|
| Producto y proveedor activos, no asociados | 201 |
| `"supplierSku": "  FA-778  "` | 201 con `"supplierSku": "FA-778"` |
| Sin `supplierSku`, o solo con espacios | 201 con `supplierSku` `null` |
| `supplierSku` de 51 caracteres | 400: `El código del producto en el proveedor no puede superar los 50 caracteres.` |
| Sin `supplierId` | 400: `El proveedor es obligatorio.` |
| `"supplierId": "abc"` | 400: `El cuerpo de la petición no es un JSON válido.` |
| Producto de la ruta inexistente o borrado | 404: `No existe un producto activo con el id '{productId}'.` |
| Proveedor inexistente o desactivado | 400: `El proveedor indicado no existe o no está activo.` |
| Pareja ya asociada | 409: `El proveedor ya está asociado al producto.` |

#### Orden de los errores

Si un alta incumple varias reglas, se responde solo el primer error en este orden:

1. Formato de `supplierId` y `supplierSku` (400).
2. Producto de la ruta inexistente o borrado (404).
3. Proveedor inexistente o desactivado (400).
4. Pareja repetida (409).

Por ejemplo, un proveedor desactivado sobre un producto borrado responde 404, y volver a asociar un proveedor que se desactivó después responde 400 por el proveedor, no 409.

#### Proveedor activo también frente a la concurrencia (BR-09, BR-10)

- El alta comprueba el producto y el proveedor en la misma sentencia que inserta la asociación, y bloquea la fila del proveedor. Si a la vez se desactiva ese proveedor, o el alta responde 201 y la desactivación 204, o la desactivación responde 204 y el alta 400. Nunca se crea una asociación con un proveedor ya desactivado.
- Si dos peticiones asocian a la vez la misma pareja, una responde 201 y la otra 409. Lo garantiza el índice único `UX_ProductSuppliers_Link`.

### Visibilidad

Solo se ven las asociaciones cuyo producto **y** proveedor están activos. Borrar un producto o desactivar un proveedor conserva las filas de sus asociaciones, pero dejan de aparecer en todas las lecturas.

### `GET /api/products/{productId}/suppliers/{supplierId}`

Responde 200 con la asociación. Si la pareja no existe, o si el producto o el proveedor no están activos, responde 404 con `El proveedor no está asociado al producto.`

### `GET /api/products/{productId}/suppliers`

Lista los proveedores activos del producto, paginados (BR-18) y ordenados por `supplierName` y, a igualdad, por `supplierId`.

- `page` y `pageSize` siguen las mismas reglas que en categorías y se comprueban **antes** que el producto.
- Producto inexistente o borrado: 404 con `No existe un producto activo con el id '{productId}'.`, aunque conserve asociaciones.
- Producto activo sin proveedores: 200 con `items` vacío.

### `GET /api/suppliers/{supplierId}/products`

Vista inversa: lista los productos activos del proveedor, paginados (BR-18) y ordenados por `productName` y, a igualdad, por `productId`.

- `page` y `pageSize` se comprueban **antes** que el proveedor.
- Proveedor inexistente o desactivado: 404 con `No existe un proveedor activo con el id '{supplierId}'.`, aunque conserve asociaciones.
- Proveedor activo sin productos: 200 con `items` vacío.

## Movimientos de inventario (`add-inventory-movements`)

Los movimientos son el libro inmutable del stock: cada entrada o salida cambia `stockQuantity` del producto y queda registrada para siempre. Los endpoints están anidados bajo el producto, porque cada movimiento pertenece a un único producto.

### Endpoints

| Método y ruta | Scope exigido | Éxito | Errores con ProblemDetails | Sin token o token inválido | Token sin el scope |
|---|---|---|---|---|---|
| `POST /api/products/{productId}/movements` | `inventory.write` | 201 | 400, 404, 409 | 401 | 403 |
| `GET /api/products/{productId}/movements?page&pageSize` | `inventory.read` | 200 | 400, 404 | 401 | 403 |

- 401 y 403 llegan con el cuerpo vacío, igual que en el resto de la API.
- `{productId}` solo coincide con un GUID (`{productId:guid}`). Cualquier otro valor responde 404 con el cuerpo vacío.
- **No existen `PUT`, `PATCH` ni `DELETE`** (BR-13). Un movimiento no se modifica ni se borra: una corrección es un movimiento nuevo. `DELETE /api/products/{productId}/movements` responde 405, y cualquier ruta con un id de movimiento (`/movements/{movementId}`) responde 404.

### Representación de un movimiento

La misma en la respuesta 201 y en cada elemento del listado:

```json
{
  "id": "6c37cac6-82b0-4dee-9421-84f101b7f143",
  "productId": "e927dbd0-7f26-4060-af94-fd93130d45b6",
  "supplierId": "13a9832e-d0f3-4e73-8075-bfd69bd44013",
  "type": "Entry",
  "quantity": 10,
  "stockAfter": 10,
  "reason": "Compra inicial",
  "occurredAtUtc": "2026-11-10T19:23:46.4112896Z",
  "registeredBy": "ee59fce2-7335-40b0-801b-92b834dc40d7"
}
```

| Campo | Tipo | Contenido |
|---|---|---|
| `id` | GUID | Lo genera la API al registrar el movimiento |
| `productId` | GUID | Producto al que pertenece |
| `supplierId` | GUID o `null` | Proveedor de una entrada (BR-16), o `null`. Una salida siempre tiene `null`. Se sigue devolviendo aunque el proveedor se haya desactivado después |
| `type` | string | Siempre en su forma canónica: `"Entry"` o `"Exit"` |
| `quantity` | entero | Mayor que 0 |
| `stockAfter` | entero | `stockQuantity` del producto justo después de aplicar el movimiento |
| `reason` | string o `null` | Motivo recortado, de 1 a 250 caracteres, o `null` |
| `occurredAtUtc` | fecha y hora ISO 8601 | Momento del registro, en UTC, fijado por la API |
| `registeredBy` | string | Claim `sub` del token con el que se registró (el UUID del usuario de servicio del cliente en Keycloak, no el id del cliente) |

La respuesta 201 escribe `occurredAtUtc` con el sufijo `Z`. El listado lo escribe sin sufijo, porque sale de una columna `datetime2`, igual que `createdAtUtc` en categorías y productos. El valor siempre está en UTC.

### `POST /api/products/{productId}/movements`

Registra una entrada o una salida y responde **201 sin cabecera `Location`**. A diferencia de categorías y productos, no existe un recurso individual de movimiento que consultar, así que no se envía un enlace que daría 404 (ver `docs/assumptions.md`). Todo lo que el cliente necesita, incluido `stockAfter`, va en el cuerpo de la respuesta.

Cuerpo de la petición:

```json
{
  "type": "Entry",
  "quantity": 10,
  "reason": "Compra inicial",
  "supplierId": "13a9832e-d0f3-4e73-8075-bfd69bd44013"
}
```

| Campo | Obligatorio | Reglas |
|---|---|---|
| `type` | Sí | `Entry` o `Exit` (BR-11). Se recorta y no distingue mayúsculas: `" entry "` es válido y se devuelve como `"Entry"`. Cualquier otro texto, incluidos `"1"` o `"3"`, responde 400 |
| `quantity` | Sí | Entero mayor que 0 (BR-11) |
| `reason` | No | Se recorta. Vacío o solo con espacios se guarda como `null`. Máximo 250 caracteres después de recortar |
| `supplierId` | No | BR-16. Solo en una entrada: el proveedor debe existir, estar activo y estar **asociado al producto**. Ausente o `null`, la entrada no tiene proveedor. En una salida solo se admite ausente o `null` |

Los campos que no son parte del contrato se ignoran sin error: `stockAfter`, `occurredAtUtc`, `registeredBy` y cualquier otro. El autor siempre sale del token, nunca del cuerpo.

> **BREAKING** (`add-suppliers`): `supplierId` ya **no se ignora**. Una salida que lo traía, antes aceptada con 201, ahora responde 400.

| Situación | Respuesta |
|---|---|
| Entrada válida sobre un producto activo | 201; el stock sube en `quantity` |
| Salida válida que cabe en el stock | 201; el stock baja en `quantity`. Una salida igual al stock deja `stockAfter` en 0 |
| `type` ausente o en blanco | 400: `El tipo de movimiento es obligatorio.` |
| `type` con otro valor | 400: `El tipo de movimiento debe ser 'Entry' o 'Exit'.` |
| `quantity` ausente | 400: `La cantidad del movimiento es obligatoria.` |
| `quantity` igual a 0 o negativa | 400: `La cantidad del movimiento debe ser un entero mayor que 0.` |
| `reason` de más de 250 caracteres | 400: `El motivo del movimiento no puede superar los 250 caracteres.` |
| `type` como número JSON (`1`) o `quantity` decimal (`1.5`) | 400: `El cuerpo de la petición no es un JSON válido.` |
| Entrada con un proveedor asociado y activo | 201 con `supplierId` |
| Salida con un `supplierId` distinto de `null` | 400: `Una salida no puede indicar proveedor.`; no se registra nada |
| `"supplierId": "abc"` | 400: `El cuerpo de la petición no es un JSON válido.` |
| Producto inexistente o borrado | 404: `No existe un producto activo con el id '{productId}'.`; no se registra nada |
| Entrada con un proveedor inexistente, desactivado o no asociado al producto | 400: `El proveedor indicado no está activo o no está asociado al producto.`; el stock no cambia |
| Salida mayor que el stock disponible | 409: `El stock disponible del producto es insuficiente para la salida.`; el stock no cambia (BR-12) |
| Entrada que llevaría el stock por encima de 2147483647 | 409: `La entrada supera el stock máximo permitido para el producto.`; el stock no cambia |

- **El stock nunca es negativo** (BR-12).
- **Todo o nada** (BR-14): el cambio de stock y el registro del movimiento ocurren en una sola transacción. Si algo falla, el producto conserva su stock y la respuesta es 500 sin detalles.
- **Salidas simultáneas** (BR-15): si varias salidas del mismo producto llegan a la vez y no hay stock para todas, solo se aceptan las que caben, y las demás reciben 409. Por ejemplo, con stock 5, dos salidas de 4 a la vez: una responde 201 con `stockAfter` 1 y la otra 409.
- **Proveedor activo también frente a la concurrencia** (BR-09): la entrada bloquea la fila del proveedor dentro de su transacción, antes de cambiar el stock. Si a la vez se desactiva ese proveedor, o la entrada responde 201 y la desactivación 204, o la desactivación responde 204 y la entrada 400. Ningún movimiento usa un proveedor ya desactivado.
- Registrar un movimiento **no cambia `updatedAtUtc`** del producto, que sigue indicando la última edición del catálogo.

#### Orden de los errores

Si una petición tiene varios problemas, se responde solo el primero, en este orden:

1. Formato del cuerpo: `type`, `quantity`, `reason` y una salida con `supplierId` (400).
2. Producto inexistente o borrado (404).
3. Proveedor de la entrada inexistente, desactivado o no asociado (400).
4. Stock insuficiente o desbordamiento (409).

Ejemplos:

- `quantity` 0 sobre un producto inexistente responde 400.
- Una salida excesiva sobre un producto borrado responde 404.
- Una salida con `supplierId` sobre un producto borrado responde 400.
- Una entrada con un proveedor no asociado sobre un producto borrado responde 404.
- Una entrada que desbordaría el stock con un proveedor no asociado responde 400.

### `GET /api/products/{productId}/movements`

Lista los movimientos del producto, paginados (BR-18), **del más reciente al más antiguo**: por `occurredAtUtc` descendente y, a igualdad de fecha, por `id` descendente. Ese desempate es arbitrario pero estable, así que la misma consulta devuelve siempre el mismo orden.

| Parámetro de consulta | Tipo | Por defecto | Reglas |
|---|---|---|---|
| `page` | entero | 1 | Desde 1 |
| `pageSize` | entero | 20 | De 1 a 100 |

La respuesta tiene la misma forma que los demás listados (`{ items, page, pageSize, totalCount, totalPages }`), y cada elemento de `items` es un movimiento completo. Solo incluye los movimientos de ese producto.

| Situación | Respuesta |
|---|---|
| Producto activo con movimientos | 200 con la página pedida |
| Producto activo sin movimientos | 200 con `items` vacío y `totalCount` 0 |
| Producto inexistente | 404: `No existe un producto activo con el id '{productId}'.` |
| Producto borrado, aunque conserve movimientos | 404 con el mismo mensaje (DEC-004) |
| `page` o `pageSize` fuera de rango | 400 con el mensaje de categorías. Se comprueba **antes** que el producto: `?page=0` sobre un producto borrado responde 400 |
