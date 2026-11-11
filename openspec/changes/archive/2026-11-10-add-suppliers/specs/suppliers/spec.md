# Spec Delta

## Purpose

Gestionar los proveedores del inventario: crearlos, consultarlos uno a uno o en listados paginados, editarlos y desactivarlos, respetando las reglas BR-08, BR-09 y BR-18.

## ADDED Requirements

### Requirement: Crear un proveedor
`POST /api/suppliers` SHALL exigir el scope `inventory.write` y aceptar un cuerpo JSON con `name`, `nit`, `email` (opcional) y `phone` (opcional). Si el proveedor es válido, la API MUST guardarlo como activo y responder 201 con el proveedor creado y la cabecera `Location` apuntando a `/api/suppliers/{id}`.

#### Scenario: Alta correcta
- **WHEN** un cliente con `inventory.write` envía `{ "name": "Ferretería Andina", "nit": "900123456-7", "email": "ventas@andina.com", "phone": "+57 300 123 4567" }`
- **THEN** la respuesta es 201 con `id`, `name` igual a `"Ferretería Andina"`, `nit` igual a `"900123456-7"`, `email` igual a `"ventas@andina.com"`, `phone` igual a `"+57 300 123 4567"`, `createdAtUtc` en UTC y `updatedAtUtc` igual a `null`
- **AND** la cabecera `Location` es `/api/suppliers/{id}` con el id devuelto

#### Scenario: Alta sin correo ni teléfono
- **WHEN** un cliente con `inventory.write` crea un proveedor solo con `name` y `nit` válidos
- **THEN** la respuesta es 201 con `email` igual a `null` y `phone` igual a `null`

#### Scenario: Token sin inventory.write
- **WHEN** un cliente con un token válido que solo tiene `inventory.read` llama a `POST /api/suppliers`
- **THEN** la respuesta es 403 con el cuerpo vacío

### Requirement: Nombre de proveedor obligatorio y acotado (BR-08)
La API SHALL recortar los espacios de los extremos del nombre antes de validarlo y guardarlo. Un nombre ausente, vacío o compuesto solo por espacios, o con más de 200 caracteres después de recortarlo, MUST rechazarse con 400 y un mensaje en español. El nombre no tiene que ser único.

#### Scenario: Nombre con espacios en los extremos
- **WHEN** un cliente con `inventory.write` crea un proveedor válido con `name` igual a `"  Ferretería Andina  "`
- **THEN** la respuesta es 201 y `name` es `"Ferretería Andina"`

#### Scenario: Nombre ausente
- **WHEN** un cliente con `inventory.write` crea un proveedor sin `name`
- **THEN** la respuesta es 400 con un mensaje en español que indica que el nombre es obligatorio

#### Scenario: Nombre solo con espacios
- **WHEN** un cliente con `inventory.write` crea un proveedor con `name` igual a `"   "`
- **THEN** la respuesta es 400 con un mensaje en español que indica que el nombre es obligatorio

#### Scenario: Nombre de 200 caracteres
- **WHEN** un cliente con `inventory.write` crea un proveedor válido con un `name` de 200 caracteres
- **THEN** la respuesta es 201

#### Scenario: Nombre de 201 caracteres
- **WHEN** un cliente con `inventory.write` crea un proveedor con un `name` de 201 caracteres
- **THEN** la respuesta es 400 con un mensaje en español que indica el máximo de 200 caracteres

#### Scenario: Nombre repetido
- **WHEN** existe un proveedor activo llamado `"Ferretería Andina"` y un cliente con `inventory.write` crea otro con el mismo nombre y otro NIT
- **THEN** la respuesta es 201

### Requirement: NIT obligatorio y acotado (BR-08)
La API SHALL recortar los espacios de los extremos del NIT antes de validarlo y guardarlo. Un NIT ausente, vacío o compuesto solo por espacios, o con más de 30 caracteres después de recortarlo, MUST rechazarse con 400 y un mensaje en español.

