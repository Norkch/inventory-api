# Spec Delta

## Purpose

Registrar qué proveedores suministran cada producto mediante una asociación muchos a muchos, única por pareja e inmutable, y consultarla desde el producto y desde el proveedor, respetando las reglas BR-09, BR-10 y BR-18.

## ADDED Requirements

### Requirement: Asociar un proveedor a un producto
`POST /api/products/{productId}/suppliers` SHALL exigir el scope `inventory.write` y aceptar un cuerpo JSON con `supplierId` y `supplierSku` (opcional). Si la asociación es válida, la API MUST guardarla y responder 201 con la asociación creada y la cabecera `Location` apuntando a `/api/products/{productId}/suppliers/{supplierId}`.

#### Scenario: Asociación correcta
- **WHEN** el producto activo `"Tornillo 3/8"` y el proveedor activo `"Ferretería Andina"` no están asociados, y un cliente con `inventory.write` envía `{ "supplierId": "<id del proveedor>", "supplierSku": "FA-778" }` a `POST /api/products/{id del producto}/suppliers`
- **THEN** la respuesta es 201 con `productId`, `productName` igual a `"Tornillo 3/8"`, `supplierId`, `supplierName` igual a `"Ferretería Andina"`, `supplierSku` igual a `"FA-778"` y `createdAtUtc` en UTC
- **AND** la cabecera `Location` es `/api/products/{productId}/suppliers/{supplierId}`

#### Scenario: Asociación sin código del proveedor
- **WHEN** un cliente con `inventory.write` asocia un proveedor activo a un producto activo enviando solo `supplierId`
- **THEN** la respuesta es 201 con `supplierSku` igual a `null`

#### Scenario: Token sin inventory.write
- **WHEN** un cliente con un token válido que solo tiene `inventory.read` llama a `POST /api/products/{productId}/suppliers`
- **THEN** la respuesta es 403 con el cuerpo vacío

### Requirement: Código del producto en el proveedor opcional y acotado
`supplierSku` SHALL ser opcional. La API MUST recortar sus espacios de los extremos y guardar como `null` uno vacío o compuesto solo por espacios. Uno de más de 50 caracteres después de recortarlo MUST rechazarse con 400 y un mensaje en español. No tiene que ser único.

#### Scenario: supplierSku con espacios en los extremos
- **WHEN** un cliente con `inventory.write` crea una asociación válida con `supplierSku` igual a `"  FA-778  "`
- **THEN** la respuesta es 201 y `supplierSku` es `"FA-778"`

#### Scenario: supplierSku solo con espacios
- **WHEN** un cliente con `inventory.write` crea una asociación válida con `supplierSku` igual a `"   "`
- **THEN** la respuesta es 201 y `supplierSku` es `null`

#### Scenario: supplierSku de 50 caracteres
- **WHEN** un cliente con `inventory.write` crea una asociación válida con un `supplierSku` de 50 caracteres
- **THEN** la respuesta es 201

#### Scenario: supplierSku de 51 caracteres
- **WHEN** un cliente con `inventory.write` crea una asociación con un `supplierSku` de 51 caracteres
- **THEN** la respuesta es 400 con un mensaje en español que indica el máximo de 50 caracteres

### Requirement: Producto de la ruta existente y activo
La asociación SHALL crearse solo sobre un producto activo. Si el producto de la ruta no existe o está borrado, la API MUST responder 404 con un mensaje en español y MUST NOT crear la asociación.

#### Scenario: Producto inexistente
- **WHEN** un cliente con `inventory.write` asocia un proveedor activo a un GUID que no corresponde a ningún producto
- **THEN** la respuesta es 404 con un mensaje en español que indica que no existe un producto activo con ese id

#### Scenario: Producto borrado
- **WHEN** un cliente con `inventory.write` asocia un proveedor activo a un producto borrado
- **THEN** la respuesta es 404 y no se crea ninguna asociación

### Requirement: Proveedor existente y activo (BR-09)
`supplierId` SHALL ser obligatorio y MUST corresponder a un proveedor activo. Si falta, o si el proveedor no existe o está desactivado, la API MUST responder 400 con un mensaje en español. Un proveedor MUST NOT quedar asociado después de desactivarse, ni siquiera cuando el alta coincide con su desactivación.

#### Scenario: supplierId ausente
- **WHEN** un cliente con `inventory.write` crea una asociación sobre un producto activo sin `supplierId`
- **THEN** la respuesta es 400 con un mensaje en español que indica que el proveedor es obligatorio

#### Scenario: supplierId que no es un GUID
- **WHEN** un cliente con `inventory.write` envía `"supplierId": "abc"`
- **THEN** la respuesta es 400 con un mensaje en español

#### Scenario: Proveedor inexistente
- **WHEN** un cliente con `inventory.write` asocia a un producto activo un `supplierId` que no corresponde a ningún proveedor
- **THEN** la respuesta es 400 con un mensaje en español que indica que el proveedor no existe o no está activo

#### Scenario: Proveedor desactivado
- **WHEN** un cliente con `inventory.write` asocia a un producto activo un proveedor desactivado
- **THEN** la respuesta es 400 con el mismo mensaje que para un proveedor inexistente

