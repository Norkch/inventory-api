# Contrato de la API

Contrato HTTP de la API de inventario. Cada sección indica el cambio de OpenSpec que la define.

## Autenticación (`add-keycloak-oauth`)

### Cómo se obtiene un token

La API no emite tokens: solo los valida. Los tokens los emite Keycloak con el flujo OAuth2 `client_credentials`.

```bash
curl -s -X POST http://localhost:8081/realms/inventory/protocol/openid-connect/token \
  -d grant_type=client_credentials \
  -d client_id=inventory-client \
  -d client_secret=<INVENTORY_CLIENT_SECRET> \
  --data-urlencode "scope=inventory.read inventory.write"
```

El token se envía en cada petición con la cabecera `Authorization: Bearer <token>`.

| Scope | Concede |
|---|---|
| `inventory.read` | Endpoints de lectura |
| `inventory.write` | Endpoints de escritura |

Los dos scopes son opcionales: el token solo lleva los que se pidieron en `scope`.

### Configuración obligatoria

La API lee la configuración de autenticación solo de estas variables de entorno:

| Variable | Valor en `docker-compose.yml` |
|---|---|
| `Auth__MetadataAddress` | `http://keycloak:8080/realms/inventory/.well-known/openid-configuration` |
| `Auth__Issuer` | `http://localhost:8081/realms/inventory` |
| `Auth__Audience` | `${INVENTORY_API_AUDIENCE:-inventory-api}` |
| `Auth__SwaggerClientId` | `${INVENTORY_CLIENT_ID:-inventory-client}` (id que Swagger UI deja rellenado; no es un secreto) |

Si falta alguna variable o está vacía, la API no arranca. El error nombra la opción que falta, por ejemplo: `DataAnnotation validation failed for 'AuthOptions' members: 'Audience' with the error: 'The Audience field is required.'`. Con las cuatro variables informadas, la API arranca y atiende peticiones.

### Validación del token

Toda ruta bajo `/api` exige un token bearer válido. Un token es válido cuando cumple todo esto:

- La firma se verifica con las claves que publica el emisor (`jwks_uri` de la metadata).
- `iss` es igual a `Auth__Issuer`.
- `aud` contiene `Auth__Audience`.
- No ha caducado. Se tolera un desfase de reloj de 30 s después de `exp`.

Los claims se leen con el nombre que traen en el token, sin renombrarlos: `azp` es el id del cliente y `scope` es la lista de scopes.

### Comprobación de scopes

El claim `scope` es una cadena con los scopes separados por espacios. Un scope está concedido solo si aparece como elemento completo de esa lista. La comparación distingue mayúsculas y minúsculas.

| Claim `scope` | Scope comprobado | ¿Concedido? |
|---|---|---|
| `"profile inventory.read email"` | `inventory.read` | Sí |
| `"inventory.readonly"` | `inventory.read` | No (coincidencia parcial) |
| `"Inventory.Read"` | `inventory.read` | No (mayúsculas distintas) |
| `"  inventory.read   inventory.write "` | `inventory.write` | Sí (se ignoran los espacios repetidos y los de los extremos) |
| vacío o ausente | `inventory.read` | No |

### Respuestas 401 y 403

La API devuelve 401 y 403 **con el cuerpo vacío**. No usa ProblemDetails para estos códigos.

| Código | Cuándo | Cuerpo |
|---|---|---|
| 401 Unauthorized | Falta la cabecera `Authorization`, la firma no es válida, el token caducó, `iss` es de otro emisor o `aud` no contiene la audiencia configurada | Vacío. La cabecera `WWW-Authenticate: Bearer ...` indica el motivo |
| 403 Forbidden | El token es válido, pero no tiene el scope que exige el endpoint | Vacío |

Una regla rápida para diferenciarlos: 401 significa que la API no acepta el token, y 403 que lo acepta pero el token no tiene el permiso.

### Endpoints

| Método y ruta | Scope exigido | Éxito | Sin token o token inválido | Token sin el scope |
|---|---|---|---|---|
| `GET /api/auth/whoami` | Ninguno (basta un token válido) | 200 | 401 | No aplica |
| `GET /api/auth/check-read` | `inventory.read` | 200 | 401 | 403 |
| `GET /api/auth/check-write` | `inventory.write` | 200 | 401 | 403 |

#### `GET /api/auth/whoami`

Devuelve la identidad del cliente que llama. `clientId` viene del claim `azp` y `scopes`, del claim `scope`, ya separado en una lista y sin elementos vacíos.

Respuesta 200 con un token pedido con `scope=inventory.read`:

```json
{
  "clientId": "inventory-client",
  "scopes": ["inventory.read"]
}
```

Con un token sin scopes de inventario, la respuesta también es 200, y `scopes` no incluye ni `inventory.read` ni `inventory.write`:

```json
{
  "clientId": "inventory-client",
  "scopes": []
}
```

#### `GET /api/auth/check-read`

Diagnóstico del permiso de lectura. Respuesta 200:

```json
{ "message": "Permiso de lectura concedido: el token tiene el scope inventory.read." }
```

Un token con solo `inventory.read` recibe 200 aquí y 403 en `check-write`.

#### `GET /api/auth/check-write`

Diagnóstico del permiso de escritura. Respuesta 200:

```json
{ "message": "Permiso de escritura concedido: el token tiene el scope inventory.write." }
```

Un token con `inventory.read` e `inventory.write` recibe 200.

> **Temporales:** `check-read` y `check-write` existen solo para verificar los scopes mientras no haya endpoints de negocio. Se eliminan en la tarea de limpieza del entregable 3, cuando `POST /api/categories` permita comprobar el 403 con un endpoint real. `whoami` se mantiene.