#### Scenario: NIT con espacios en los extremos
- **WHEN** un cliente con `inventory.write` crea un proveedor válido con `nit` igual a `"  900123456-7  "`
- **THEN** la respuesta es 201 y `nit` es `"900123456-7"`

#### Scenario: NIT ausente
- **WHEN** un cliente con `inventory.write` crea un proveedor sin `nit`
- **THEN** la respuesta es 400 con un mensaje en español que indica que el NIT es obligatorio

#### Scenario: NIT solo con espacios
- **WHEN** un cliente con `inventory.write` crea un proveedor con `nit` igual a `"   "`
- **THEN** la respuesta es 400 con un mensaje en español que indica que el NIT es obligatorio

#### Scenario: NIT de 30 caracteres
- **WHEN** un cliente con `inventory.write` crea un proveedor válido con un `nit` de 30 caracteres
- **THEN** la respuesta es 201

#### Scenario: NIT de 31 caracteres
- **WHEN** un cliente con `inventory.write` crea un proveedor con un `nit` de 31 caracteres
- **THEN** la respuesta es 400 con un mensaje en español que indica el máximo de 30 caracteres

### Requirement: NIT único y reservado para siempre (BR-08)
El NIT SHALL ser único entre todos los proveedores, activos o desactivados, sin distinguir mayúsculas. Crear un proveedor con el NIT de otro MUST responder 409 con un mensaje en español, también cuando el otro está desactivado y cuando dos altas con el mismo NIT llegan a la vez. El mensaje MUST NOT indicar si el otro proveedor está desactivado.

#### Scenario: NIT repetido
- **WHEN** existe un proveedor activo con `nit` igual a `"900123456-7"` y un cliente con `inventory.write` crea otro con el mismo NIT
- **THEN** la respuesta es 409 con un mensaje en español que indica que ya existe un proveedor con ese NIT

#### Scenario: NIT repetido con otras mayúsculas
- **WHEN** existe un proveedor con `nit` igual a `"ABC-123"` y un cliente con `inventory.write` crea otro con `nit` igual a `"abc-123"`
- **THEN** la respuesta es 409

#### Scenario: NIT de un proveedor desactivado
- **WHEN** existe un proveedor desactivado con `nit` igual a `"900123456-7"` y un cliente con `inventory.write` crea otro con el mismo NIT
- **THEN** la respuesta es 409 con el mismo mensaje que para un proveedor activo

#### Scenario: Altas simultáneas con el mismo NIT
- **WHEN** dos clientes con `inventory.write` crean a la vez un proveedor con `nit` igual a `"900999999-1"`
- **THEN** una petición recibe 201 y la otra 409
- **AND** solo existe un proveedor con ese NIT

### Requirement: Correo opcional con formato válido
`email` SHALL ser opcional. La API MUST recortar sus espacios de los extremos y guardar como `null` uno vacío o solo con espacios. Si no, MUST guardarlo tal cual, sin cambiar mayúsculas. Un correo de más de 200 caracteres o sin la forma `algo@dominio.ext` (sin espacios, una sola `@` y al menos un punto después de ella) MUST rechazarse con 400 y un mensaje en español.

#### Scenario: Correo con espacios en los extremos
- **WHEN** un cliente con `inventory.write` crea un proveedor válido con `email` igual a `"  Juan@Mail.com  "`
- **THEN** la respuesta es 201 y `email` es `"Juan@Mail.com"`

#### Scenario: Correo solo con espacios
- **WHEN** un cliente con `inventory.write` crea un proveedor válido con `email` igual a `"   "`
- **THEN** la respuesta es 201 y `email` es `null`

#### Scenario: Correo sin arroba
- **WHEN** un cliente con `inventory.write` crea un proveedor con `email` igual a `"juan.mail.com"`
- **THEN** la respuesta es 400 con un mensaje en español que indica que el correo no tiene un formato válido