#### Scenario: Alta simultánea con la desactivación del proveedor
- **WHEN** un cliente asocia el proveedor P a un producto activo a la vez que otro cliente desactiva P
- **THEN** o el alta responde 201 y la desactivación 204, o la desactivación responde 204 y el alta 400
- **AND** no existe ninguna asociación creada después de que P quedara desactivado

### Requirement: Pareja producto-proveedor única (BR-10)
Un producto SHALL poder tener varios proveedores y un proveedor varios productos, pero la pareja (producto, proveedor) MUST ser única. Repetir una pareja MUST responder 409 con un mensaje en español, también cuando dos altas de la misma pareja llegan a la vez.

#### Scenario: Pareja repetida
- **WHEN** el producto A ya está asociado al proveedor P y un cliente con `inventory.write` vuelve a asociar P a A
- **THEN** la respuesta es 409 con un mensaje en español que indica que el proveedor ya está asociado al producto

#### Scenario: Un producto con varios proveedores
- **WHEN** el producto A está asociado al proveedor P y un cliente con `inventory.write` asocia el proveedor Q a A
- **THEN** la respuesta es 201

#### Scenario: Un proveedor con varios productos
- **WHEN** el proveedor P está asociado al producto A y un cliente con `inventory.write` asocia P al producto B
- **THEN** la respuesta es 201

#### Scenario: Altas simultáneas de la misma pareja
- **WHEN** dos clientes con `inventory.write` asocian a la vez el proveedor P al producto A, que no estaban asociados
- **THEN** una petición recibe 201 y la otra 409
- **AND** solo existe una asociación entre A y P

### Requirement: Orden de los errores del alta de asociación
Cuando un alta incumple varias reglas, la API SHALL responder con el primer error en este orden: el formato de `supplierId` y `supplierSku` (400), después el producto de la ruta (404), después el proveedor (400) y por último la pareja repetida (409).

#### Scenario: supplierSku inválido sobre producto inexistente
- **WHEN** un cliente con `inventory.write` envía un `supplierSku` de 51 caracteres a un producto inexistente
- **THEN** la respuesta es 400

#### Scenario: Proveedor desactivado sobre producto borrado
- **WHEN** un cliente con `inventory.write` asocia un proveedor desactivado a un producto borrado
- **THEN** la respuesta es 404

#### Scenario: Pareja repetida con el proveedor desactivado
- **WHEN** el producto A está asociado al proveedor P, P se desactiva y un cliente con `inventory.write` vuelve a asociar P a A
- **THEN** la respuesta es 400 con el mensaje del proveedor

### Requirement: La asociación no se edita ni se borra
La API SHALL NOT ofrecer ninguna forma de modificar ni borrar una asociación. Las peticiones `PUT`, `PATCH` y `DELETE` sobre las rutas de asociaciones MUST NOT cambiar ninguna asociación. Crear un producto MUST NOT asociarle proveedores.

#### Scenario: DELETE sobre una asociación
- **WHEN** un cliente con `inventory.write` envía `DELETE /api/products/{productId}/suppliers/{supplierId}` sobre una asociación existente
- **THEN** la respuesta es 405 y la asociación no cambia

#### Scenario: PUT sobre una asociación
- **WHEN** un cliente con `inventory.write` envía `PUT /api/products/{productId}/suppliers/{supplierId}`
- **THEN** la respuesta es 405 y la asociación no cambia

#### Scenario: Producto recién creado
- **WHEN** un cliente crea un producto y después pide `GET /api/products/{id}/suppliers`
- **THEN** la respuesta es 200 con `items` vacío

### Requirement: Consultar una asociación
`GET /api/products/{productId}/suppliers/{supplierId}` SHALL exigir el scope `inventory.read` y responder 200 con la representación de la asociación. Si la pareja no existe, o si el producto o el proveedor no están activos, MUST responder 404 con un mensaje en español.

#### Scenario: Asociación visible
- **WHEN** el producto activo A está asociado al proveedor activo P y un cliente con `inventory.read` pide `GET /api/products/{A}/suppliers/{P}`
- **THEN** la respuesta es 200 con `productId`, `productName`, `supplierId`, `supplierName`, `supplierSku` y `createdAtUtc`

#### Scenario: Pareja no asociada
- **WHEN** el producto activo A y el proveedor activo P no están asociados y un cliente con `inventory.read` pide `GET /api/products/{A}/suppliers/{P}`
- **THEN** la respuesta es 404 con un mensaje en español que indica que el proveedor no está asociado al producto

#### Scenario: Proveedor desactivado
- **WHEN** el producto activo A está asociado al proveedor P, P está desactivado y un cliente con `inventory.read` pide la asociación
- **THEN** la respuesta es 404 con el mismo mensaje que para una pareja no asociada

#### Scenario: Producto borrado
- **WHEN** el producto A está borrado, estaba asociado al proveedor activo P y un cliente con `inventory.read` pide la asociación
- **THEN** la respuesta es 404 con el mismo mensaje que para una pareja no asociada

