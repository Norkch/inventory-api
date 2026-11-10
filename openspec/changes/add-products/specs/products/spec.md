# Spec Delta

## Purpose

Gestionar los productos del inventario: crearlos, consultarlos uno a uno o en listados paginados con filtros, editarlos y borrarlos de forma lógica, respetando las reglas BR-01, BR-02, BR-03, BR-06, BR-07 y BR-18.

## ADDED Requirements

### Requirement: Crear un producto
`POST /api/products` SHALL exigir el scope `inventory.write` y aceptar un cuerpo JSON con `sku`, `name`, `description` (opcional) y `categoryId`. Si el producto es válido, la API MUST guardarlo como activo y responder 201 con el producto creado y la cabecera `Location` apuntando a `/api/products/{id}`.

#### Scenario: Alta correcta
- **WHEN** existe la categoría activa `"Tornillería"` y un cliente con `inventory.write` envía `{ "sku": "TOR-001", "name": "Tornillo 3/8", "description": "Acero inoxidable", "categoryId": "<id de Tornillería>" }`
- **THEN** la respuesta es 201 con `id`, `sku` igual a `"TOR-001"`, `name` igual a `"Tornillo 3/8"`, `description` igual a `"Acero inoxidable"`, `categoryId`, `categoryName` igual a `"Tornillería"`, `stockQuantity` igual a 0, `createdAtUtc` en UTC y `updatedAtUtc` igual a `null`
- **AND** la cabecera `Location` es `/api/products/{id}` con el `id` devuelto

#### Scenario: Alta sin descripción
- **WHEN** un cliente con `inventory.write` crea un producto válido sin `description`
- **THEN** la respuesta es 201 y `description` es `null`

#### Scenario: Token sin inventory.write
- **WHEN** un cliente con un token válido que solo tiene `inventory.read` llama a `POST /api/products`
- **THEN** la respuesta es 403 con el cuerpo vacío

### Requirement: Stock inicial cero (BR-07)
Un producto nuevo SHALL empezar con `stockQuantity` igual a 0. Si el cuerpo del alta incluye `stockQuantity`, la API MUST ignorarlo sin responder error.

#### Scenario: Alta con stockQuantity en el cuerpo
- **WHEN** un cliente con `inventory.write` crea un producto válido enviando además `"stockQuantity": 50`
- **THEN** la respuesta es 201 y `stockQuantity` es 0

### Requirement: SKU obligatorio y acotado (BR-01)
La API SHALL recortar los espacios de los extremos del SKU antes de validarlo y guardarlo. Un SKU ausente, vacío o compuesto solo por espacios, o con más de 50 caracteres después de recortarlo, MUST rechazarse con 400 y un mensaje en español.

#### Scenario: SKU con espacios en los extremos
- **WHEN** un cliente con `inventory.write` crea un producto con `sku` igual a `"  TOR-001  "`
- **THEN** la respuesta es 201 y `sku` es `"TOR-001"`

#### Scenario: SKU ausente
- **WHEN** un cliente con `inventory.write` crea un producto sin `sku`
- **THEN** la respuesta es 400 con un mensaje en español que indica que el SKU es obligatorio

#### Scenario: SKU solo con espacios
- **WHEN** un cliente con `inventory.write` crea un producto con `sku` igual a `"   "`
- **THEN** la respuesta es 400 con un mensaje en español que indica que el SKU es obligatorio

#### Scenario: SKU de 50 caracteres
- **WHEN** un cliente con `inventory.write` crea un producto con un `sku` de 50 caracteres
- **THEN** la respuesta es 201

#### Scenario: SKU de 51 caracteres
- **WHEN** un cliente con `inventory.write` crea un producto con un `sku` de 51 caracteres
- **THEN** la respuesta es 400 con un mensaje en español que indica el máximo de 50 caracteres

### Requirement: SKU único y reservado para siempre (BR-01)
El SKU SHALL ser único entre todos los productos, activos o borrados, sin distinguir mayúsculas. Crear un producto con el SKU de otro MUST responder 409 con un mensaje en español, también cuando el otro está borrado y cuando dos altas con el mismo SKU llegan a la vez. El mensaje MUST NOT indicar si el otro producto está borrado.

