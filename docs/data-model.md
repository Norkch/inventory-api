# Modelo de datos

Última actualización: 2026-11-10 · Motor: SQL Server 2022 (Docker) · Base: `InventoryDb`

## Principios
- **Sin llaves foráneas** La integridad referencial se garantiza en la aplicación, y cada comprobación tiene su prueba.
- Los identificadores son `UNIQUEIDENTIFIER` generados por la aplicación.
- Todas las fechas son `DATETIME2` en UTC.
- Se conservan las restricciones `PRIMARY KEY`, `UNIQUE`, `NOT NULL`, `DEFAULT` y `CHECK`, como última barrera de integridad.
- Los índices únicos de texto aprovechan la intercalación por defecto de SQL Server, que no distingue mayúsculas. `Categories.Name`, `Products.Sku` y `Suppliers.Nit` la declaran de forma explícita (`SQL_Latin1_General_CP1_CI_AS`) para no depender de la configuración de la instancia.
- Los índices filtrados (`WHERE ...`) exigen `QUOTED_IDENTIFIER ON` en las sesiones que modifican la tabla. `db/init.sql` y `Microsoft.Data.SqlClient` lo activan; quien escriba SQL a mano contra esas tablas también debe activarlo.
- El esquema lo crea `db/init.sql`, idempotente, desde el contenedor `dbinit`. EF Core solo lee.

## Tablas

### Categories
| Columna | Tipo | Null | Restricción |
|---|---|---|---|
| Id | UNIQUEIDENTIFIER | No | PK |
| Name | NVARCHAR(100) `COLLATE SQL_Latin1_General_CP1_CI_AS` | No | Único entre las categorías activas, sin distinguir mayúsculas |
| Description | NVARCHAR(500) | Sí | |
| IsActive | BIT | No | Por defecto 1 |
| CreatedAtUtc | DATETIME2 | No | Por defecto: fecha actual UTC |

Eliminar una categoría es lógico (`IsActive = 0`, BR-05). El nombre de una categoría desactivada se puede reutilizar.

### Products
| Columna | Tipo | Null | Restricción |
|---|---|---|---|
| Id | UNIQUEIDENTIFIER | No | PK |
| CategoryId | UNIQUEIDENTIFIER | No | Referencia lógica a Categories |
| Sku | NVARCHAR(50) `COLLATE SQL_Latin1_General_CP1_CI_AS` | No | Único entre todos los productos, también los borrados, sin distinguir mayúsculas |
| Name | NVARCHAR(200) | No | |
| Description | NVARCHAR(1000) | Sí | |
| StockQuantity | INT | No | Por defecto 0; `CHECK (StockQuantity >= 0)` |
| IsActive | BIT | No | Por defecto 1  |
| CreatedAtUtc | DATETIME2 | No | Por defecto: fecha actual UTC |
| UpdatedAtUtc | DATETIME2 | Sí | Lo fija la aplicación al editar y al borrar |

`StockQuantity` es un saldo derivado: solo cambia con movimientos. Cada movimiento lo cambia en una sola transacción con dos sentencias (ver D3 del diseño del cambio `add-inventory-movements`):

1. Un `UPDATE` condicional atómico: `SET StockQuantity = StockQuantity + @Delta OUTPUT INSERTED.StockQuantity WHERE Id = @ProductId AND IsActive = 1 AND StockQuantity >= -@Delta`. `@Delta` es `+quantity` en una entrada y `-quantity` en una salida. Si no afecta a ninguna fila, el producto no está activo (404) o el stock no alcanza (409). El bloqueo de la fila serializa las salidas simultáneas (BR-15), y `CK_Products_StockQuantity` queda como última barrera.
2. El `INSERT` del movimiento en `InventoryMovements`, con `StockAfter` igual al valor que devolvió el `OUTPUT`.

Si cualquiera de las dos falla, la transacción se deshace (BR-14). Una entrada que superaría `2147483647` provoca el error 8115 de SQL Server, y la API responde 409. Registrar un movimiento no cambia `UpdatedAtUtc`.

Eliminar un producto es lógico (`IsActive = 0`, BR-06) y conserva su stock y sus movimientos. A diferencia del nombre de una categoría, el SKU de un producto borrado **no** se puede reutilizar: `UX_Products_Sku` no tiene filtro.

