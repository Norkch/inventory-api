# Spec Delta

## Purpose

Llevar el libro inmutable de movimientos de inventario: registrar entradas y salidas que cambian el stock de un producto activo y consultar su historial paginado, respetando las reglas BR-11 a BR-15 y BR-18.

## ADDED Requirements

### Requirement: Registrar un movimiento
`POST /api/products/{id}/movements` SHALL exigir el scope `inventory.write` y aceptar un cuerpo JSON con `type`, `quantity` y `reason` (opcional). Si el movimiento es válido, la API MUST aplicarlo al stock del producto y responder 201 con el movimiento creado, sin cabecera `Location`.

#### Scenario: Entrada correcta
- **WHEN** el producto activo `"TOR-001"` tiene `stockQuantity` igual a 0 y un cliente con `inventory.write` envía `{ "type": "Entry", "quantity": 10, "reason": "Compra inicial" }`
- **THEN** la respuesta es 201 con `id`, `productId` igual al id del producto, `type` igual a `"Entry"`, `quantity` igual a 10, `stockAfter` igual a 10, `reason` igual a `"Compra inicial"`, `occurredAtUtc` en UTC y `registeredBy`
- **AND** la respuesta no tiene cabecera `Location`
- **AND** el producto tiene `stockQuantity` igual a 10

#### Scenario: Salida correcta
- **WHEN** el producto activo tiene `stockQuantity` igual a 10 y un cliente con `inventory.write` envía `{ "type": "Exit", "quantity": 4 }`
- **THEN** la respuesta es 201 con `type` igual a `"Exit"`, `quantity` igual a 4, `stockAfter` igual a 6 y `reason` igual a `null`
- **AND** el producto tiene `stockQuantity` igual a 6

#### Scenario: Token sin inventory.write
- **WHEN** un cliente con un token válido que solo tiene `inventory.read` llama a `POST /api/products/{id}/movements`
- **THEN** la respuesta es 403 con el cuerpo vacío

### Requirement: Tipo de movimiento Entry o Exit (BR-11)
`type` SHALL ser `Entry` o `Exit`. La API MUST recortar los espacios de los extremos y aceptar el valor sin distinguir mayúsculas, y la respuesta MUST devolver siempre la forma canónica `"Entry"` o `"Exit"`. Un tipo ausente, vacío o con otro valor, incluido un número escrito como texto, MUST rechazarse con 400 y un mensaje en español.

#### Scenario: Tipo en minúsculas y con espacios
- **WHEN** un cliente con `inventory.write` registra un movimiento con `type` igual a `"  entry  "` sobre un producto activo
- **THEN** la respuesta es 201 y `type` es `"Entry"`

#### Scenario: Tipo ausente
- **WHEN** un cliente con `inventory.write` registra un movimiento sin `type`
- **THEN** la respuesta es 400 con un mensaje en español que indica que el tipo de movimiento es obligatorio

#### Scenario: Tipo desconocido
- **WHEN** un cliente con `inventory.write` registra un movimiento con `type` igual a `"Salida"`
- **THEN** la respuesta es 400 con un mensaje en español que indica que el tipo debe ser `Entry` o `Exit`

#### Scenario: Tipo numérico escrito como texto
- **WHEN** un cliente con `inventory.write` registra un movimiento con `type` igual a `"1"`
- **THEN** la respuesta es 400 con un mensaje en español que indica que el tipo debe ser `Entry` o `Exit`

#### Scenario: Tipo como número JSON
- **WHEN** un cliente con `inventory.write` registra un movimiento con `type` igual al número `1`
- **THEN** la respuesta es 400 con ProblemDetails y un `detail` en español

### Requirement: Cantidad entera mayor que cero (BR-11)
`quantity` SHALL ser un entero mayor que 0. Una cantidad ausente, igual a 0 o negativa MUST rechazarse con 400 y un mensaje en español. Un valor que no es un entero JSON MUST responder 400 con ProblemDetails.

#### Scenario: Cantidad ausente
- **WHEN** un cliente con `inventory.write` registra un movimiento sin `quantity`
- **THEN** la respuesta es 400 con un mensaje en español que indica que la cantidad es obligatoria

