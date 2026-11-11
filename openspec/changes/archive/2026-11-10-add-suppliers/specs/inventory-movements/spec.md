# Spec Delta

## ADDED Requirements

### Requirement: Proveedor opcional en las entradas (BR-16)
Una entrada SHALL poder indicar `supplierId`. Si lo indica, el proveedor MUST existir, estar activo y estar asociado al producto; si no, la API MUST responder 400 con un mensaje en español y MUST NOT registrar el movimiento ni cambiar el stock. Una entrada sin `supplierId` o con `supplierId` igual a `null` MUST registrarse sin proveedor.

#### Scenario: Entrada con un proveedor asociado
- **WHEN** el producto activo A está asociado al proveedor activo P, tiene `stockQuantity` igual a 0 y un cliente con `inventory.write` envía `{ "type": "Entry", "quantity": 10, "supplierId": "<id de P>" }`
- **THEN** la respuesta es 201 con `supplierId` igual al id de P y `stockAfter` igual a 10

#### Scenario: Entrada sin proveedor
- **WHEN** un cliente con `inventory.write` registra una entrada válida sin `supplierId`
- **THEN** la respuesta es 201 con `supplierId` igual a `null`

#### Scenario: Entrada con un proveedor no asociado
- **WHEN** el proveedor activo P no está asociado al producto activo A, que tiene `stockQuantity` igual a 5, y un cliente con `inventory.write` registra una entrada de 3 sobre A con el `supplierId` de P
- **THEN** la respuesta es 400 con un mensaje en español que indica que el proveedor no está activo o no está asociado al producto
- **AND** el producto conserva `stockQuantity` igual a 5 y no se registra ningún movimiento

#### Scenario: Entrada con un proveedor desactivado
- **WHEN** el producto activo A está asociado al proveedor P, P está desactivado y un cliente con `inventory.write` registra una entrada sobre A con el `supplierId` de P
- **THEN** la respuesta es 400 con el mismo mensaje que para un proveedor no asociado

#### Scenario: Entrada con un proveedor inexistente
- **WHEN** un cliente con `inventory.write` registra una entrada sobre un producto activo con un `supplierId` que no corresponde a ningún proveedor
- **THEN** la respuesta es 400 con el mismo mensaje que para un proveedor no asociado

#### Scenario: supplierId que no es un GUID
- **WHEN** un cliente con `inventory.write` registra una entrada con `"supplierId": "abc"`
- **THEN** la respuesta es 400 con ProblemDetails y un `detail` en español

#### Scenario: Entrada simultánea con la desactivación del proveedor
- **WHEN** un cliente registra una entrada con el proveedor asociado P a la vez que otro cliente desactiva P
- **THEN** o la entrada responde 201 y la desactivación 204, o la desactivación responde 204 y la entrada 400
- **AND** no existe ningún movimiento con P registrado después de que P quedara desactivado

### Requirement: Una salida no lleva proveedor (BR-16)
Una salida con un `supplierId` distinto de `null` SHALL rechazarse con 400 y un mensaje en español, sin comprobar el producto ni el stock. La salida rechazada MUST NOT registrar ningún movimiento ni cambiar el stock. Una salida con `supplierId` igual a `null` MUST tratarse como una salida sin proveedor.

#### Scenario: Salida con un proveedor
- **WHEN** un cliente con `inventory.write` registra una salida válida sobre un producto activo enviando además un `supplierId`
- **THEN** la respuesta es 400 con un mensaje en español que indica que una salida no puede indicar proveedor
- **AND** el stock del producto no cambia y no se registra ningún movimiento

#### Scenario: Salida con supplierId nulo
- **WHEN** un cliente con `inventory.write` registra una salida válida con `"supplierId": null`
- **THEN** la respuesta es 201 con `supplierId` igual a `null`

#### Scenario: Salida con supplierId sobre producto borrado
- **WHEN** un cliente con `inventory.write` registra una salida con un `supplierId` sobre un producto borrado
- **THEN** la respuesta es 400

## MODIFIED Requirements

### Requirement: Registrar un movimiento
`POST /api/products/{id}/movements` SHALL exigir el scope `inventory.write` y aceptar un cuerpo JSON con `type`, `quantity`, `reason` (opcional) y `supplierId` (opcional). Si el movimiento es válido, la API MUST aplicarlo al stock del producto y responder 201 con el movimiento creado, sin cabecera `Location`.

#### Scenario: Entrada correcta
- **WHEN** el producto activo `"TOR-001"` tiene `stockQuantity` igual a 0 y un cliente con `inventory.write` envía `{ "type": "Entry", "quantity": 10, "reason": "Compra inicial" }`
- **THEN** la respuesta es 201 con `id`, `productId` igual al id del producto, `supplierId` igual a `null`, `type` igual a `"Entry"`, `quantity` igual a 10, `stockAfter` igual a 10, `reason` igual a `"Compra inicial"`, `occurredAtUtc` en UTC y `registeredBy`
- **AND** la respuesta no tiene cabecera `Location`
- **AND** el producto tiene `stockQuantity` igual a 10

#### Scenario: Salida correcta
- **WHEN** el producto activo tiene `stockQuantity` igual a 10 y un cliente con `inventory.write` envía `{ "type": "Exit", "quantity": 4 }`
- **THEN** la respuesta es 201 con `type` igual a `"Exit"`, `quantity` igual a 4, `stockAfter` igual a 6, `reason` igual a `null` y `supplierId` igual a `null`
- **AND** el producto tiene `stockQuantity` igual a 6

