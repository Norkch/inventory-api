# Spec Delta

## MODIFIED Requirements

### Requirement: Todos los endpoints de /api exigen un token válido
Toda petición a una ruta bajo `/api` SHALL incluir un token bearer válido: firma verificable con las claves publicadas por el emisor, `iss` igual al emisor configurado, `aud` con la audiencia configurada, sin caducar y con un claim `sub` no vacío. En caso contrario la API MUST responder 401 con el cuerpo vacío.

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

#### Scenario: Token sin claim sub
- **WHEN** un cliente envía un token con firma, emisor, audiencia y caducidad válidos pero sin claim `sub` a `GET /api/categories`
- **THEN** la respuesta es 401 con el cuerpo vacío

#### Scenario: Token con sub vacío
- **WHEN** un cliente envía un token por lo demás válido cuyo claim `sub` es una cadena vacía o solo espacios
- **THEN** la respuesta es 401 con el cuerpo vacío