#### Scenario: Token sin inventory.read
- **WHEN** un cliente con un token válido que solo tiene `inventory.write` llama a `GET /api/products/{productId}/suppliers/{supplierId}`
- **THEN** la respuesta es 403 con el cuerpo vacío

### Requirement: Listar los proveedores de un producto (BR-18)
`GET /api/products/{productId}/suppliers` SHALL exigir el scope `inventory.read` y devolver las asociaciones del producto cuyo proveedor está activo, ordenadas por nombre del proveedor y, a igualdad, por `supplierId`. La respuesta MUST tener la forma `{ items, page, pageSize, totalCount, totalPages }`, con las reglas de paginación de categorías.

#### Scenario: Orden por nombre del proveedor
- **WHEN** el producto activo A está asociado a los proveedores activos `"Tornillos Sur"`, `"Aceros Norte"` y `"Maderas Centro"`
- **THEN** `items` los devuelve en el orden `"Aceros Norte"`, `"Maderas Centro"`, `"Tornillos Sur"`

#### Scenario: Excluye los proveedores desactivados
- **WHEN** el producto activo A está asociado a dos proveedores activos y a uno desactivado
- **THEN** `items` tiene dos elementos y `totalCount` es 2

#### Scenario: Solo las asociaciones del producto
- **WHEN** existen asociaciones de dos productos activos y un cliente con `inventory.read` lista las del primero
- **THEN** `items` solo contiene asociaciones con `productId` igual al primero

#### Scenario: Producto activo sin proveedores
- **WHEN** un cliente con `inventory.read` lista los proveedores de un producto activo que no tiene ninguno
- **THEN** la respuesta es 200 con `items` vacío y `totalCount` igual a 0

#### Scenario: pageSize igual a 101
- **WHEN** un cliente con `inventory.read` pide `GET /api/products/{productId}/suppliers?pageSize=101`
- **THEN** la respuesta es 400 con un mensaje en español sobre `pageSize`

#### Scenario: Token sin inventory.read
- **WHEN** un cliente con un token válido que solo tiene `inventory.write` llama a `GET /api/products/{productId}/suppliers`
- **THEN** la respuesta es 403 con el cuerpo vacío

### Requirement: Proveedores de un producto inexistente o borrado
Si el producto de la ruta no existe o está borrado, `GET /api/products/{productId}/suppliers` SHALL responder 404 con un mensaje en español, aunque conserve asociaciones. Los errores de paginación MUST comprobarse antes que el producto.

#### Scenario: Producto borrado con asociaciones
- **WHEN** un producto borrado tiene asociaciones y un cliente con `inventory.read` lista sus proveedores
- **THEN** la respuesta es 404 con un mensaje en español que indica que no existe un producto activo con ese id

#### Scenario: Paginación inválida sobre producto borrado
- **WHEN** un cliente con `inventory.read` pide `GET /api/products/{productId}/suppliers?page=0` sobre un producto borrado
- **THEN** la respuesta es 400

### Requirement: Listar los productos de un proveedor (BR-18)
`GET /api/suppliers/{supplierId}/products` SHALL exigir el scope `inventory.read` y devolver las asociaciones del proveedor cuyo producto está activo, ordenadas por nombre del producto y, a igualdad, por `productId`. La respuesta MUST tener la forma `{ items, page, pageSize, totalCount, totalPages }`, con las reglas de paginación de categorías.

#### Scenario: Orden por nombre del producto
- **WHEN** el proveedor activo P está asociado a los productos activos `"Tuerca"`, `"Arandela"` y `"Tornillo"`
- **THEN** `items` los devuelve en el orden `"Arandela"`, `"Tornillo"`, `"Tuerca"`

#### Scenario: Excluye los productos borrados
- **WHEN** el proveedor activo P está asociado a dos productos activos y a uno borrado
- **THEN** `items` tiene dos elementos y `totalCount` es 2

#### Scenario: Proveedor activo sin productos
- **WHEN** un cliente con `inventory.read` lista los productos de un proveedor activo que no tiene ninguno
- **THEN** la respuesta es 200 con `items` vacío y `totalCount` igual a 0

#### Scenario: Token sin inventory.read
- **WHEN** un cliente con un token válido que solo tiene `inventory.write` llama a `GET /api/suppliers/{supplierId}/products`
- **THEN** la respuesta es 403 con el cuerpo vacío

### Requirement: Productos de un proveedor inexistente o desactivado
Si el proveedor de la ruta no existe o está desactivado, `GET /api/suppliers/{supplierId}/products` SHALL responder 404 con un mensaje en español, aunque conserve asociaciones. Los errores de paginación MUST comprobarse antes que el proveedor.

#### Scenario: Proveedor desactivado con asociaciones
- **WHEN** un proveedor desactivado tiene asociaciones y un cliente con `inventory.read` lista sus productos
- **THEN** la respuesta es 404 con un mensaje en español que indica que no existe un proveedor activo con ese id

#### Scenario: Paginación inválida sobre proveedor desactivado
- **WHEN** un cliente con `inventory.read` pide `GET /api/suppliers/{supplierId}/products?pageSize=0` sobre un proveedor desactivado
- **THEN** la respuesta es 400