#### Scenario: Token sin inventory.write
- **WHEN** un cliente con un token válido que solo tiene `inventory.read` llama a `POST /api/products/{id}/movements`
- **THEN** la respuesta es 403 con el cuerpo vacío

### Requirement: Campos fuera del contrato se ignoran
Si el cuerpo del registro incluye `stockAfter`, `occurredAtUtc` u otros campos que no son parte del contrato, la API SHALL ignorarlos sin responder error. `supplierId` es parte del contrato y no se ignora.

#### Scenario: Salida con supplierId
- **WHEN** un cliente con `inventory.write` registra una salida válida enviando además un `supplierId`
- **THEN** la respuesta es 400, porque `supplierId` ya no se ignora (ver "Una salida no lleva proveedor")

#### Scenario: Entrada con stockAfter en el cuerpo
- **WHEN** un cliente con `inventory.write` registra una entrada de 2 sobre un producto con `stockQuantity` igual a 0 enviando además `"stockAfter": 99`
- **THEN** la respuesta es 201 con `stockAfter` igual a 2

#### Scenario: Entrada con occurredAtUtc en el cuerpo
- **WHEN** un cliente con `inventory.write` registra una entrada válida enviando además `"occurredAtUtc": "2000-01-01T00:00:00Z"`
- **THEN** la respuesta es 201 y `occurredAtUtc` es la fecha en que la API registró el movimiento

### Requirement: Orden de los errores del registro
La API SHALL comprobar primero el cuerpo de la petición, incluida una salida con `supplierId`, después que el producto esté activo, después el proveedor de una entrada y por último el stock. Una petición con varios problemas MUST responder con el código del primero que encuentre en ese orden.

#### Scenario: Cuerpo inválido sobre producto inexistente
- **WHEN** un cliente con `inventory.write` registra un movimiento con `quantity` igual a 0 sobre un id que no corresponde a ningún producto
- **THEN** la respuesta es 400

#### Scenario: Salida excesiva sobre producto borrado
- **WHEN** un cliente con `inventory.write` registra una salida de 100 sobre un producto borrado con `stockQuantity` igual a 5
- **THEN** la respuesta es 404

#### Scenario: Proveedor no asociado sobre producto borrado
- **WHEN** un cliente con `inventory.write` registra una entrada con un proveedor no asociado sobre un producto borrado
- **THEN** la respuesta es 404

#### Scenario: Proveedor no asociado y entrada que desborda el stock
- **WHEN** el producto activo tiene `stockQuantity` igual a 2147483000 y un cliente con `inventory.write` registra una entrada de 1000 con un proveedor no asociado
- **THEN** la respuesta es 400 con el mensaje del proveedor

### Requirement: Listar los movimientos de un producto (BR-18)
`GET /api/products/{id}/movements` SHALL exigir el scope `inventory.read` y devolver los movimientos del producto con la misma representación que el registro, incluido `supplierId`, ordenados por `occurredAtUtc` descendente y, a igualdad, por `id` descendente. La respuesta MUST tener la forma `{ items, page, pageSize, totalCount, totalPages }`, y `page` y `pageSize` siguen las mismas reglas que en categorías.

#### Scenario: Primera página por defecto
- **WHEN** un producto activo tiene 25 movimientos y un cliente con `inventory.read` llama a `GET /api/products/{id}/movements` sin parámetros
- **THEN** la respuesta es 200 con 20 elementos en `items`, `page` igual a 1, `pageSize` igual a 20, `totalCount` igual a 25 y `totalPages` igual a 2

#### Scenario: Del más reciente al más antiguo
- **WHEN** un producto activo tiene movimientos registrados a las 10:00, 10:05 y 10:02
- **THEN** `items` los devuelve en el orden 10:05, 10:02, 10:00

#### Scenario: Movimientos con la misma fecha
- **WHEN** un producto activo tiene dos movimientos con el mismo `occurredAtUtc`
- **THEN** `items` los devuelve ordenados por `id` descendente, y el orden es el mismo en todas las consultas

#### Scenario: Solo los movimientos del producto
- **WHEN** existen movimientos de dos productos activos y un cliente con `inventory.read` lista los del primero
- **THEN** `items` solo contiene movimientos con `productId` igual al primero

#### Scenario: Proveedor en el historial
- **WHEN** un producto activo tiene una entrada con el proveedor P y una salida
- **THEN** en `items` la entrada tiene `supplierId` igual al id de P y la salida `supplierId` igual a `null`

#### Scenario: Proveedor desactivado en el historial
- **WHEN** un producto activo tiene una entrada con el proveedor P y P se desactiva después
- **THEN** la entrada sigue apareciendo en `items` con `supplierId` igual al id de P

#### Scenario: Producto activo sin movimientos
- **WHEN** un cliente con `inventory.read` lista los movimientos de un producto activo que no tiene ninguno
- **THEN** la respuesta es 200 con `items` vacío y `totalCount` igual a 0

#### Scenario: Token sin inventory.read
- **WHEN** un cliente con un token válido que solo tiene `inventory.write` llama a `GET /api/products/{id}/movements`
- **THEN** la respuesta es 403 con el cuerpo vacío

#### Scenario: pageSize igual a 101
- **WHEN** un cliente con `inventory.read` pide `GET /api/products/{id}/movements?pageSize=101`
- **THEN** la respuesta es 400 con un mensaje en español sobre `pageSize`