#### Scenario: Cantidad cero
- **WHEN** un cliente con `inventory.write` registra un movimiento con `quantity` igual a 0
- **THEN** la respuesta es 400 con un mensaje en español que indica que la cantidad debe ser un entero mayor que 0

#### Scenario: Cantidad negativa
- **WHEN** un cliente con `inventory.write` registra un movimiento con `quantity` igual a -3
- **THEN** la respuesta es 400 con un mensaje en español que indica que la cantidad debe ser un entero mayor que 0

#### Scenario: Cantidad decimal
- **WHEN** un cliente con `inventory.write` registra un movimiento con `quantity` igual a 1.5
- **THEN** la respuesta es 400 con ProblemDetails y un `detail` en español

### Requirement: Motivo opcional y acotado
La API SHALL recortar los espacios de los extremos de `reason` antes de validarlo y guardarlo. Un motivo ausente, vacío o compuesto solo por espacios MUST guardarse y devolverse como `null`. Un motivo de más de 250 caracteres después de recortarlo MUST rechazarse con 400 y un mensaje en español.

#### Scenario: Motivo con espacios en los extremos
- **WHEN** un cliente con `inventory.write` registra un movimiento válido con `reason` igual a `"  Compra mensual  "`
- **THEN** la respuesta es 201 y `reason` es `"Compra mensual"`

#### Scenario: Motivo solo con espacios
- **WHEN** un cliente con `inventory.write` registra un movimiento válido con `reason` igual a `"   "`
- **THEN** la respuesta es 201 y `reason` es `null`

#### Scenario: Motivo de 250 caracteres
- **WHEN** un cliente con `inventory.write` registra un movimiento válido con un `reason` de 250 caracteres
- **THEN** la respuesta es 201

#### Scenario: Motivo de 251 caracteres
- **WHEN** un cliente con `inventory.write` registra un movimiento con un `reason` de 251 caracteres
- **THEN** la respuesta es 400 con un mensaje en español que indica el máximo de 250 caracteres

### Requirement: Producto existente y activo (BR-11)
El movimiento SHALL registrarse solo sobre un producto activo. Si el producto no existe o está borrado, la API MUST responder 404 con un mensaje en español y MUST NOT registrar el movimiento ni cambiar ningún stock.

#### Scenario: Producto inexistente
- **WHEN** un cliente con `inventory.write` registra un movimiento válido sobre un id que no corresponde a ningún producto
- **THEN** la respuesta es 404 con un mensaje en español que indica que no existe un producto activo con ese id

#### Scenario: Producto borrado
- **WHEN** el producto está borrado con `stockQuantity` igual a 15 y un cliente con `inventory.write` registra sobre él una entrada válida
- **THEN** la respuesta es 404
- **AND** el producto conserva `stockQuantity` igual a 15 y no se registra ningún movimiento

### Requirement: Una salida no puede superar el stock (BR-12)
Una salida cuya cantidad supera el stock disponible SHALL rechazarse con 409 y un mensaje en español. El stock de un producto MUST NOT quedar negativo, y una salida rechazada MUST NOT registrar ningún movimiento.

#### Scenario: Salida mayor que el stock
- **WHEN** el producto activo tiene `stockQuantity` igual a 5 y un cliente con `inventory.write` registra una salida de 6
- **THEN** la respuesta es 409 con un mensaje en español que indica que el stock es insuficiente
- **AND** el producto conserva `stockQuantity` igual a 5 y no se registra ningún movimiento

#### Scenario: Salida igual al stock
- **WHEN** el producto activo tiene `stockQuantity` igual a 5 y un cliente con `inventory.write` registra una salida de 5
- **THEN** la respuesta es 201 con `stockAfter` igual a 0

#### Scenario: Salida sobre stock cero
- **WHEN** el producto activo tiene `stockQuantity` igual a 0 y un cliente con `inventory.write` registra una salida de 1
- **THEN** la respuesta es 409

