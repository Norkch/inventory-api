# Proposal

## Why

La API ya valida tokens y scopes, pero todavía no expone ningún endpoint de negocio: `check-read` y `check-write` solo existen para comprobar los permisos. Las categorías son la primera entidad que se puede gestionar, porque no dependen de ninguna otra, y los productos (BR-03) las necesitan. Implementarlas obliga además a montar la base de persistencia (Dapper para escribir, EF Core para leer) y el formato de errores que reutilizarán los cambios siguientes.

## What Changes

- Nuevos endpoints de categorías:
  - `POST /api/categories` (`inventory.write`): crea una categoría y responde 201.
  - `GET /api/categories?page&pageSize` (`inventory.read`): lista las categorías activas, paginadas (BR-18) y ordenadas por nombre.
  - `GET /api/categories/{id}` (`inventory.read`): devuelve una categoría activa o 404.
  - `DELETE /api/categories/{id}` (`inventory.write`): **borrado lógico**. Responde 204, 404 si no existe o ya está inactiva, y 409 si tiene productos activos (BR-05).
- No hay endpoint para editar categorías (sin PUT).
- BR-04: el nombre es obligatorio, se recorta, tiene máximo 100 caracteres y es único entre las categorías activas, sin distinguir mayúsculas. El nombre de una categoría desactivada se puede reutilizar. `Description` es opcional, con máximo 500 caracteres.
- Respuesta paginada común: `{ items, page, pageSize, totalCount, totalPages }`. Una página fuera de rango responde 200 con `items` vacío.
- CQRS sin mezclar: las escrituras (alta y borrado lógico) van con Dapper detrás de `ICategoryWriteRepository`, y las lecturas (por id y listado) van con EF Core `AsNoTracking` detrás de `ICategoryReadRepository`. Un handler de comando nunca usa el puerto de lectura, ni uno de consulta el de escritura.
- Errores de negocio y de formato con ProblemDetails (`application/problem+json`) y mensajes en español: 400, 404 y 409. Un error inesperado responde 500 sin detalles internos. 401 y 403 siguen con el cuerpo vacío.
- Modelo de datos: `Categories` gana `IsActive` y el índice único `UX_Categories_Name` pasa a ser filtrado (`WHERE IsActive = 1`). `db/init.sql` actualiza también las bases ya creadas.
- La API no arranca sin la cadena de conexión `ConnectionStrings__Inventory`.
- **BREAKING:** se eliminan `GET /api/auth/check-read` y `GET /api/auth/check-write`. Los scopes se comprueban ahora con los endpoints de categorías. `whoami` se mantiene.
- Documentación: `docs/business-rules.md` (BR-03, BR-05 y la entidad Category), `docs/data-model.md`, `docs/architecture.md`, `docs/api-contract.md`, `docs/decisions.md` (borrado lógico de categorías), `docs/assumptions.md` y `README.md`.

## Capabilities

### New Capabilities
- `categories`: alta, consulta paginada, consulta por id y borrado lógico de categorías, con las reglas BR-04, BR-05 y BR-18.
- `api-errors`: formato de las respuestas de error de la API (ProblemDetails en español para 400, 404, 409 y 500).

### Modified Capabilities
- `api-auth`: se elimina el requisito de los endpoints de diagnóstico temporales, y los escenarios de token y de scopes pasan a usar los endpoints de categorías.
- `dev-identity-provider`: los escenarios de autorización desde Swagger UI usan los endpoints de categorías en lugar de `check-read` y `check-write`.

## Impact

- **Código:**
  - `Inventory.Domain`: entidad `Category`, `Guard` y excepciones de dominio.
  - `Inventory.Application`: comandos, consultas, handlers, puertos, DTOs y paginación.
  - `Inventory.Infrastructure`: conexión de Dapper, `DbContext` de lectura y repositorios.
  - `Inventory.Api`: `CategoriesController`, middleware de errores, respuesta de modelo inválido en español y `Program.cs`. Se eliminan `check-read`, `check-write` y `AccessCheckResponse` de `AuthController`.
  - `tests/Inventory.UnitTests`.
- **Base de datos:** `db/init.sql` (columna `IsActive`, collation explícita en `Name`, índice filtrado).
- **Paquetes:** ninguno nuevo. Se usan los que ya referencia Infrastructure: Dapper 2.1.89 y `Microsoft.EntityFrameworkCore.SqlServer` 6.0.36.
- **Configuración:** `ConnectionStrings__Inventory` pasa a ser obligatoria (ya está en `docker-compose.yml`).
- **Fuera de alcance:** editar categorías, reactivar categorías desactivadas, búsqueda o filtros en el listado, y los endpoints de productos. BR-03 se documenta como "categoría activa", pero se implementa en el cambio de productos.