### InventoryMovements
| Columna | Tipo | Null | Restricción |
|---|---|---|---|
| Id | UNIQUEIDENTIFIER | No | PK |
| ProductId | UNIQUEIDENTIFIER | No | Referencia lógica a Products |
| SupplierId | UNIQUEIDENTIFIER | Sí | Extensión. `CHECK (MovementType = 1 OR SupplierId IS NULL)` |
| MovementType | TINYINT | No | `CHECK IN (1, 2)`: 1 = Entry, 2 = Exit |
| Quantity | INT | No | `CHECK (Quantity > 0)` |
| StockAfter | INT | No | Saldo tras el movimiento |
| Reason | NVARCHAR(250) | Sí | |
| OccurredAtUtc | DATETIME2 | No | |
| RegisteredBy | NVARCHAR(100) | No | Cliente OAuth2 (claim `sub`) |

Tabla inmutable: no hay actualización ni eliminación. Cada fila se inserta en la misma transacción que el `UPDATE` de `Products.StockQuantity` (ver la sección de `Products`), así que `StockAfter` coincide siempre con el stock del producto justo después de aplicar ese movimiento.

`SupplierId` es el proveedor de una entrada (BR-16), o `NULL`. Una salida lo tiene siempre `NULL`, y `CK_InventoryMovements_Supplier` queda como última barrera.

Cuando una entrada indica proveedor, la misma transacción del movimiento, **antes** del `UPDATE` de stock, comprueba que el proveedor está activo y asociado al producto, y bloquea su fila con `UPDLOCK, HOLDLOCK` (D5 del diseño de `add-suppliers`). Así una desactivación simultánea del proveedor espera a que termine la entrada. Desactivar un proveedor después no cambia los movimientos que lo indican.

### Suppliers
| Columna | Tipo | Null | Restricción |
|---|---|---|---|
| Id | UNIQUEIDENTIFIER | No | PK |
| Name | NVARCHAR(200) | No | |
| Nit | NVARCHAR(30) `COLLATE SQL_Latin1_General_CP1_CI_AS` | No | Único entre todos los proveedores, también los desactivados, sin distinguir mayúsculas |
| Email | NVARCHAR(200) | Sí | |
| Phone | NVARCHAR(30) | Sí | |
| IsActive | BIT | No | Por defecto 1 |
| CreatedAtUtc | DATETIME2 | No | Por defecto: fecha actual UTC |
| UpdatedAtUtc | DATETIME2 | Sí | |

### ProductSuppliers
| Columna | Tipo | Null | Restricción |
|---|---|---|---|
| Id | UNIQUEIDENTIFIER | No | PK |
| ProductId | UNIQUEIDENTIFIER | No | Referencia lógica a Products |
| SupplierId | UNIQUEIDENTIFIER | No | Referencia lógica a Suppliers |
| SupplierSku | NVARCHAR(50) | Sí | |
| CreatedAtUtc | DATETIME2 | No | Por defecto: fecha actual UTC |

Relación muchos a muchos. Único: `(ProductId, SupplierId)`.

## Índices
| Índice | Tabla | Columnas | Para qué |
|---|---|---|---|
| UX_Categories_Name | Categories | Name, filtrado `WHERE IsActive = 1` | Nombre único entre las categorías activas |
| UX_Products_Sku | Products | Sku, sin filtro | SKU único entre todos los productos, también los borrados |
| IX_Products_CategoryId | Products | CategoryId (incluye IsActive) | Buscar productos por categoría y comprobar si una categoría tiene productos activos |
| IX_InventoryMovements_ProductId_OccurredAtUtc | InventoryMovements | ProductId, OccurredAtUtc DESC | Historial por producto |
| UX_Suppliers_Nit | Suppliers | Nit, sin filtro | NIT único entre todos los proveedores, también los desactivados |
| UX_ProductSuppliers_Link | ProductSuppliers | ProductId, SupplierId | Sin asociaciones duplicadas |
| IX_ProductSuppliers_SupplierId | ProductSuppliers | SupplierId | Productos de un proveedor |