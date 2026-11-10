# Spec Delta

## MODIFIED Requirements

### Requirement: Todos los endpoints de /api exigen un token válido
Toda petición a una ruta bajo `/api` SHALL incluir un token bearer válido: firma verificable con las claves publicadas por el emisor, `iss` igual al emisor configurado, `aud` con la audiencia configurada y sin caducar. En caso contrario la API MUST responder 401 con el cuerpo vacío.

#### Scenario: Petición sin token
- **WHEN** un cliente llama a `GET /api/categories` sin cabecera `Authorization`
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

### Requirement: Lecturas exigen inventory.read y escrituras inventory.write
Los endpoints de lectura SHALL exigir el scope `inventory.read` y los de escritura el scope `inventory.write`. Si el token es válido pero no tiene el scope exigido, la API MUST responder 403 con el cuerpo vacío.

#### Scenario: Token de solo lectura en un endpoint de lectura
- **WHEN** un cliente con un token válido que solo tiene `inventory.read` llama a `GET /api/categories`
- **THEN** la respuesta es 200

#### Scenario: Token de solo lectura en un endpoint de escritura
- **WHEN** un cliente con un token válido que solo tiene `inventory.read` llama a `POST /api/categories`
- **THEN** la respuesta es 403 con el cuerpo vacío

#### Scenario: Token de solo escritura en un endpoint de lectura
- **WHEN** un cliente con un token válido que solo tiene `inventory.write` llama a `GET /api/categories`
- **THEN** la respuesta es 403 con el cuerpo vacío

#### Scenario: Token con ambos scopes
- **WHEN** un cliente con un token válido que tiene `inventory.read` e `inventory.write` crea una categoría válida con `POST /api/categories`
- **THEN** la respuesta es 201

## REMOVED Requirements

### Requirement: Endpoints de diagnóstico temporales
**Reason**: `check-read` y `check-write` solo servían para comprobar los scopes mientras no había endpoints de negocio. Los endpoints de categorías ya exigen `inventory.read` e `inventory.write`, así que permiten comprobar el 200, el 401 y el 403 con rutas reales.
**Migration**: Para comprobar la lectura, usar `GET /api/categories`. Para comprobar la escritura, usar `POST /api/categories`. `GET /api/auth/whoami` se mantiene para ver el cliente y los scopes del token.
