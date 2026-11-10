# Spec Delta

## MODIFIED Requirements

### Requirement: Autorización desde Swagger UI
Swagger UI SHALL ofrecer el botón Authorize con dos opciones: el flujo OAuth2 `client_credentials` contra el endpoint de token del realm `inventory`, con los scopes `inventory.read` e `inventory.write`, y pegar un token bearer.

#### Scenario: Autorizar con client credentials
- **WHEN** en Swagger UI se autoriza con `inventory-client`, su secreto y solo el scope `inventory.read`, y luego se llama a `GET /api/categories` y a `POST /api/categories` con una categoría válida
- **THEN** `GET /api/categories` responde 200 y `POST /api/categories` responde 403

#### Scenario: Sin autorizar
- **WHEN** en Swagger UI se llama a `GET /api/categories` y a `POST /api/categories` sin autorizar
- **THEN** ambos responden 401

#### Scenario: Pegar un token bearer
- **WHEN** en Swagger UI se pega en la opción Bearer un token obtenido con `curl` que tiene `inventory.write`, y se llama a `POST /api/categories` con una categoría válida
- **THEN** la respuesta es 201
