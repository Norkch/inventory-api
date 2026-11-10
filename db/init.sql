SET QUOTED_IDENTIFIER ON;
GO
IF DB_ID('InventoryDb') IS NULL CREATE DATABASE InventoryDb;
GO
USE InventoryDb;
GO
IF OBJECT_ID('dbo.Categories') IS NULL
CREATE TABLE dbo.Categories(
    Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
    Name         NVARCHAR(100)    COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
    Description  NVARCHAR(500)    NULL,
    IsActive     BIT              NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT 1,
    CreatedAtUtc DATETIME2        NOT NULL CONSTRAINT DF_Categories_CreatedAtUtc DEFAULT SYSUTCDATETIME()
);
GO
IF COL_LENGTH('dbo.Categories', 'IsActive') IS NULL
    ALTER TABLE dbo.Categories ADD IsActive BIT NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT 1;
GO
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Categories') AND name = 'UX_Categories_Name')
   AND (EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Categories') AND name = 'UX_Categories_Name' AND has_filter = 0)
        OR EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Categories') AND name = 'Name' AND collation_name <> 'SQL_Latin1_General_CP1_CI_AS'))
    DROP INDEX UX_Categories_Name ON dbo.Categories;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Categories') AND name = 'Name' AND collation_name <> 'SQL_Latin1_General_CP1_CI_AS')
    ALTER TABLE dbo.Categories ALTER COLUMN Name NVARCHAR(100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL;
GO
IF OBJECT_ID('dbo.Suppliers') IS NULL
CREATE TABLE dbo.Suppliers(
    Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Suppliers PRIMARY KEY,
    Name         NVARCHAR(200)    NOT NULL,
    Nit        NVARCHAR(30)     NOT NULL,
    Email        NVARCHAR(200)    NULL,
    Phone        NVARCHAR(30)     NULL,
    IsActive     BIT              NOT NULL CONSTRAINT DF_Suppliers_IsActive DEFAULT 1,
    CreatedAtUtc DATETIME2        NOT NULL CONSTRAINT DF_Suppliers_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc DATETIME2        NULL
);
GO
IF OBJECT_ID('dbo.Products') IS NULL
CREATE TABLE dbo.Products(
    Id            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
    CategoryId    UNIQUEIDENTIFIER NOT NULL,
    Sku           NVARCHAR(50)     COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
    Name          NVARCHAR(200)    NOT NULL,
    Description   NVARCHAR(1000)   NULL,
    StockQuantity INT              NOT NULL CONSTRAINT DF_Products_StockQuantity DEFAULT 0 CONSTRAINT CK_Products_StockQuantity CHECK (StockQuantity >= 0),
    IsActive      BIT              NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT 1,
    CreatedAtUtc  DATETIME2        NOT NULL CONSTRAINT DF_Products_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc  DATETIME2        NULL
);
GO
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Products') AND name = 'UX_Products_Sku')
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Products') AND name = 'Sku' AND collation_name <> 'SQL_Latin1_General_CP1_CI_AS')
    DROP INDEX UX_Products_Sku ON dbo.Products;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Products') AND name = 'Sku' AND collation_name <> 'SQL_Latin1_General_CP1_CI_AS')
    ALTER TABLE dbo.Products ALTER COLUMN Sku NVARCHAR(50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL;
GO
IF OBJECT_ID('dbo.ProductSuppliers') IS NULL
CREATE TABLE dbo.ProductSuppliers(
    Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProductSuppliers PRIMARY KEY,
    ProductId    UNIQUEIDENTIFIER NOT NULL,
    SupplierId   UNIQUEIDENTIFIER NOT NULL,
    SupplierSku  NVARCHAR(50)     NULL,
    CreatedAtUtc DATETIME2        NOT NULL CONSTRAINT DF_ProductSuppliers_CreatedAtUtc DEFAULT SYSUTCDATETIME()
);
GO
IF OBJECT_ID('dbo.InventoryMovements') IS NULL
CREATE TABLE dbo.InventoryMovements(
    Id            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_InventoryMovements PRIMARY KEY,
    ProductId     UNIQUEIDENTIFIER NOT NULL,
    SupplierId    UNIQUEIDENTIFIER NULL,
    MovementType  TINYINT          NOT NULL CONSTRAINT CK_InventoryMovements_Type CHECK (MovementType IN (1, 2)),
    Quantity      INT              NOT NULL CONSTRAINT CK_InventoryMovements_Quantity CHECK (Quantity > 0),
    StockAfter    INT              NOT NULL,
    Reason        NVARCHAR(250)    NULL,
    OccurredAtUtc DATETIME2        NOT NULL,
    RegisteredBy  NVARCHAR(100)    NOT NULL,
    CONSTRAINT CK_InventoryMovements_Supplier CHECK (MovementType = 1 OR SupplierId IS NULL)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Categories_Name')
    CREATE UNIQUE INDEX UX_Categories_Name ON dbo.Categories(Name) WHERE IsActive = 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Suppliers_Nit')
    CREATE UNIQUE INDEX UX_Suppliers_Nit ON dbo.Suppliers(Nit);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Products_Sku')
    CREATE UNIQUE INDEX UX_Products_Sku ON dbo.Products(Sku);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_CategoryId')
    CREATE INDEX IX_Products_CategoryId ON dbo.Products(CategoryId) INCLUDE (IsActive);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ProductSuppliers_Link')
    CREATE UNIQUE INDEX UX_ProductSuppliers_Link ON dbo.ProductSuppliers(ProductId, SupplierId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductSuppliers_SupplierId')
    CREATE INDEX IX_ProductSuppliers_SupplierId ON dbo.ProductSuppliers(SupplierId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InventoryMovements_ProductId_OccurredAtUtc')
    CREATE INDEX IX_InventoryMovements_ProductId_OccurredAtUtc ON dbo.InventoryMovements(ProductId, OccurredAtUtc DESC);
GO