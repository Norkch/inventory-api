# Proposal

## Why

El esquema ya tiene `Suppliers`, `ProductSuppliers` y la columna `InventoryMovements.SupplierId`, pero la API no los expone: no se pueden registrar proveedores, ni saber quién suministra cada producto, ni indicar de dónde viene una entrada de stock. Este cambio implementa BR-08, BR-09 y BR-10 y cierra BR-16, que `add-inventory-movements` dejó aplazada.

## What Changes

- Nuevos endpoints de proveedores (BR-08, BR-09, BR-18):
  - `POST /api/suppliers` (`inventory.write`): crea un proveedor activo y responde 201 con `Location: /api/suppliers/{id}`.
  - `GET /api/suppliers/{id}` (`inventory.read`): devuelve un proveedor activo; uno desactivado responde 404.
  - `GET /api/suppliers?page&pageSize` (`inventory.read`): lista los activos por `name` y luego `id`, sin búsqueda.
  - `PUT /api/suppliers/{id}` (`inventory.write`): reemplaza `name`, `email` y `phone`, fija `updatedAtUtc` e ignora `nit`.
  - `DELETE /api/suppliers/{id}` (`inventory.write`): desactiva el proveedor, responde 204 y conserva sus asociaciones. No hay reactivación.
- Reglas del proveedor:
  - `name` obligatorio, recortado, máximo 200, no único.
  - `nit` obligatorio, recortado, máximo 30, único entre **todos** los proveedores (también los desactivados) sin distinguir mayúsculas; 409 si se repite, también en altas simultáneas.
  - `email` opcional, máximo 200, con formato `algo@dominio.ext`.
  - `phone` opcional, máximo 30: `+` inicial opcional, dígitos, espacios y guiones, con 7 a 15 dígitos.
  - `email` y `phone` se recortan, una cadena vacía se guarda como `null` y, si no, se guardan tal cual.
- Nuevos endpoints de la asociación producto-proveedor (BR-09, BR-10), que no se edita ni se borra:
  - `POST /api/products/{productId}/suppliers` (`inventory.write`) con `supplierId` y `supplierSku` opcional (máximo 50). Orden de errores: formato 400, producto 404, proveedor 400, pareja repetida 409. Responde 201 con `Location: /api/products/{productId}/suppliers/{supplierId}`.
  - `GET /api/products/{productId}/suppliers/{supplierId}` (`inventory.read`).
  - `GET /api/products/{productId}/suppliers` (`inventory.read`), paginado y ordenado por nombre del proveedor.
  - `GET /api/suppliers/{supplierId}/products` (`inventory.read`), paginado y ordenado por nombre del producto.
  - Solo se ven las asociaciones cuyo producto y proveedor están activos. Un producto o proveedor no activo en la ruta responde 404.
- **BREAKING** en `POST /api/products/{id}/movements` (BR-16):
  - una entrada puede indicar `supplierId`, que debe ser un proveedor activo asociado al producto (400);
  - una salida con `supplierId` responde 400; antes se ignoraba;
  - orden de errores: cuerpo 400, producto 404, proveedor 400, stock 409;
  - el movimiento, en el registro y en el listado, devuelve `supplierId` (o `null`).
- Un proveedor que se desactiva a la vez que alguien lo asocia o lo usa en una entrada nunca queda asociado ni usado después de desactivarse: la fila del proveedor se bloquea durante la operación.
- Documentación: `docs/api-contract.md`, `docs/data-model.md`, `docs/business-rules.md` (BR-16 deja de estar aplazada), `docs/architecture.md` (se quita `UnlinkProductSupplier`) y `docs/assumptions.md`.

## Capabilities

### New Capabilities
- `suppliers`: alta, consulta, listado paginado, edición y desactivación de proveedores, con las reglas BR-08, BR-09 y BR-18.
- `product-suppliers`: asociación muchos a muchos entre productos y proveedores, su alta y sus consultas desde el producto y desde el proveedor, con BR-09 y BR-10.

### Modified Capabilities
- `inventory-movements`: el registro acepta `supplierId` en las entradas y lo rechaza en las salidas (BR-16); cambian el orden de los errores del registro y la representación del movimiento, y `supplierId` deja de ser un campo ignorado.

## Impact

- **Código:**
  - `Inventory.Domain`: entidades `Supplier` y `ProductSupplier`, reglas de validación de proveedor y de asociación.
  - `Inventory.Application`: comandos, consultas y handlers de proveedores y asociaciones, puertos de escritura y lectura, DTOs y mensajes; cambios en `RegisterMovementHandler`, `MovementDto` y el resultado del registro.
  - `Inventory.Infrastructure`: repositorios Dapper y EF Core de proveedores y asociaciones; `MovementWriteRepository` comprueba y bloquea el proveedor dentro de la transacción e inserta `SupplierId`.
  - `Inventory.Api`: `SuppliersController`, `ProductSuppliersController`, sus contratos y su registro; `RegisterMovementRequest` añade `SupplierId`.
  - `tests/Inventory.UnitTests`.
- **Base de datos:** `Suppliers.Nit` pasa a declarar `COLLATE SQL_Latin1_General_CP1_CI_AS`, con migración idempotente en `db/init.sql` (recrear `UX_Suppliers_Nit`), como `Products.Sku`. Las tablas y demás índices ya existen.
- **Paquetes:** ninguno nuevo.
- **Configuración:** ninguna variable nueva.
- **Fuera de alcance:** búsqueda en el listado de proveedores, reactivación de proveedores, edición o borrado de asociaciones, asociar proveedores en el alta del producto, filtrar movimientos por proveedor y precios de compra.