#### Scenario: Correo sin extensión
- **WHEN** un cliente con `inventory.write` crea un proveedor con `email` igual a `"juan@mail"`
- **THEN** la respuesta es 400 con un mensaje en español que indica que el correo no tiene un formato válido

#### Scenario: Correo con espacios internos
- **WHEN** un cliente con `inventory.write` crea un proveedor con `email` igual a `"juan perez@mail.com"`
- **THEN** la respuesta es 400 con un mensaje en español que indica que el correo no tiene un formato válido

#### Scenario: Correo con dos arrobas
- **WHEN** un cliente con `inventory.write` crea un proveedor con `email` igual a `"juan@@mail.com"`
- **THEN** la respuesta es 400 con un mensaje en español que indica que el correo no tiene un formato válido

#### Scenario: Correo de 201 caracteres
- **WHEN** un cliente con `inventory.write` crea un proveedor con un `email` de formato válido y 201 caracteres
- **THEN** la respuesta es 400 con un mensaje en español que indica el máximo de 200 caracteres

### Requirement: Teléfono opcional con formato válido
`phone` SHALL ser opcional. La API MUST recortarlo y guardar como `null` uno vacío o solo con espacios; si no, MUST guardarlo tal cual. Solo puede tener un `+` inicial opcional, dígitos, espacios y guiones, y entre 7 y 15 dígitos. Si incumple el formato o tiene más de 30 caracteres, MUST rechazarse con 400 y un mensaje en español.

#### Scenario: Teléfono con prefijo, espacios y guiones
- **WHEN** un cliente con `inventory.write` crea un proveedor válido con `phone` igual a `"+57 300-123-4567"`
- **THEN** la respuesta es 201 y `phone` es `"+57 300-123-4567"`

#### Scenario: Teléfono solo con espacios
- **WHEN** un cliente con `inventory.write` crea un proveedor válido con `phone` igual a `"   "`
- **THEN** la respuesta es 201 y `phone` es `null`

#### Scenario: Teléfono con 7 dígitos
- **WHEN** un cliente con `inventory.write` crea un proveedor válido con `phone` igual a `"123-4567"`
- **THEN** la respuesta es 201

#### Scenario: Teléfono con 6 dígitos
- **WHEN** un cliente con `inventory.write` crea un proveedor con `phone` igual a `"123-456"`
- **THEN** la respuesta es 400 con un mensaje en español que indica que el teléfono no tiene un formato válido

#### Scenario: Teléfono con 16 dígitos
- **WHEN** un cliente con `inventory.write` crea un proveedor con `phone` igual a `"1234567890123456"`
- **THEN** la respuesta es 400 con un mensaje en español que indica que el teléfono no tiene un formato válido

#### Scenario: Teléfono con letras
- **WHEN** un cliente con `inventory.write` crea un proveedor con `phone` igual a `"300 ABC 4567"`
- **THEN** la respuesta es 400 con un mensaje en español que indica que el teléfono no tiene un formato válido

#### Scenario: Teléfono con el signo más en medio
- **WHEN** un cliente con `inventory.write` crea un proveedor con `phone` igual a `"300+1234567"`
- **THEN** la respuesta es 400 con un mensaje en español que indica que el teléfono no tiene un formato válido

#### Scenario: Teléfono de 31 caracteres
- **WHEN** un cliente con `inventory.write` crea un proveedor con un `phone` de 31 caracteres formado por 15 dígitos y 16 espacios o guiones
- **THEN** la respuesta es 400 con un mensaje en español que indica el máximo de 30 caracteres

### Requirement: Orden de los errores del alta de proveedor
Cuando un alta incumple varias reglas, la API SHALL responder con el primer error en este orden: el formato de `name`, `nit`, `email` y `phone` (400) y después el NIT repetido (409).

#### Scenario: Correo inválido y NIT repetido
- **WHEN** un cliente con `inventory.write` crea un proveedor con el NIT de otro proveedor y `email` igual a `"juan@mail"`
- **THEN** la respuesta es 400 con el mensaje del correo