#### Scenario: SKU repetido con otras mayúsculas
- **WHEN** existe un producto con `sku` igual a `"TOR-001"` y un cliente con `inventory.write` crea otro con `sku` igual a `"tor-001"`
- **THEN** la respuesta es 409 con un mensaje en español que indica que ya existe un producto con ese SKU

#### Scenario: SKU repetido con espacios en los extremos
- **WHEN** existe un producto con `sku` igual a `"TOR-001"` y un cliente con `inventory.write` crea otro con `sku` igual a `" TOR-001 "`
- **THEN** la respuesta es 409

#### Scenario: SKU de un producto borrado
- **WHEN** el producto con `sku` igual a `"TOR-001"` está borrado y un cliente con `inventory.write` crea otro con `sku` igual a `"TOR-001"`
- **THEN** la respuesta es 409 con el mismo mensaje que para un producto activo

#### Scenario: Altas simultáneas con el mismo SKU
- **WHEN** dos clientes con `inventory.write` crean a la vez un producto con `sku` igual a `"TOR-002"`
- **THEN** una petición recibe 201 y la otra 409
- **AND** solo existe un producto con ese SKU

### Requirement: Nombre de producto obligatorio y acotado (BR-02)
La API SHALL recortar los espacios de los extremos del nombre antes de validarlo y guardarlo. Un nombre ausente, vacío o compuesto solo por espacios, o con más de 200 caracteres después de recortarlo, MUST rechazarse con 400 y un mensaje en español. El nombre no tiene que ser único.

#### Scenario: Nombre con espacios en los extremos
- **WHEN** un cliente con `inventory.write` crea un producto con `name` igual a `"  Tornillo 3/8  "`
- **THEN** la respuesta es 201 y `name` es `"Tornillo 3/8"`

#### Scenario: Nombre ausente
- **WHEN** un cliente con `inventory.write` crea un producto sin `name`
- **THEN** la respuesta es 400 con un mensaje en español que indica que el nombre es obligatorio

#### Scenario: Nombre de 200 caracteres
- **WHEN** un cliente con `inventory.write` crea un producto con un `name` de 200 caracteres
- **THEN** la respuesta es 201

#### Scenario: Nombre de 201 caracteres
- **WHEN** un cliente con `inventory.write` crea un producto con un `name` de 201 caracteres
- **THEN** la respuesta es 400 con un mensaje en español que indica el máximo de 200 caracteres

#### Scenario: Nombre repetido
- **WHEN** existe un producto con `name` igual a `"Tornillo 3/8"` y un cliente con `inventory.write` crea otro con el mismo nombre y otro SKU
- **THEN** la respuesta es 201

### Requirement: Descripción de producto opcional y acotada
`description` SHALL ser opcional. Si se envía, la API MUST recortar sus espacios de los extremos. Una descripción vacía o compuesta solo por espacios MUST guardarse como `null`, y una de más de 1000 caracteres después de recortarla MUST rechazarse con 400 y un mensaje en español.

#### Scenario: Descripción solo con espacios
- **WHEN** un cliente con `inventory.write` crea un producto con `description` igual a `"   "`
- **THEN** la respuesta es 201 y `description` es `null`

#### Scenario: Descripción de 1000 caracteres
- **WHEN** un cliente con `inventory.write` crea un producto con una `description` de 1000 caracteres
- **THEN** la respuesta es 201

#### Scenario: Descripción de 1001 caracteres
- **WHEN** un cliente con `inventory.write` crea un producto con una `description` de 1001 caracteres
- **THEN** la respuesta es 400 con un mensaje en español que indica el máximo de 1000 caracteres

### Requirement: Categoría existente y activa (BR-03)
`categoryId` SHALL ser obligatorio y MUST corresponder a una categoría activa. Si falta, o si la categoría no existe o está desactivada, la API MUST responder 400 con un mensaje en español. Un producto activo MUST NOT quedar asociado a una categoría desactivada, ni siquiera cuando el alta o la edición coinciden con la desactivación de la categoría.

#### Scenario: categoryId ausente
- **WHEN** un cliente con `inventory.write` crea un producto sin `categoryId`
- **THEN** la respuesta es 400 con un mensaje en español que indica que la categoría es obligatoria

