# api-auth Specification

## Purpose

Proteger todos los endpoints bajo `/api` validando tokens bearer emitidos por el servidor de identidad y exigiendo el scope adecuado para leer o escribir, sin que la API emita tokens propios.

## Requirements

### Requirement: Configuración obligatoria de autenticación
La API SHALL leer la dirección de metadata, el emisor y la audiencia válidos, y el id del cliente que Swagger UI deja rellenado, solo de las variables de entorno `Auth__MetadataAddress`, `Auth__Issuer`, `Auth__Audience` y `Auth__SwaggerClientId`. La API MUST NOT arrancar si falta alguna o si está vacía.

#### Scenario: Falta una variable de autenticación
- **WHEN** la API arranca sin `Auth__Audience`
- **THEN** el arranque falla con un error que nombra la opción ausente

#### Scenario: Configuración completa
- **WHEN** la API arranca con las cuatro variables informadas
- **THEN** la API arranca y atiende peticiones

### Requirement: Todos los endpoints de /api exigen un token válido
Toda petición a una ruta bajo `/api` SHALL incluir un token bearer válido: firma verificable con las claves publicadas por el emisor, `iss` igual al emisor configurado, `aud` con la audiencia configurada y sin caducar. En caso contrario la API MUST responder 401 con el cuerpo vacío.

#### Scenario: Petición sin token
- **WHEN** un cliente llama a `GET /api/auth/check-read` sin cabecera `Authorization`
- **THEN** la respuesta es 401 con el cuerpo vacío

#### Scenario: Token con firma inválida
- **WHEN** un cliente envía un token cuya firma no corresponde a las claves del emisor
- **THEN** la respuesta es 401 con el cuerpo vacío

#### Scenario: Token caducado
- **WHEN** un cliente envía un token cuyo `exp` ya pasó, contando la tolerancia de reloj
- **THEN** la respuesta es 401 con el cuerpo vacío

#### Scenario: Token de otro emisor
- **WHEN** un cliente envía un token cuyo `iss` es distinto del emisor configurado
- **THEN** la respuesta es 401 con el cuerpo vacío

#### Scenario: Token de otra audiencia
- **WHEN** un cliente envía un token cuyo `aud` no contiene la audiencia configurada
- **THEN** la respuesta es 401 con el cuerpo vacío

### Requirement: Comprobación de scope dentro del claim scope
La API SHALL tratar el claim `scope` como una lista de scopes separados por espacios. Un scope se considera concedido solo si aparece como elemento completo de esa lista, distinguiendo mayúsculas y minúsculas.

#### Scenario: El scope está en la lista
- **WHEN** el claim `scope` es `"profile inventory.read email"` y se comprueba `inventory.read`
- **THEN** el scope se considera concedido

#### Scenario: Coincidencia parcial
- **WHEN** el claim `scope` es `"inventory.readonly"` y se comprueba `inventory.read`
- **THEN** el scope no se considera concedido

#### Scenario: Mayúsculas distintas
- **WHEN** el claim `scope` es `"Inventory.Read"` y se comprueba `inventory.read`
- **THEN** el scope no se considera concedido

#### Scenario: Espacios repetidos o en los extremos
- **WHEN** el claim `scope` es `"  inventory.read   inventory.write "` y se comprueba `inventory.write`
- **THEN** el scope se considera concedido

#### Scenario: Claim vacío o ausente
- **WHEN** el claim `scope` está vacío o no existe y se comprueba `inventory.read`
- **THEN** el scope no se considera concedido

### Requirement: Lecturas exigen inventory.read y escrituras inventory.write
Los endpoints de lectura SHALL exigir el scope `inventory.read` y los de escritura el scope `inventory.write`. Si el token es válido pero no tiene el scope exigido, la API MUST responder 403 con el cuerpo vacío.

#### Scenario: Token de solo lectura en un endpoint de lectura
- **WHEN** un cliente con un token válido que solo tiene `inventory.read` llama a `GET /api/auth/check-read`
- **THEN** la respuesta es 200

#### Scenario: Token de solo lectura en un endpoint de escritura
- **WHEN** un cliente con un token válido que solo tiene `inventory.read` llama a `GET /api/auth/check-write`
- **THEN** la respuesta es 403 con el cuerpo vacío

#### Scenario: Token con ambos scopes
- **WHEN** un cliente con un token válido que tiene `inventory.read` e `inventory.write` llama a `GET /api/auth/check-write`
- **THEN** la respuesta es 200

### Requirement: Identidad del cliente que llama
`GET /api/auth/whoami` SHALL responder 200 a cualquier token válido, sin exigir scope, con el id del cliente tomado del claim `azp` y la lista de scopes del claim `scope`. Los nombres de los claims MUST leerse tal como vienen en el token, sin renombrarlos.

#### Scenario: Token válido con un scope
- **WHEN** `inventory-client` llama a `GET /api/auth/whoami` con un token válido que tiene `inventory.read`
- **THEN** la respuesta es 200 con `clientId` igual a `"inventory-client"` y `scopes` con `"inventory.read"`

#### Scenario: Token válido sin scopes de inventario
- **WHEN** un cliente llama a `GET /api/auth/whoami` con un token válido sin `inventory.read` ni `inventory.write`
- **THEN** la respuesta es 200 y `scopes` no incluye ningún scope de inventario

### Requirement: Endpoints de diagnóstico temporales
La API SHALL exponer `GET /api/auth/check-read`, que exige `inventory.read`, y `GET /api/auth/check-write`, que exige `inventory.write`. Ambos responden 200 con un mensaje en español cuando el scope está concedido. Son temporales y MUST eliminarse en la tarea de limpieza del entregable 3.

#### Scenario: Diagnóstico de lectura concedido
- **WHEN** un cliente con `inventory.read` llama a `GET /api/auth/check-read`
- **THEN** la respuesta es 200 con un mensaje en español que confirma el permiso de lectura

#### Scenario: Diagnóstico de escritura concedido
- **WHEN** un cliente con `inventory.write` llama a `GET /api/auth/check-write`
- **THEN** la respuesta es 200 con un mensaje en español que confirma el permiso de escritura