### Requirement: Una entrada no puede desbordar el stock
Una entrada que llevaría el stock por encima del máximo de un entero de 32 bits (2147483647) SHALL rechazarse con 409 y un mensaje en español, en lugar de un error 500. La entrada rechazada MUST NOT registrar ningún movimiento ni cambiar el stock.

#### Scenario: Entrada que desborda el stock
- **WHEN** el producto activo tiene `stockQuantity` igual a 2147483000 y un cliente con `inventory.write` registra una entrada de 1000
- **THEN** la respuesta es 409 con un mensaje en español que indica que la entrada supera el stock máximo permitido
- **AND** el producto conserva `stockQuantity` igual a 2147483000 y no se registra ningún movimiento

#### Scenario: Entrada hasta el máximo
- **WHEN** el producto activo tiene `stockQuantity` igual a 2147483000 y un cliente con `inventory.write` registra una entrada de 647
- **THEN** la respuesta es 201 con `stockAfter` igual a 2147483647

### Requirement: Stock y movimiento en una sola transacción (BR-14)
El cambio de stock del producto y el registro del movimiento SHALL ocurrir de forma atómica. Si cualquiera de los dos falla, la API MUST NOT dejar aplicado el otro, y `stockAfter` del movimiento MUST coincidir con el `stockQuantity` del producto justo después de aplicarlo.

#### Scenario: Falla el registro del movimiento
- **WHEN** el stock del producto se actualiza pero el registro del movimiento falla
- **THEN** el producto conserva el stock que tenía antes de la petición
- **AND** la respuesta es 500 sin detalles internos

#### Scenario: stockAfter coincide con el stock del producto
- **WHEN** un cliente con `inventory.write` registra una entrada de 3 sobre un producto activo con `stockQuantity` igual a 7
- **THEN** el movimiento tiene `stockAfter` igual a 10 y el producto tiene `stockQuantity` igual a 10

### Requirement: Salidas simultáneas sin stock negativo (BR-15)
Cuando varias salidas sobre el mismo producto llegan a la vez y no hay stock para todas, la API SHALL aceptar solo las que caben en el stock disponible y responder 409 a las demás. El stock MUST NOT quedar negativo en ningún momento.

#### Scenario: Dos salidas simultáneas que no caben juntas
- **WHEN** el producto activo tiene `stockQuantity` igual a 5 y dos clientes con `inventory.write` registran a la vez una salida de 4 cada uno
- **THEN** una petición recibe 201 con `stockAfter` igual a 1 y la otra recibe 409
- **AND** el producto termina con `stockQuantity` igual a 1 y existe un solo movimiento nuevo

### Requirement: Autor del movimiento
`registeredBy` SHALL ser el valor del claim `sub` del token con el que se registró el movimiento. La API MUST NOT aceptar el autor desde el cuerpo de la petición.

#### Scenario: Autor tomado del token
- **WHEN** un cliente cuyo token tiene `sub` igual a `"3f2a9c1e-0000-4000-8000-000000000001"` registra un movimiento válido
- **THEN** la respuesta es 201 con `registeredBy` igual a `"3f2a9c1e-0000-4000-8000-000000000001"`

#### Scenario: Autor en el cuerpo
- **WHEN** un cliente registra un movimiento válido enviando además `"registeredBy": "otro"`
- **THEN** la respuesta es 201 y `registeredBy` es el `sub` del token

### Requirement: Campos fuera del contrato se ignoran
Si el cuerpo del registro incluye `supplierId`, `stockAfter`, `occurredAtUtc` u otros campos que no son parte del contrato, la API SHALL ignorarlos sin responder error.

#### Scenario: Salida con supplierId
- **WHEN** un cliente con `inventory.write` registra una salida válida enviando además un `supplierId`
- **THEN** la respuesta es 201 y el movimiento no tiene proveedor

#### Scenario: Entrada con stockAfter en el cuerpo
- **WHEN** un cliente con `inventory.write` registra una entrada de 2 sobre un producto con `stockQuantity` igual a 0 enviando además `"stockAfter": 99`
- **THEN** la respuesta es 201 con `stockAfter` igual a 2

