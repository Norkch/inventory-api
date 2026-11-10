# Modelo de datos

Última actualización: 2026-11-09 · Motor: SQL Server 2022 (Docker) · Base: `InventoryDb`

## Principios
- **Sin llaves foráneas** La integridad referencial se garantiza en la aplicación, y cada comprobación tiene su prueba.
- Los identificadores son `UNIQUEIDENTIFIER` generados por la aplicación.
- Todas las fechas son `DATETIME2` en UTC.
- Se conservan las restricciones `PRIMARY KEY`, `UNIQUE`, `NOT NULL`, `DEFAULT` y `CHECK`, como última barrera de integridad.
- Los índices únicos de texto aprovechan la intercalación por defecto de SQL Server, que no distingue mayúsculas. `Categories.Name` la declara de forma explícita (`SQL_Latin1_General_CP1_CI_AS`) para no depender de la configuración de la instancia.
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
| Sku | NVARCHAR(50) | No | Único |
| Name | NVARCHAR(200) | No | |
| Description | NVARCHAR(1000) | Sí | |
| StockQuantity | INT | No | Por defecto 0; `CHECK (StockQuantity >= 0)` |
| IsActive | BIT | No | Por defecto 1  |
| CreatedAtUtc | DATETIME2 | No | Por defecto: fecha actual UTC |
| UpdatedAtUtc | DATETIME2 | Sí | |

`StockQuantity` es un saldo derivado: solo cambia con movimientos.

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

Tabla inmutable: no hay actualización ni eliminación.

### Suppliers
| Columna | Tipo | Null | Restricción |
|---|---|---|---|
| Id | UNIQUEIDENTIFIER | No | PK |
| Name | NVARCHAR(200) | No | |
| Nit | NVARCHAR(30) | No | Único |
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
| UX_Products_Sku | Products | Sku | SKU único |
| IX_Products_CategoryId | Products | CategoryId (incluye IsActive) | Buscar productos por categoría y comprobar si una categoría tiene productos activos |
| IX_InventoryMovements_ProductId_OccurredAtUtc | InventoryMovements | ProductId, OccurredAtUtc DESC | Historial por producto |
| UX_Suppliers_NitId | Suppliers | Nit | NIT único |
| UX_ProductSuppliers_Link | ProductSuppliers | ProductId, SupplierId | Sin asociaciones duplicadas |
| IX_ProductSuppliers_SupplierId | ProductSuppliers | SupplierId | Productos de un proveedor |