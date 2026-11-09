## Purpose

Ofrecer un servidor de identidad local, reproducible con Docker, que emita tokens OAuth2 `client_credentials` para la API de inventario y permita probar la autenticación desde Swagger UI.

## ADDED Requirements

### Requirement: Realm importado automáticamente
Al levantar el entorno con `docker compose up`, el servidor de identidad SHALL crear el realm `inventory` a partir de `keycloak/inventory-realm.json`, sin pasos manuales.

#### Scenario: Primer arranque
- **WHEN** se ejecuta `docker compose up -d --build` en un clon limpio con `.env` creado desde `.env.example`
- **THEN** `http://localhost:8081/realms/inventory/.well-known/openid-configuration` responde 200

### Requirement: El secreto del cliente no está en el repositorio
El secreto de `inventory-client` SHALL llegar solo por una variable de entorno. Ni `keycloak/inventory-realm.json` ni `docker-compose.yml` MUST contener su valor ni un valor por defecto. El entorno MUST NOT levantarse si la variable falta.

#### Scenario: Variable del secreto ausente
- **WHEN** se ejecuta `docker compose up` sin la variable del secreto definida
- **THEN** compose falla con un error que nombra la variable

#### Scenario: Revisión del repositorio
- **WHEN** se busca el valor del secreto de `.env` en los archivos versionados
- **THEN** no aparece en ninguno

### Requirement: Cliente de servicio con scopes opcionales
El realm SHALL tener un cliente confidencial `inventory-client` habilitado solo para el flujo `client_credentials`, con `inventory.read` e `inventory.write` como scopes opcionales. Un token incluye en el claim `scope` solo los scopes que se pidieron. El id del cliente SHALL leerse de la variable de entorno `INVENTORY_CLIENT_ID`, con `inventory-client` como valor por defecto, y la misma variable MUST alimentar el `client_id` que Swagger UI deja rellenado (`Auth__SwaggerClientId`).

#### Scenario: Id de cliente configurado
- **WHEN** se recrea Keycloak con `INVENTORY_CLIENT_ID=other-client` y ese cliente pide un token con su secreto
- **THEN** Keycloak emite el token con `azp` igual a `other-client`, `inventory-client` ya no existe en el realm y Swagger UI deja rellenado `other-client`

#### Scenario: Pedir solo lectura
- **WHEN** `inventory-client` pide un token con `scope=inventory.read`
- **THEN** el claim `scope` contiene `inventory.read` y no contiene `inventory.write`

#### Scenario: Pedir ambos scopes
- **WHEN** `inventory-client` pide un token con `scope=inventory.read inventory.write`
- **THEN** el claim `scope` contiene ambos

#### Scenario: Secreto incorrecto
- **WHEN** se pide un token para `inventory-client` con un secreto incorrecto
- **THEN** el servidor de identidad responde 401 con `error` igual a `unauthorized_client` y no emite ningún token

### Requirement: Contenido del token
Todo token emitido a `inventory-client` SHALL llevar `aud` con `inventory-api` (aunque no se pida ningún scope de inventario), `azp` igual a `inventory-client` y `scope` como cadena separada por espacios. La audiencia SHALL leerse de la variable de entorno `INVENTORY_API_AUDIENCE`, con `inventory-api` como valor por defecto, y la misma variable MUST alimentar la audiencia que valida la API (`Auth__Audience`).

#### Scenario: Audiencia configurada
- **WHEN** se recrean Keycloak y la API con `INVENTORY_API_AUDIENCE=other-api` y `inventory-client` llama a `GET /api/auth/whoami` con un token recién emitido
- **THEN** el token tiene `aud` con `other-api` y la respuesta es 200

#### Scenario: Token sin scopes de inventario
- **WHEN** `inventory-client` pide un token sin indicar scopes
- **THEN** el token tiene `aud` con `inventory-api` y `azp` igual a `inventory-client`

### Requirement: Emisor estable
El claim `iss` SHALL ser siempre `http://localhost:8081/realms/inventory`, tanto si el token se pide desde el host como desde la red interna de Docker. La metadata que obtiene la API desde la red interna MUST apuntar a direcciones accesibles dentro de esa red.

#### Scenario: Token pedido desde el host
- **WHEN** se pide un token a `http://localhost:8081/realms/inventory/protocol/openid-connect/token`
- **THEN** `iss` es `http://localhost:8081/realms/inventory`

#### Scenario: Metadata consultada desde la red interna
- **WHEN** la API consulta `http://keycloak:8080/realms/inventory/.well-known/openid-configuration`
- **THEN** `issuer` es `http://localhost:8081/realms/inventory` y `jwks_uri` apunta a `http://keycloak:8080`

### Requirement: Autorización desde Swagger UI
Swagger UI SHALL ofrecer el botón Authorize con dos opciones: el flujo OAuth2 `client_credentials` contra el endpoint de token del realm `inventory`, con los scopes `inventory.read` e `inventory.write`, y pegar un token bearer.

#### Scenario: Autorizar con client credentials
- **WHEN** en Swagger UI se autoriza con `inventory-client`, su secreto y solo el scope `inventory.read`, y luego se llama a `check-read` y a `check-write`
- **THEN** `check-read` responde 200 y `check-write` responde 403

#### Scenario: Sin autorizar
- **WHEN** en Swagger UI se llama a `check-read` y a `check-write` sin autorizar
- **THEN** ambos responden 401

#### Scenario: Pegar un token bearer
- **WHEN** en Swagger UI se pega en la opción Bearer un token obtenido con `curl` que tiene `inventory.write`
- **THEN** `check-write` responde 200