#### Scenario: Categoría inexistente
- **WHEN** un cliente con `inventory.write` crea un producto con un `categoryId` que no corresponde a ninguna categoría
- **THEN** la respuesta es 400 con un mensaje en español que indica que la categoría no existe o no está activa

#### Scenario: Categoría desactivada
- **WHEN** un cliente con `inventory.write` crea un producto con el `categoryId` de una categoría desactivada
- **THEN** la respuesta es 400 con el mismo mensaje que para una categoría inexistente

#### Scenario: categoryId que no es un GUID
- **WHEN** un cliente con `inventory.write` envía `"categoryId": "abc"`
- **THEN** la respuesta es 400 con un mensaje en español

#### Scenario: Alta simultánea con la desactivación de la categoría
- **WHEN** un cliente crea un producto en la categoría C, que no tiene productos activos, a la vez que otro cliente desactiva C
- **THEN** o el alta responde 201 y la desactivación 409, o la desactivación responde 204 y el alta 400
- **AND** no queda ningún producto activo en una categoría desactivada

### Requirement: Orden de los errores del alta
Cuando un alta incumple varias reglas, la API SHALL responder con el primer error en este orden: el formato de `sku`, `name` y `description` (400), después la categoría (400) y por último el SKU repetido (409).

#### Scenario: Nombre vacío y categoría inexistente
- **WHEN** un cliente con `inventory.write` crea un producto con `name` igual a `""` y un `categoryId` inexistente
- **THEN** la respuesta es 400 con el mensaje del nombre obligatorio

#### Scenario: Categoría inexistente y SKU repetido
- **WHEN** un cliente con `inventory.write` crea un producto con el SKU de otro producto y un `categoryId` inexistente
- **THEN** la respuesta es 400 con el mensaje de la categoría

### Requirement: Consultar un producto por id
`GET /api/products/{id}` SHALL exigir el scope `inventory.read` y responder 200 con la representación del producto activo, incluidos `categoryName` y `stockQuantity`. Si no existe ningún producto activo con ese id, MUST responder 404 con un mensaje en español.

#### Scenario: Producto activo
- **WHEN** un cliente con `inventory.read` pide el id de un producto activo de la categoría `"Tornillería"`
- **THEN** la respuesta es 200 con `id`, `sku`, `name`, `description`, `categoryId`, `categoryName` igual a `"Tornillería"`, `stockQuantity`, `createdAtUtc` y `updatedAtUtc`

#### Scenario: Id inexistente
- **WHEN** un cliente con `inventory.read` pide un GUID que no corresponde a ningún producto
- **THEN** la respuesta es 404 con un mensaje en español

#### Scenario: Producto borrado
- **WHEN** un cliente con `inventory.read` pide el id de un producto borrado
- **THEN** la respuesta es 404 con el mismo mensaje que para un id inexistente

#### Scenario: Token sin inventory.read
- **WHEN** un cliente con un token válido que solo tiene `inventory.write` llama a `GET /api/products/{id}`
- **THEN** la respuesta es 403 con el cuerpo vacío

### Requirement: Listar productos paginados (BR-18)
`GET /api/products` SHALL exigir el scope `inventory.read` y devolver solo los productos activos, con la representación completa de cada uno, ordenados por `name` ascendente y, a igualdad de nombre, por `id`. La respuesta MUST tener la forma `{ items, page, pageSize, totalCount, totalPages }`, donde `totalCount` cuenta los productos activos que cumplen los filtros. `page` y `pageSize` siguen las mismas reglas que en categorías.

#### Scenario: Primera página por defecto
- **WHEN** existen 25 productos activos y un cliente con `inventory.read` llama a `GET /api/products` sin parámetros
- **THEN** la respuesta es 200 con 20 elementos en `items`, `page` igual a 1, `pageSize` igual a 20, `totalCount` igual a 25 y `totalPages` igual a 2

#### Scenario: Orden por nombre
- **WHEN** existen los productos activos `"Tuerca"`, `"Arandela"` y `"Tornillo"`
- **THEN** `items` los devuelve en el orden `"Arandela"`, `"Tornillo"`, `"Tuerca"`

#### Scenario: Excluye los borrados
- **WHEN** existen dos productos activos y uno borrado
- **THEN** `items` tiene dos elementos y `totalCount` es 2

