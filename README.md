# Inventory API

API REST de inventario para gestionar categorías, productos, proveedores, la asociación entre productos y proveedores, y los movimientos de stock (entradas y salidas).

## Stack
.NET 6 · ASP.NET Core · SQL Server (Docker) · Dapper (escrituras) · EF Core (lecturas) · OAuth2 client_credentials con Keycloak · xUnit

## Qué ofrece la API
Todos los endpoints exigen un token bearer válido (401 sin él). Las lecturas piden el scope `inventory.read` y las escrituras `inventory.write` (403 si falta). Los listados se paginan con `page` (desde 1) y `pageSize` (de 1 a 100, por defecto 20).

| Recurso | Endpoints | Notas |
|---|---|---|
| Identidad | `GET /api/auth/whoami` | Cliente y scopes del token. No pide scope |
| Categorías | `POST`, `GET`, `GET {id}`, `DELETE {id}` en `/api/categories` | Borrado lógico; no se desactiva una categoría con productos activos |
| Productos | `POST`, `GET`, `GET {id}`, `PUT {id}`, `DELETE {id}` en `/api/products` | SKU único para siempre. El listado filtra por `categoryId` y busca por nombre o SKU (`search`). El stock solo cambia con movimientos |
| Proveedores | `POST`, `GET`, `GET {id}`, `PUT {id}`, `DELETE {id}` en `/api/suppliers` | NIT único para siempre, sin distinguir mayúsculas; el `PUT` no lo cambia. `DELETE` desactiva y no se reactiva |
| Proveedores de un producto | `POST` y `GET` en `/api/products/{productId}/suppliers`, `GET /api/products/{productId}/suppliers/{supplierId}` y `GET /api/suppliers/{supplierId}/products` | Pareja única, sin edición ni borrado. Solo se ven las asociaciones con producto y proveedor activos |
| Movimientos | `POST` y `GET` en `/api/products/{productId}/movements` | Libro inmutable. Stock y movimiento en una sola transacción, sin stock negativo ni con salidas simultáneas. Una entrada puede indicar un proveedor asociado al producto; una salida no |

Los errores se devuelven como ProblemDetails, con el mensaje en español. El contrato completo, con cada regla, mensaje y orden de errores, está en [`docs/api-contract.md`](docs/api-contract.md).

## Cómo ejecutarlo

Requisitos: Docker Desktop (o Docker Engine con Compose v2) y los puertos `8080`, `8081` y `1433` libres.

1. **Crear `.env`** a partir del ejemplo y cambiar los valores `change-me-...`:
   ```bash
   cp .env.example .env
   ```
   | Variable | Para qué sirve |
   |---|---|
   | `SA_PASSWORD` | Contraseña de `sa` en SQL Server (debe cumplir la política de complejidad) |
   | `KC_BOOTSTRAP_ADMIN_USERNAME` / `KC_BOOTSTRAP_ADMIN_PASSWORD` | Usuario administrador de la consola de Keycloak (`http://localhost:8081`) |
   | `INVENTORY_CLIENT_ID` | Id del cliente de servicio en Keycloak y el que Swagger deja rellenado. Opcional: por defecto `inventory-client` |
   | `INVENTORY_API_AUDIENCE` | Audiencia que Keycloak pone en los tokens y que la API exige. Opcional: por defecto `inventory-api` |
   | `INVENTORY_CLIENT_SECRET` | Secreto del cliente `inventory-client`; se usa para pedir tokens |

   Si falta alguna variable de Keycloak (salvo `INVENTORY_CLIENT_ID` e `INVENTORY_API_AUDIENCE`, que tienen valor por defecto), `docker compose` se niega a arrancar y nombra la variable. Si cambias `INVENTORY_CLIENT_ID` o `INVENTORY_API_AUDIENCE`, recrea Keycloak (`docker compose up -d --force-recreate keycloak api`) para que importe el realm con el nuevo id, y usa ese id en lugar de `inventory-client` en los ejemplos. `.env` no se sube al repositorio.

2. **Levantar todo:**
   ```bash
   docker compose up -d --build
   ```
   Arranca `sqlserver`, `dbinit` (crea la base con `db/init.sql`), `keycloak` y `api`. En equipos lentos `dbinit` puede fallar por tiempo de espera de SQL Server; en ese caso basta con repetir `docker compose up -d`.