### Requirement: Consultar un proveedor por id
`GET /api/suppliers/{id}` SHALL exigir el scope `inventory.read` y responder 200 con la representación del proveedor activo. Si no existe ningún proveedor activo con ese id, MUST responder 404 con un mensaje en español.

#### Scenario: Proveedor activo
- **WHEN** un cliente con `inventory.read` pide el id de un proveedor activo
- **THEN** la respuesta es 200 con `id`, `name`, `nit`, `email`, `phone`, `createdAtUtc` y `updatedAtUtc`

#### Scenario: Id inexistente
- **WHEN** un cliente con `inventory.read` pide un GUID que no corresponde a ningún proveedor
- **THEN** la respuesta es 404 con un mensaje en español que indica que no existe un proveedor activo con ese id

#### Scenario: Proveedor desactivado
- **WHEN** un cliente con `inventory.read` pide el id de un proveedor desactivado
- **THEN** la respuesta es 404 con el mismo mensaje que para un id inexistente

#### Scenario: Token sin inventory.read
- **WHEN** un cliente con un token válido que solo tiene `inventory.write` llama a `GET /api/suppliers/{id}`
- **THEN** la respuesta es 403 con el cuerpo vacío

### Requirement: Listar proveedores paginados (BR-18)
`GET /api/suppliers` SHALL exigir el scope `inventory.read` y devolver solo los proveedores activos, con la representación completa de cada uno, ordenados por `name` ascendente y, a igualdad de nombre, por `id`. La respuesta MUST tener la forma `{ items, page, pageSize, totalCount, totalPages }`, y `page` y `pageSize` siguen las mismas reglas que en categorías. El listado no admite búsqueda ni filtros.

#### Scenario: Primera página por defecto
- **WHEN** existen 25 proveedores activos y un cliente con `inventory.read` llama a `GET /api/suppliers` sin parámetros
- **THEN** la respuesta es 200 con 20 elementos en `items`, `page` igual a 1, `pageSize` igual a 20, `totalCount` igual a 25 y `totalPages` igual a 2

#### Scenario: Orden por nombre
- **WHEN** existen los proveedores activos `"Tornillos Sur"`, `"Aceros Norte"` y `"Maderas Centro"`
- **THEN** `items` los devuelve en el orden `"Aceros Norte"`, `"Maderas Centro"`, `"Tornillos Sur"`

#### Scenario: Proveedores con el mismo nombre
- **WHEN** existen dos proveedores activos con el mismo `name`
- **THEN** `items` los devuelve ordenados por `id`, y el orden es el mismo en todas las consultas

#### Scenario: Excluye los desactivados
- **WHEN** existen dos proveedores activos y uno desactivado
- **THEN** `items` tiene dos elementos y `totalCount` es 2

#### Scenario: pageSize igual a 101
- **WHEN** un cliente con `inventory.read` pide `GET /api/suppliers?pageSize=101`
- **THEN** la respuesta es 400 con un mensaje en español sobre `pageSize`

#### Scenario: Token sin inventory.read
- **WHEN** un cliente con un token válido que solo tiene `inventory.write` llama a `GET /api/suppliers`
- **THEN** la respuesta es 403 con el cuerpo vacío

### Requirement: Editar un proveedor
`PUT /api/suppliers/{id}` SHALL exigir el scope `inventory.write` y reemplazar `name`, `email` y `phone`, con las mismas reglas que el alta. Un `email` o `phone` ausente MUST guardarse como `null`. Si el proveedor está activo, la API MUST guardar los cambios, fijar `updatedAtUtc` y responder 200 con la representación actualizada. Si no hay ningún proveedor activo con ese id, MUST responder 404.

#### Scenario: Edición correcta
- **WHEN** un cliente con `inventory.write` envía a un proveedor activo `{ "name": "Andina S.A.S.", "email": "compras@andina.com", "phone": "6011234567" }`
- **THEN** la respuesta es 200 con esos `name`, `email` y `phone`, y `updatedAtUtc` en UTC
- **AND** `nit` y `createdAtUtc` no cambian