#### Scenario: Nombre de la categoría y stock
- **WHEN** existe un producto activo de la categoría `"Tornillería"` con `stockQuantity` igual a 0
- **THEN** su elemento en `items` tiene `categoryName` igual a `"Tornillería"` y `stockQuantity` igual a 0

#### Scenario: pageSize igual a 101
- **WHEN** un cliente con `inventory.read` pide `GET /api/products?pageSize=101`
- **THEN** la respuesta es 400 con un mensaje en español sobre `pageSize`

#### Scenario: Token sin inventory.read
- **WHEN** un cliente con un token válido que solo tiene `inventory.write` llama a `GET /api/products`
- **THEN** la respuesta es 403 con el cuerpo vacío

### Requirement: Filtrar el listado por categoría
`categoryId` SHALL ser un parámetro opcional del listado. Si se envía, la API MUST devolver solo los productos activos de esa categoría. Un `categoryId` que no corresponde a ninguna categoría activa MUST responder 200 con `items` vacío, y uno que no es un GUID MUST responder 400 con un mensaje en español que nombre `categoryId`.

#### Scenario: Productos de una categoría
- **WHEN** existen dos productos activos en la categoría A y uno en la categoría B, y un cliente con `inventory.read` pide `GET /api/products?categoryId=<id de A>`
- **THEN** `items` tiene los dos productos de A y `totalCount` es 2

#### Scenario: Categoría inexistente en el filtro
- **WHEN** un cliente con `inventory.read` pide `GET /api/products?categoryId=<GUID inexistente>`
- **THEN** la respuesta es 200 con `items` vacío y `totalCount` igual a 0

#### Scenario: categoryId que no es un GUID en el filtro
- **WHEN** un cliente con `inventory.read` pide `GET /api/products?categoryId=abc`
- **THEN** la respuesta es 400 con un mensaje en español que nombra `categoryId`

### Requirement: Buscar en el listado por nombre o SKU
`search` SHALL ser un parámetro opcional del listado. La API MUST recortar sus espacios de los extremos y devolver los productos activos cuyo `name` o `sku` contiene el texto, sin distinguir mayúsculas. Los caracteres `%`, `_` y `[` MUST buscarse de forma literal. Un `search` vacío o solo con espacios MUST tratarse como ausente. Se puede combinar con `categoryId`.

#### Scenario: Coincidencia en el nombre
- **WHEN** existen los productos `"Tornillo 3/8"` y `"Tuerca"`, y un cliente con `inventory.read` pide `search=TORN`
- **THEN** `items` tiene solo `"Tornillo 3/8"`

#### Scenario: Coincidencia en el SKU
- **WHEN** existe un producto con `sku` igual a `"TOR-001"` y `name` igual a `"Perno"`, y un cliente con `inventory.read` pide `search=r-00`
- **THEN** `items` incluye ese producto

#### Scenario: Comodín buscado de forma literal
- **WHEN** existen los productos `"Descuento 50%"` y `"Caja 500"`, y un cliente con `inventory.read` pide `search=50%`
- **THEN** `items` tiene solo `"Descuento 50%"`

#### Scenario: Búsqueda solo con espacios
- **WHEN** existen tres productos activos y un cliente con `inventory.read` pide `search=%20%20`
- **THEN** `items` tiene los tres productos

#### Scenario: Búsqueda combinada con categoría
- **WHEN** existen `"Tornillo 3/8"` en la categoría A y `"Tornillo 1/2"` en la categoría B, y un cliente con `inventory.read` pide `categoryId=<id de A>&search=tornillo`
- **THEN** `items` tiene solo `"Tornillo 3/8"`

### Requirement: Editar un producto
`PUT /api/products/{id}` SHALL exigir el scope `inventory.write` y aceptar `name`, `description` y `categoryId`, con las mismas reglas que el alta. Si el producto está activo, la API MUST guardar los cambios, fijar `updatedAtUtc` y responder 200 con la representación actualizada. Si no hay ningún producto activo con ese id, MUST responder 404.

