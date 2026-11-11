# Reglas de negocio

Última actualización: 2026-11-09 · Estado: borrador para revisión

## Convenciones
- **ID:** `BR-NN`, estable. Una regla eliminada no reutiliza su número.
- **Trazabilidad:** cada regla se cita en el cambio de OpenSpec que la implementa y en el nombre de su prueba.
- Las fechas se guardan en UTC.

## Entidades
| Entidad | Campos |  Notas |
|---|---|---|---|
| Category | Id, Name, Description, IsActive, CreatedAtUtc | Nombre único entre las activas, sin distinguir mayúsculas. Se desactiva, no se borra (BR-05) |
| Product | Id, CategoryId, Sku, Name, Description, StockQuantity, IsActive, CreatedAtUtc, UpdatedAtUtc | El stock solo cambia con movimientos |
| InventoryMovement | Id, ProductId, MovementType, Quantity, StockAfter, Reason, OccurredAtUtc, RegisteredBy |Inmutable |
| ProductSupplier | Id, ProductId, SupplierId, SupplierSku, CreatedAtUtc |Relación muchos a muchos entre producto y proveedor |

## Reglas
| ID | Regla | Tipo | Respuesta ante incumplimiento |
|---|---|---|---|
| BR-01 | El SKU es obligatorio, se recorta y tiene máximo 50 caracteres. Es único entre todos los productos, también los borrados, sin distinguir mayúsculas: el SKU de un producto borrado no se reutiliza. No se puede modificar | 400 si falta, 409 si ya existe |
| BR-02 | El nombre del producto se recorta, es obligatorio y tiene máximo 200 caracteres. No tiene que ser único | 400 |
| BR-03 | Un producto debe pertenecer a una categoría que exista y esté activa |  400 |
| BR-04 | El nombre de categoría se recorta, es obligatorio, tiene máximo 100 caracteres y es único entre las categorías activas, sin distinguir mayúsculas. El nombre de una categoría desactivada se puede reutilizar | 400 o 409 |
| BR-05 | Eliminar una categoría es lógico (`IsActive = false`) y solo se permite si no tiene productos activos; los productos inactivos no lo impiden. Su nombre queda libre para otra categoría | 409 si tiene productos activos, 404 si no existe o ya está desactivada |
| BR-06 | Eliminar un producto es lógico (`IsActive = false`) y conserva sus movimientos. Se permite aunque tenga stock: ese stock queda congelado | 404 si ya no está activo |
| BR-07 | Un producto nuevo empieza con stock 0; el stock solo cambia con movimientos. El alta y la edición ignoran `stockQuantity` | El campo se ignora |
| BR-08 | Un proveedor tiene nombre (máximo 200) y NIT (máximo 30) obligatorios; el NIT es único | 400 o 409 |
| BR-09 | Un proveedor se desactiva, no se elimina. Un proveedor inactivo no se puede asociar ni usar en entradas | 400 |
| BR-10 | Un producto puede tener varios proveedores y un proveedor varios productos; la pareja (producto, proveedor) es única | 409 |
| BR-11 | Un movimiento es Entry o Exit, con cantidad entera mayor que 0, sobre un producto activo. El tipo se recorta y no distingue mayúsculas | 400 o 404 |
| BR-12 | Una salida no puede superar el stock disponible; el stock nunca es negativo. Una entrada tampoco puede llevar el stock por encima de 2147483647 | 409 |
| BR-13 | Los movimientos son inmutables; una corrección es un movimiento nuevo | Núcleo | El endpoint no existe |
| BR-14 | El cambio de stock y el registro del movimiento ocurren en una sola transacción | Todo o nada |
| BR-15 | Dos salidas simultáneas no pueden dejar el stock negativo | 409 para la que pierde |
| BR-16 | Una entrada puede indicar el proveedor, que debe estar activo y asociado al producto; una salida no lleva proveedor. Implementada en `add-suppliers` | 400 |
| BR-17 | Todas las fechas se guardan en UTC |  |
| BR-18 | Los listados se paginan: page desde 1, pageSize de 1 a 100 (por defecto 20) | 400 |
| BR-19 | Todos los endpoints requieren token. inventory.read para lecturas y inventory.write para escrituras | 401 sin token, 403 sin permiso |