### Requirement: El movimiento no cambia la fecha de edición del producto
Registrar un movimiento SHALL cambiar solo el `stockQuantity` del producto. `updatedAtUtc` y los demás datos del producto MUST NOT cambiar.

#### Scenario: Producto nunca editado
- **WHEN** un producto activo con `updatedAtUtc` igual a `null` recibe una entrada válida
- **THEN** `GET /api/products/{id}` devuelve el nuevo `stockQuantity` y `updatedAtUtc` sigue siendo `null`

### Requirement: Orden de los errores del registro
La API SHALL comprobar primero el cuerpo de la petición, después que el producto esté activo y por último el stock. Una petición con varios problemas MUST responder con el código del primero que encuentre en ese orden.

#### Scenario: Cuerpo inválido sobre producto inexistente
- **WHEN** un cliente con `inventory.write` registra un movimiento con `quantity` igual a 0 sobre un id que no corresponde a ningún producto
- **THEN** la respuesta es 400

#### Scenario: Salida excesiva sobre producto borrado
- **WHEN** un cliente con `inventory.write` registra una salida de 100 sobre un producto borrado con `stockQuantity` igual a 5
- **THEN** la respuesta es 404

### Requirement: Movimientos inmutables (BR-13)
La API SHALL NOT ofrecer ninguna forma de modificar ni borrar un movimiento. Las peticiones `PUT`, `PATCH` y `DELETE` sobre las rutas de movimientos MUST NOT cambiar ningún movimiento ni ningún stock.

#### Scenario: DELETE sobre la colección de movimientos
- **WHEN** un cliente con `inventory.write` envía `DELETE /api/products/{id}/movements`
- **THEN** la respuesta es 405 y los movimientos y el stock del producto no cambian

#### Scenario: PUT sobre un movimiento
- **WHEN** un cliente con `inventory.write` envía `PUT /api/products/{id}/movements/{movementId}`
- **THEN** la respuesta es 404 y el movimiento no cambia

### Requirement: Listar los movimientos de un producto (BR-18)
`GET /api/products/{id}/movements` SHALL exigir el scope `inventory.read` y devolver los movimientos del producto con la misma representación que el registro, ordenados por `occurredAtUtc` descendente y, a igualdad, por `id` descendente. La respuesta MUST tener la forma `{ items, page, pageSize, totalCount, totalPages }`, y `page` y `pageSize` siguen las mismas reglas que en categorías.

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

#### Scenario: Producto activo sin movimientos
- **WHEN** un cliente con `inventory.read` lista los movimientos de un producto activo que no tiene ninguno
- **THEN** la respuesta es 200 con `items` vacío y `totalCount` igual a 0

#### Scenario: Token sin inventory.read
- **WHEN** un cliente con un token válido que solo tiene `inventory.write` llama a `GET /api/products/{id}/movements`
- **THEN** la respuesta es 403 con el cuerpo vacío

#### Scenario: pageSize igual a 101
- **WHEN** un cliente con `inventory.read` pide `GET /api/products/{id}/movements?pageSize=101`
- **THEN** la respuesta es 400 con un mensaje en español sobre `pageSize`

### Requirement: Historial de un producto inexistente o borrado
Si el producto no existe o está borrado, `GET /api/products/{id}/movements` SHALL responder 404 con un mensaje en español, aunque el producto borrado conserve movimientos. Los errores de paginación MUST comprobarse antes que el producto.

#### Scenario: Producto borrado con movimientos
- **WHEN** un producto borrado tiene movimientos y un cliente con `inventory.read` los lista
- **THEN** la respuesta es 404 con un mensaje en español que indica que no existe un producto activo con ese id

#### Scenario: Producto inexistente
- **WHEN** un cliente con `inventory.read` lista los movimientos de un id que no corresponde a ningún producto
- **THEN** la respuesta es 404

#### Scenario: Paginación inválida sobre producto borrado
- **WHEN** un cliente con `inventory.read` pide `GET /api/products/{id}/movements?page=0` sobre un producto borrado
- **THEN** la respuesta es 400
