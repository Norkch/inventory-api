# categories Specification

## Purpose

Gestionar las categorías del inventario: crearlas, consultarlas una a una o en listados paginados y desactivarlas, respetando las reglas BR-04, BR-05 y BR-18.

## Requirements

### Requirement: Crear una categoría
`POST /api/categories` SHALL exigir el scope `inventory.write` y aceptar un cuerpo JSON con `name` y `description` (opcional). Si la categoría es válida, la API MUST guardarla como activa y responder 201 con la categoría creada y la cabecera `Location` apuntando a `/api/categories/{id}`.

#### Scenario: Alta correcta
- **WHEN** un cliente con `inventory.write` envía `{ "name": "Tornillería", "description": "Tornillos y tuercas" }`
- **THEN** la respuesta es 201 con `id`, `name` igual a `"Tornillería"`, `description` igual a `"Tornillos y tuercas"` y `createdAtUtc` en UTC
- **AND** la cabecera `Location` es `/api/categories/{id}` con el `id` devuelto

#### Scenario: Alta sin descripción
- **WHEN** un cliente con `inventory.write` envía `{ "name": "Tornillería" }`
- **THEN** la respuesta es 201 y `description` es `null`

#### Scenario: Token sin inventory.write
- **WHEN** un cliente con un token válido que solo tiene `inventory.read` llama a `POST /api/categories`
- **THEN** la respuesta es 403 con el cuerpo vacío

### Requirement: Nombre de categoría obligatorio y acotado (BR-04)
La API SHALL recortar los espacios de los extremos del nombre antes de validarlo y guardarlo. Un nombre ausente, vacío o compuesto solo por espacios, o con más de 100 caracteres después de recortarlo, MUST rechazarse con 400 y un mensaje en español.

#### Scenario: Nombre con espacios en los extremos
- **WHEN** un cliente con `inventory.write` crea una categoría con `name` igual a `"  Tornillería  "`
- **THEN** la respuesta es 201 y `name` es `"Tornillería"`

#### Scenario: Nombre ausente
- **WHEN** un cliente con `inventory.write` envía `{ "description": "Sin nombre" }`
- **THEN** la respuesta es 400 con un mensaje en español que indica que el nombre es obligatorio

#### Scenario: Nombre solo con espacios
- **WHEN** un cliente con `inventory.write` crea una categoría con `name` igual a `"     "`
- **THEN** la respuesta es 400 con un mensaje en español que indica que el nombre es obligatorio

#### Scenario: Nombre de 100 caracteres
- **WHEN** un cliente con `inventory.write` crea una categoría con un nombre de exactamente 100 caracteres
- **THEN** la respuesta es 201

#### Scenario: Nombre de 101 caracteres
- **WHEN** un cliente con `inventory.write` crea una categoría con un nombre de 101 caracteres después de recortarlo
- **THEN** la respuesta es 400 con un mensaje en español que indica el máximo de 100 caracteres

### Requirement: Nombre de categoría único entre las activas (BR-04)
El nombre SHALL ser único entre las categorías activas, sin distinguir mayúsculas. Crear una categoría con el nombre de otra activa MUST responder 409 con un mensaje en español, también cuando dos peticiones con el mismo nombre llegan a la vez. El nombre de una categoría desactivada MUST poder reutilizarse.

#### Scenario: Nombre repetido con otras mayúsculas
- **WHEN** existe la categoría activa `"Tornillería"` y un cliente con `inventory.write` crea `"TORNILLERÍA"`
- **THEN** la respuesta es 409 con un mensaje en español que indica que ya existe una categoría con ese nombre

#### Scenario: Nombre repetido con espacios en los extremos
- **WHEN** existe la categoría activa `"Tornillería"` y un cliente con `inventory.write` crea `" Tornillería "`
- **THEN** la respuesta es 409

#### Scenario: Altas simultáneas con el mismo nombre
- **WHEN** dos peticiones crean a la vez una categoría `"Pinturas"` que no existe
- **THEN** una responde 201 y la otra 409

#### Scenario: Reutilizar el nombre de una categoría desactivada
- **WHEN** la categoría `"Tornillería"` se desactivó y un cliente con `inventory.write` crea `"Tornillería"`
- **THEN** la respuesta es 201 con un `id` distinto del de la categoría desactivada

### Requirement: Descripción opcional y acotada
`description` SHALL ser opcional. Si se envía, la API MUST recortar sus espacios de los extremos. Una descripción vacía o compuesta solo por espacios MUST guardarse como `null`, y una de más de 500 caracteres después de recortarla MUST rechazarse con 400 y un mensaje en español.

#### Scenario: Descripción solo con espacios
- **WHEN** un cliente con `inventory.write` crea una categoría con `description` igual a `"   "`
- **THEN** la respuesta es 201 y `description` es `null`

#### Scenario: Descripción de 501 caracteres
- **WHEN** un cliente con `inventory.write` crea una categoría con una descripción de 501 caracteres después de recortarla
- **THEN** la respuesta es 400 con un mensaje en español que indica el máximo de 500 caracteres

### Requirement: Consultar una categoría por id
`GET /api/categories/{id}` SHALL exigir el scope `inventory.read` y responder 200 con `id`, `name`, `description` y `createdAtUtc` de la categoría activa. Si no existe ninguna categoría activa con ese id, MUST responder 404 con un mensaje en español.

