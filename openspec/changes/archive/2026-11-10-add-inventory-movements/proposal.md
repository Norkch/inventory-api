# Proposal

## Why

Los productos ya existen, pero su stock está fijo en 0: BR-07 dice que solo cambia con movimientos, y todavía no hay forma de registrarlos. Este cambio añade el libro inmutable de movimientos de inventario (BR-11 a BR-15), que es lo que da sentido a `stockQuantity`, y garantiza que el stock nunca quede negativo aunque lleguen salidas simultáneas.

## What Changes

- Nuevos endpoints de movimientos, anidados bajo el producto:
  - `POST /api/products/{id}/movements` (`inventory.write`): registra una entrada o una salida y responde 201 con el movimiento creado, incluido `stockAfter`. **Sin cabecera `Location`**, porque no existe un recurso individual de movimiento.
  - `GET /api/products/{id}/movements?page&pageSize` (`inventory.read`): lista los movimientos del producto, paginados (BR-18), del más reciente al más antiguo (`occurredAtUtc` descendente y, a igualdad, `id` descendente).
- BR-11: `type` es `Entry` o `Exit`; se recorta y no distingue mayúsculas, y la respuesta devuelve siempre la forma canónica. `quantity` es un entero mayor que 0. El producto debe existir y estar activo (404).
- BR-12: una salida que supera el stock disponible responde 409 y no registra nada. El stock nunca es negativo.
- BR-13: los movimientos son inmutables. No existen `PUT` ni `DELETE`; una corrección es un movimiento nuevo.
- BR-14: el cambio de stock y el registro del movimiento ocurren en una sola transacción: o se hacen los dos o ninguno.
- BR-15: dos salidas simultáneas no pueden dejar el stock negativo. El stock se cambia con un `UPDATE` condicional atómico; la que pierde responde 409.
- Una entrada que llevaría el stock por encima del máximo de un entero responde 409, en lugar de un error 500.
- `reason` es opcional, se recorta, se guarda `null` si solo tiene espacios y tiene como máximo 250 caracteres.
- `RegisteredBy` se toma del claim `sub` del token. **La API rechaza con 401 cualquier token válido que no traiga `sub`**, en todos los endpoints bajo `/api`, para que el libro nunca registre un autor vacío o inventado.
- Registrar un movimiento no modifica `updatedAtUtc` del producto, que sigue significando la última edición del catálogo.
- `GET` sobre los movimientos de un producto inexistente o borrado responde 404, coherente con DEC-004; un producto activo sin movimientos responde 200 con la lista vacía.
- Documentación: `docs/api-contract.md`, `docs/data-model.md` (cómo cambia `StockQuantity`), `docs/architecture.md` y `docs/assumptions.md`.

## Capabilities

### New Capabilities
- `inventory-movements`: registro de entradas y salidas de stock y consulta paginada del historial de un producto, con las reglas BR-11 a BR-15 y BR-18.

### Modified Capabilities
- `api-auth`: el requisito de token válido pasa a exigir también el claim `sub`; un token sin `sub` responde 401 con el cuerpo vacío.

## Impact

- **Código:**
  - `Inventory.Domain`: entidad `InventoryMovement`, enum `MovementType`, reglas de validación de movimientos y `InsufficientStockException`.
  - `Inventory.Application`: comando y handler de registro, consulta y handler del listado, puertos `IMovementWriteRepository` (con resultado tipado) e `IMovementReadRepository`, DTO y mensajes.
  - `Inventory.Infrastructure`: `MovementWriteRepository` (Dapper, transacción con `UPDATE` condicional e `INSERT`), registro y repositorio de lectura (EF Core) y `InventoryMovements` en `InventoryReadDbContext`.
  - `Inventory.Api`: `MovementsController`, sus contratos, el registro de dependencias y el rechazo de tokens sin `sub` en `ConfigureJwtBearerOptions`.
  - `tests/Inventory.UnitTests`.
- **Base de datos:** ninguna modificación. `InventoryMovements` y su índice ya existen en `db/init.sql`.
- **Paquetes:** ninguno nuevo.
- **Configuración:** ninguna variable nueva.
- **Fuera de alcance:** proveedor en los movimientos (BR-16; `SupplierId` queda siempre `NULL` y un `supplierId` en el cuerpo se ignora), consulta de un movimiento suelto, movimientos de productos borrados, informes de stock y filtros del historial por tipo o fecha.
