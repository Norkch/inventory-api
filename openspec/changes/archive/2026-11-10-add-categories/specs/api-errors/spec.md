# Spec Delta

## Purpose

Definir un formato único y predecible para las respuestas de error de la API, con mensajes en español para el cliente y sin exponer detalles internos.

## ADDED Requirements

### Requirement: Errores de negocio con ProblemDetails
Los errores 400, 404 y 409 que produce la API SHALL responderse con `Content-Type: application/problem+json` y un cuerpo con `type`, `title`, `status` y `detail`. `status` MUST coincidir con el código HTTP, y `title` y `detail` MUST estar en español.

#### Scenario: Error de validación
- **WHEN** una petición incumple una regla de validación, por ejemplo un nombre de categoría vacío
- **THEN** la respuesta es 400 con `Content-Type: application/problem+json`, `status` igual a 400 y `detail` en español que describe la regla incumplida

#### Scenario: Recurso inexistente
- **WHEN** una petición pide un recurso que no existe
- **THEN** la respuesta es 404 con `Content-Type: application/problem+json`, `status` igual a 404 y `detail` en español

#### Scenario: Conflicto
- **WHEN** una petición entra en conflicto con el estado actual, por ejemplo un nombre de categoría repetido
- **THEN** la respuesta es 409 con `Content-Type: application/problem+json`, `status` igual a 409 y `detail` en español

### Requirement: Petición con formato inválido
Si el cuerpo JSON no se puede leer o un parámetro no tiene el tipo esperado, la API SHALL responder 400 con ProblemDetails y un `detail` en español que nombre el campo o parámetro afectado, sin mensajes del framework en inglés.

#### Scenario: JSON mal formado
- **WHEN** un cliente con `inventory.write` envía a `POST /api/categories` el cuerpo `{ "name": `
- **THEN** la respuesta es 400 con `Content-Type: application/problem+json` y `detail` en español

#### Scenario: Parámetro con tipo incorrecto
- **WHEN** un cliente con `inventory.read` pide `GET /api/categories?pageSize=diez`
- **THEN** la respuesta es 400 con `Content-Type: application/problem+json` y un `detail` en español que nombra `pageSize`

### Requirement: Error inesperado sin detalles internos
Ante un error no previsto, la API SHALL responder 500 con ProblemDetails y un `detail` genérico en español. La respuesta MUST NOT incluir el mensaje de la excepción, la traza de pila, sentencias SQL ni cadenas de conexión.

#### Scenario: Fallo de la base de datos
- **WHEN** la base de datos no responde mientras se atiende `GET /api/categories`
- **THEN** la respuesta es 500 con `Content-Type: application/problem+json`, `status` igual a 500 y un `detail` genérico en español
- **AND** el cuerpo no contiene el texto de la excepción ni la traza de pila

### Requirement: 401 y 403 sin ProblemDetails
Las respuestas 401 y 403 SHALL mantener el cuerpo vacío que define `api-auth`. El formato ProblemDetails de esta capacidad MUST NOT aplicarse a esos dos códigos.

#### Scenario: Petición sin token
- **WHEN** un cliente llama a `POST /api/categories` sin cabecera `Authorization`
- **THEN** la respuesta es 401 con el cuerpo vacío