#### Scenario: Edición sin correo ni teléfono
- **WHEN** un proveedor activo tiene `email` y `phone` informados y un cliente con `inventory.write` lo edita enviando solo `{ "name": "Andina" }`
- **THEN** la respuesta es 200 con `email` igual a `null` y `phone` igual a `null`

#### Scenario: Edición con un nombre vacío
- **WHEN** un cliente con `inventory.write` edita un proveedor activo con `name` igual a `""`
- **THEN** la respuesta es 400 con el mensaje del nombre obligatorio y el proveedor no cambia

#### Scenario: Edición con un teléfono inválido
- **WHEN** un cliente con `inventory.write` edita un proveedor activo con `phone` igual a `"123"`
- **THEN** la respuesta es 400 con el mensaje del teléfono y el proveedor no cambia

#### Scenario: Proveedor desactivado
- **WHEN** un cliente con `inventory.write` edita un proveedor desactivado
- **THEN** la respuesta es 404 con un mensaje en español

#### Scenario: Id inexistente
- **WHEN** un cliente con `inventory.write` edita un GUID que no corresponde a ningún proveedor
- **THEN** la respuesta es 404 con un mensaje en español

#### Scenario: Token sin inventory.write
- **WHEN** un cliente con un token válido que solo tiene `inventory.read` llama a `PUT /api/suppliers/{id}`
- **THEN** la respuesta es 403 con el cuerpo vacío

### Requirement: La edición no cambia el NIT (BR-08)
La edición SHALL ignorar `nit` si viene en el cuerpo, sin responder error y sin validarlo. El NIT de un proveedor MUST NOT cambiar por un `PUT`.

#### Scenario: Edición con otro NIT
- **WHEN** un cliente con `inventory.write` edita el proveedor con `nit` igual a `"900123456-7"` enviando además `"nit": "800000000-1"`
- **THEN** la respuesta es 200 y `nit` sigue siendo `"900123456-7"`

#### Scenario: Edición con un NIT inválido
- **WHEN** un cliente con `inventory.write` edita un proveedor activo con un cuerpo válido y `"nit": ""`
- **THEN** la respuesta es 200 y `nit` no cambia

### Requirement: Desactivar un proveedor (BR-09)
`DELETE /api/suppliers/{id}` SHALL exigir el scope `inventory.write` y desactivar el proveedor sin borrar la fila, sus asociaciones ni los movimientos que lo indican. Responde 204 sin condiciones previas y MUST responder 404 con un mensaje en español si no existe o ya estaba desactivado. Un proveedor desactivado MUST NOT poder reactivarse.

#### Scenario: Proveedor activo
- **WHEN** un cliente con `inventory.write` desactiva un proveedor activo
- **THEN** la respuesta es 204 sin cuerpo
- **AND** `GET /api/suppliers/{id}` responde 404 y el proveedor no aparece en `GET /api/suppliers`

#### Scenario: Proveedor con asociaciones y entradas
- **WHEN** un cliente con `inventory.write` desactiva un proveedor asociado a productos y usado en entradas
- **THEN** la respuesta es 204
- **AND** las filas de sus asociaciones y los movimientos que lo indican se conservan sin cambios

#### Scenario: Proveedor ya desactivado
- **WHEN** un cliente con `inventory.write` desactiva un proveedor que ya estaba desactivado
- **THEN** la respuesta es 404 con un mensaje en español

#### Scenario: Id inexistente
- **WHEN** un cliente con `inventory.write` desactiva un GUID que no corresponde a ningún proveedor
- **THEN** la respuesta es 404 con un mensaje en español

#### Scenario: Token sin inventory.write
- **WHEN** un cliente con un token válido que solo tiene `inventory.read` llama a `DELETE /api/suppliers/{id}`
- **THEN** la respuesta es 403 con el cuerpo vacío