#### Scenario: Categoría activa
- **WHEN** un cliente con `inventory.read` consulta el id de la categoría activa `"Tornillería"`
- **THEN** la respuesta es 200 con `name` igual a `"Tornillería"`

#### Scenario: Id inexistente
- **WHEN** un cliente con `inventory.read` consulta un id con formato válido que no corresponde a ninguna categoría
- **THEN** la respuesta es 404 con un mensaje en español

#### Scenario: Categoría desactivada
- **WHEN** un cliente con `inventory.read` consulta el id de una categoría desactivada
- **THEN** la respuesta es 404

#### Scenario: Token sin inventory.read
- **WHEN** un cliente con un token válido que solo tiene `inventory.write` llama a `GET /api/categories/{id}`
- **THEN** la respuesta es 403 con el cuerpo vacío

### Requirement: Listar categorías paginadas (BR-18)
`GET /api/categories` SHALL exigir el scope `inventory.read` y devolver solo las categorías activas, ordenadas por `name` ascendente y, a igualdad de nombre, por `id`. La respuesta MUST tener la forma `{ items, page, pageSize, totalCount, totalPages }`, donde `totalCount` cuenta todas las categorías activas.

#### Scenario: Primera página por defecto
- **WHEN** existen 25 categorías activas y un cliente con `inventory.read` llama a `GET /api/categories` sin parámetros
- **THEN** la respuesta es 200 con 20 elementos en `items`, `page` igual a 1, `pageSize` igual a 20, `totalCount` igual a 25 y `totalPages` igual a 2

#### Scenario: Orden por nombre
- **WHEN** existen las categorías activas `"Pinturas"`, `"Adhesivos"` y `"Tornillería"`
- **THEN** `items` las devuelve en el orden `"Adhesivos"`, `"Pinturas"`, `"Tornillería"`

#### Scenario: Excluye las desactivadas
- **WHEN** existen dos categorías activas y una desactivada
- **THEN** `items` tiene dos elementos y `totalCount` es 2

#### Scenario: Página fuera de rango
- **WHEN** existen 25 categorías activas y un cliente con `inventory.read` pide `page=9&pageSize=20`
- **THEN** la respuesta es 200 con `items` vacío, `page` igual a 9, `totalCount` igual a 25 y `totalPages` igual a 2

#### Scenario: Sin categorías
- **WHEN** no existe ninguna categoría activa
- **THEN** la respuesta es 200 con `items` vacío, `totalCount` igual a 0 y `totalPages` igual a 0

### Requirement: Parámetros de paginación válidos (BR-18)
`page` SHALL ser un entero desde 1, con 1 como valor por defecto. `pageSize` SHALL ser un entero de 1 a 100, con 20 como valor por defecto. Un valor fuera de rango o que no sea un entero MUST responder 400 con un mensaje en español que nombre el parámetro.

#### Scenario: page igual a 0
- **WHEN** un cliente con `inventory.read` pide `GET /api/categories?page=0`
- **THEN** la respuesta es 400 con un mensaje en español sobre `page`

#### Scenario: pageSize igual a 0
- **WHEN** un cliente con `inventory.read` pide `GET /api/categories?pageSize=0`
- **THEN** la respuesta es 400 con un mensaje en español sobre `pageSize`

#### Scenario: pageSize igual a 101
- **WHEN** un cliente con `inventory.read` pide `GET /api/categories?pageSize=101`
- **THEN** la respuesta es 400 con un mensaje en español sobre `pageSize`

#### Scenario: pageSize igual a 100
- **WHEN** un cliente con `inventory.read` pide `GET /api/categories?pageSize=100`
- **THEN** la respuesta es 200 y `pageSize` es 100

#### Scenario: Parámetro que no es un número
- **WHEN** un cliente con `inventory.read` pide `GET /api/categories?page=abc`
- **THEN** la respuesta es 400 con un mensaje en español sobre `page`

### Requirement: Desactivar una categoría (BR-05)
`DELETE /api/categories/{id}` SHALL exigir el scope `inventory.write` y desactivar la categoría (borrado lógico) sin borrar la fila. Responde 204 si la desactiva, 404 si no existe o ya estaba desactivada, y MUST responder 409 con un mensaje en español si tiene algún producto activo.

#### Scenario: Categoría sin productos
- **WHEN** un cliente con `inventory.write` borra una categoría activa sin productos
- **THEN** la respuesta es 204
- **AND** `GET /api/categories/{id}` responde 404 y la categoría no aparece en `GET /api/categories`

#### Scenario: Categoría con un producto activo
- **WHEN** un cliente con `inventory.write` borra una categoría que tiene al menos un producto activo
- **THEN** la respuesta es 409 con un mensaje en español que indica que la categoría tiene productos activos
- **AND** la categoría sigue activa

#### Scenario: Categoría con solo productos inactivos
- **WHEN** un cliente con `inventory.write` borra una categoría cuyos productos están todos inactivos
- **THEN** la respuesta es 204

#### Scenario: Categoría ya desactivada
- **WHEN** un cliente con `inventory.write` borra una categoría que ya estaba desactivada
- **THEN** la respuesta es 404

#### Scenario: Id inexistente
- **WHEN** un cliente con `inventory.write` borra un id con formato válido que no corresponde a ninguna categoría
- **THEN** la respuesta es 404 con un mensaje en español

#### Scenario: Token sin inventory.write
- **WHEN** un cliente con un token válido que solo tiene `inventory.read` llama a `DELETE /api/categories/{id}`
- **THEN** la respuesta es 403 con el cuerpo vacío
