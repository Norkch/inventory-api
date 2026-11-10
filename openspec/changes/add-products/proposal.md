# Proposal

## Why

Las categorías ya se pueden gestionar, pero el inventario todavía no tiene productos, que son el centro del dominio: los movimientos de stock y los proveedores dependen de ellos. Este cambio añade el CRUD de productos con las reglas BR-01, BR-02, BR-03, BR-06 y BR-07, y hace efectiva la regla BR-05 de categorías, que hasta ahora solo se podía probar insertando productos a mano.

## What Changes

- Nuevos endpoints de productos:
  - `POST /api/products` (`inventory.write`): crea un producto activo con stock 0 y responde 201 con `Location`.
  - `GET /api/products?page&pageSize&categoryId&search` (`inventory.read`): lista los productos activos, paginados (BR-18) y ordenados por nombre. `categoryId` filtra por categoría y `search` busca un fragmento en el nombre o en el SKU, sin distinguir mayúsculas.
  - `GET /api/products/{id}` (`inventory.read`): devuelve un producto activo o 404.
  - `PUT /api/products/{id}` (`inventory.write`): edita `name`, `description` y `categoryId`, y responde 200 con el producto actualizado.
  - `DELETE /api/products/{id}` (`inventory.write`): **borrado lógico** (`IsActive = false`). Responde 204, o 404 si no existe o ya está inactivo. Se permite aunque el producto tenga stock; ese stock queda congelado y los movimientos se conservan (BR-06).
- BR-01: el SKU es obligatorio, se recorta y tiene como máximo 50 caracteres. Es único sin distinguir mayúsculas **entre todos los productos, también los borrados**: el SKU de un producto borrado no se puede reutilizar (409). El SKU no se puede modificar.
- BR-02: el nombre es obligatorio, se recorta y tiene como máximo 200 caracteres. No tiene que ser único. `description` es opcional, se recorta, se guarda `null` si solo tiene espacios y tiene como máximo 1000 caracteres.
- BR-03: la categoría debe existir y estar activa (400). Se comprueba en la misma sentencia que escribe el producto, para que un producto activo no pueda quedar en una categoría que se desactiva a la vez.
- BR-07: un producto nuevo empieza con stock 0. El alta y la edición ignoran `stockQuantity` (y la edición, también `sku`) si vienen en el cuerpo.
- Cada producto se representa con `id`, `sku`, `name`, `description`, `categoryId`, `categoryName`, `stockQuantity`, `createdAtUtc` y `updatedAtUtc`, igual en todas las respuestas.
- Un parámetro de consulta con un tipo incorrecto (por ejemplo, un `categoryId` que no es un GUID) responde 400 con un mensaje en español que describe el tipo esperado. Hasta ahora el mensaje siempre decía "número entero".
- Modelo de datos: `Products.Sku` declara la collation `SQL_Latin1_General_CP1_CI_AS`, como `Categories.Name`. `UX_Products_Sku` se mantiene **sin filtro**. `db/init.sql` actualiza también las bases ya creadas.
- Documentación: `docs/api-contract.md`, `docs/data-model.md`, `docs/business-rules.md` (BR-01, BR-06 y BR-07), `docs/architecture.md`, `docs/decisions.md` (SKU reservado y borrado con stock) y `docs/assumptions.md`.

## Capabilities

### New Capabilities
- `products`: alta, consulta paginada con filtros, consulta por id, edición y borrado lógico de productos, con las reglas BR-01, BR-02, BR-03, BR-06, BR-07 y BR-18.

### Modified Capabilities
<!-- Ninguna. BR-05 en `categories` ya está especificada; este cambio solo hace que sus escenarios se puedan recorrer por la API. El mensaje de tipo incorrecto ya lo cubre el requisito "Petición con formato inválido" de `api-errors`, que no fija el texto. -->

## Impact

- **Código:**
  - `Inventory.Domain`: entidad `Product`.
  - `Inventory.Application`: comandos, consultas, handlers, puertos `IProductWriteRepository` e `IProductReadRepository`, DTO y mensajes de productos.
  - `Inventory.Infrastructure`: `ProductWriteRepository` (Dapper), `ProductRecord` y `ProductReadRepository` (EF Core), y `Products` en `InventoryReadDbContext`.
  - `Inventory.Api`: `ProductsController`, sus contratos, el registro de dependencias (las piezas compartidas de persistencia salen de `AddCategories`) y el mensaje por tipo en `InvalidModelStateResponseFactory`.
  - `tests/Inventory.UnitTests`.
- **Base de datos:** `db/init.sql` (collation explícita de `Products.Sku` con migración idempotente del índice).
- **Paquetes:** ninguno nuevo.
- **Configuración:** ninguna variable nueva.
- **Fuera de alcance:** movimientos de inventario (el stock solo se lee), proveedores, reactivar productos borrados, cambiar el SKU, y buscar sin distinguir acentos.
