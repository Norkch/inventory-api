# Reglas de negocio

Última actualización: 2026-11-06 · Estado: borrador para revisión

## Convenciones
- **ID:** `BR-NN`, estable. Una regla eliminada no reutiliza su número.
- **Trazabilidad:** cada regla se cita en el cambio de OpenSpec que la implementa y en el nombre de su prueba.
- Las fechas se guardan en UTC.

## Entidades
| Entidad | Campos |  Notas |
|---|---|---|---|
| Category | Id, Name, Description, CreatedAtUtc | Nombre único sin distinguir mayúsculas |
| Product | Id, CategoryId, Sku, Name, Description, StockQuantity, IsActive, CreatedAtUtc, UpdatedAtUtc | El stock solo cambia con movimientos |
| InventoryMovement | Id, ProductId, MovementType, Quantity, StockAfter, Reason, OccurredAtUtc, RegisteredBy |Inmutable |

## Reglas
| ID | Regla | Tipo | Respuesta ante incumplimiento |
|---|---|---|---|
| BR-01 | El SKU es obligatorio, único (sin distinguir mayúsculas), se recorta y tiene máximo 50 caracteres | 400 si falta, 409 si ya existe |
| BR-02 | El nombre del producto es obligatorio (máximo 200 caracteres) | 400 |
| BR-03 | Un producto debe pertenecer a una categoría existente |  400 |
| BR-04 | El nombre de categoría es obligatorio, único y de máximo 100 caracteres | 400 o 409 |
| BR-05 | No se puede eliminar una categoría con productos activos | 409 |
| BR-06 | Eliminar un producto es lógico (`IsActive = false`) y conserva sus movimientos | 404 si ya no está activo |
| BR-07 | Un producto nuevo empieza con stock 0; el stock solo cambia con movimientos | El campo se ignora |
| BR-08 | Un proveedor tiene nombre (máximo 200) y NIT (máximo 30) obligatorios; el NIT es único | 400 o 409 |
| BR-09 | Un proveedor se desactiva, no se elimina. Un proveedor inactivo no se puede asociar ni usar en precios o entradas | 400 |
| BR-10 | Un producto puede tener varios proveedores y un proveedor varios productos; la pareja (producto, proveedor) es única | 409 |
| BR-11 | Un movimiento es Entry o Exit, con cantidad entera mayor que 0, sobre un producto activo | 400 o 404 |
| BR-12 | Una salida no puede superar el stock disponible; el stock nunca es negativo | 409 |
| BR-13 | Los movimientos son inmutables; una corrección es un movimiento nuevo | Núcleo | El endpoint no existe |
| BR-14 | El cambio de stock y el registro del movimiento ocurren en una sola transacción | Todo o nada |
| BR-15 | Dos salidas simultáneas no pueden dejar el stock negativo | 409 para la que pierde |
| BR-16 | Una entrada puede indicar el proveedor (asociado al producto); una salida no lleva proveedor | 400 |
| BR-17 | Todas las fechas se guardan en UTC |  |
| BR-18 | Los listados se paginan: page desde 1, pageSize de 1 a 100 (por defecto 20) | 400 |
| BR-19 | Todos los endpoints requieren token. inventory.read para lecturas y inventory.write para escrituras | 401 sin token, 403 sin permiso |