#### Scenario: Edición correcta
- **WHEN** un cliente con `inventory.write` envía a un producto activo `{ "name": "Tornillo 1/2", "description": null, "categoryId": "<id de otra categoría activa>" }`
- **THEN** la respuesta es 200 con `name` igual a `"Tornillo 1/2"`, `description` igual a `null`, el nuevo `categoryId` y su `categoryName`, y `updatedAtUtc` en UTC
- **AND** `sku`, `stockQuantity` y `createdAtUtc` no cambian

#### Scenario: Edición con una categoría desactivada
- **WHEN** un cliente con `inventory.write` edita un producto activo con el `categoryId` de una categoría desactivada
- **THEN** la respuesta es 400 con el mensaje de la categoría y el producto no cambia

#### Scenario: Edición con un nombre vacío
- **WHEN** un cliente con `inventory.write` edita un producto activo con `name` igual a `""`
- **THEN** la respuesta es 400 con el mensaje del nombre obligatorio y el producto no cambia

#### Scenario: Producto borrado
- **WHEN** un cliente con `inventory.write` edita un producto borrado
- **THEN** la respuesta es 404 con un mensaje en español

#### Scenario: Id inexistente
- **WHEN** un cliente con `inventory.write` edita un GUID que no corresponde a ningún producto
- **THEN** la respuesta es 404 con un mensaje en español

#### Scenario: Token sin inventory.write
- **WHEN** un cliente con un token válido que solo tiene `inventory.read` llama a `PUT /api/products/{id}`
- **THEN** la respuesta es 403 con el cuerpo vacío

### Requirement: La edición no cambia el SKU ni el stock (BR-01, BR-07)
La edición SHALL ignorar `sku` y `stockQuantity` si vienen en el cuerpo, sin responder error. El SKU y el stock de un producto MUST NOT cambiar por un `PUT`.

#### Scenario: Edición con otro SKU
- **WHEN** un cliente con `inventory.write` edita el producto con `sku` igual a `"TOR-001"` enviando además `"sku": "OTRO-001"`
- **THEN** la respuesta es 200 y `sku` sigue siendo `"TOR-001"`

#### Scenario: Edición con stockQuantity
- **WHEN** un cliente con `inventory.write` edita un producto con `stockQuantity` igual a 0 enviando además `"stockQuantity": 99`
- **THEN** la respuesta es 200 y `stockQuantity` sigue siendo 0

### Requirement: Borrar un producto (BR-06)
`DELETE /api/products/{id}` SHALL exigir el scope `inventory.write` y borrar el producto de forma lógica, sin borrar la fila ni sus movimientos. Responde 204 si lo borra y MUST responder 404 con un mensaje en español si no existe o ya estaba borrado. Se permite aunque el producto tenga stock; ese stock se conserva sin cambios.

#### Scenario: Producto activo sin stock
- **WHEN** un cliente con `inventory.write` borra un producto activo con `stockQuantity` igual a 0
- **THEN** la respuesta es 204 sin cuerpo
- **AND** `GET /api/products/{id}` responde 404 y el producto no aparece en `GET /api/products`

#### Scenario: Producto activo con stock
- **WHEN** un cliente con `inventory.write` borra un producto activo con `stockQuantity` igual a 15
- **THEN** la respuesta es 204
- **AND** la fila del producto se conserva con `StockQuantity` igual a 15 y `IsActive` igual a falso

#### Scenario: Se conservan los movimientos
- **WHEN** un cliente con `inventory.write` borra un producto activo que tiene movimientos de inventario
- **THEN** la respuesta es 204 y todos sus movimientos se conservan sin cambios

#### Scenario: Producto ya borrado
- **WHEN** un cliente con `inventory.write` borra un producto que ya estaba borrado
- **THEN** la respuesta es 404 con un mensaje en español

#### Scenario: Id inexistente
- **WHEN** un cliente con `inventory.write` borra un GUID que no corresponde a ningún producto
- **THEN** la respuesta es 404 con un mensaje en español

#### Scenario: La categoría deja de tener productos activos
- **WHEN** se borra el único producto activo de una categoría y después un cliente con `inventory.write` desactiva esa categoría
- **THEN** la desactivación de la categoría responde 204

#### Scenario: Token sin inventory.write
- **WHEN** un cliente con un token válido que solo tiene `inventory.read` llama a `DELETE /api/products/{id}`
- **THEN** la respuesta es 403 con el cuerpo vacío