3. **Esperar a Keycloak** (tarda entre 20 y 40 s en importar el realm). Está listo cuando esta URL devuelve un JSON:
   ```bash
   curl -s http://localhost:8081/realms/inventory/.well-known/openid-configuration
   ```
   En Windows PowerShell usa `curl.exe` en lugar de `curl`. Mientras no responda, las peticiones con token pueden dar 401.

4. **Autorizar en Swagger:** abrir `http://localhost:8080/swagger` y pulsar **Authorize**.
   - **oauth2 (client credentials):** `client_id` ya viene con el valor de `INVENTORY_CLIENT_ID` (`inventory-client` por defecto). Escribe en `client_secret` el valor de `INVENTORY_CLIENT_SECRET` de tu `.env`, marca los scopes que quieras (`inventory.read`, `inventory.write`) y pulsa **Authorize**.
   - **Bearer (alternativa):** pide un token con `curl` y pega solo el `access_token` (sin el prefijo `Bearer`):
     ```bash
     curl -s -X POST http://localhost:8081/realms/inventory/protocol/openid-connect/token \
       -d grant_type=client_credentials -d client_id=inventory-client \
       -d client_secret=<INVENTORY_CLIENT_SECRET> --data-urlencode "scope=inventory.read inventory.write"
     ```

5. **Probar:** `GET /api/auth/whoami` muestra el cliente y los scopes del token. Con solo `inventory.read`, `GET /api/categories` da 200 y `POST /api/categories` da 403; con `inventory.write`, `POST /api/categories` con `{ "name": "Tornillería" }` da 201; sin autorizar, ambos dan 401. El contrato completo está en [`docs/api-contract.md`](docs/api-contract.md).

6. **Parar:** `docker compose down`. Los datos de SQL Server se conservan en el volumen `sqldata`. Para empezar con una base vacía, usa `docker compose down -v`. Keycloak no guarda datos: al volver a levantarlo reimporta `keycloak/inventory-realm.json`.

## Pruebas
Requisito: .NET SDK 6.

```bash
dotnet build
dotnet test
```

Las pruebas son unitarias, con xUnit y fakes escritos a mano. No necesitan Docker. Para medir la cobertura con Coverlet:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Los repositorios de SQL Server (Dapper y EF Core) y la concurrencia no tienen prueba automática. Se verifican de extremo a extremo con Docker, y cada recorrido queda anotado en [`docs/bitacora.md`](docs/bitacora.md).

## Documentación
| Documento | Contenido |
|---|---|
| [`docs/api-contract.md`](docs/api-contract.md) | Endpoints, cuerpos, respuestas y mensajes de error |
| [`docs/business-rules.md`](docs/business-rules.md) | Entidades y reglas de negocio (BR-01 a BR-19) |
| [`docs/architecture.md`](docs/architecture.md) | Capas, CQRS y casos de uso |
| [`docs/data-model.md`](docs/data-model.md) | Tablas, índices y cómo se cambia el stock |
| [`docs/decisions.md`](docs/decisions.md) y [`docs/assumptions.md`](docs/assumptions.md) | Decisiones y supuestos |
| [`docs/bitacora.md`](docs/bitacora.md) | Verificaciones de extremo a extremo, revisiones y cobertura por entregable |
| [`docs/ai-usage.md`](docs/ai-usage.md) | Uso de IA por entregable |
| `openspec/specs/` | Especificaciones vigentes de cada capacidad |

## Método de trabajo
Desarrollo guiado por especificaciones con OpenSpec: cada cambio tiene propuesta, especificación, diseño y tareas en `openspec/changes/`. Cada entregable usa un issue, una rama, un pull request y un tag.

## Declaración de uso de IA
Usé Claude Code como asistente de desarrollo. Las especificaciones se redactaron en español con OpenSpec y las revisé antes de implementar. Cada tarea se hizo con TDD y se verificó con compilación, pruebas y una auditoría de Clean Code. La revisión de calidad la hizo una sesión o subagente distinto del que escribió el código. El detalle por entregable está en [`docs/ai-usage.md`](docs/ai-usage.md). Soy responsable del código entregado.

## Decisiones y supuestos
Ver `docs/decisions.md` y `docs/assumptions.md`. 
