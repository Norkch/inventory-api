using Inventory.Domain;
using Inventory.Domain.Entities;
using Inventory.Domain.Exceptions;

namespace Inventory.UnitTests.Domain;

public sealed class ProductSupplierTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 11, 10, 9, 30, 0, DateTimeKind.Utc);
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();

    [Fact]
    public void BR10_Create_LinksTheProductAndTheSupplier()
    {
        var link = ProductSupplier.Create(new ProductSupplierDraft(ProductId, SupplierId, " FA-778 ", CreatedAtUtc));
        Assert.NotEqual(Guid.Empty, link.Id);
        Assert.Equal(ProductId, link.ProductId);
        Assert.Equal(SupplierId, link.SupplierId);
        Assert.Equal("FA-778", link.SupplierSku);
        Assert.Equal(CreatedAtUtc, link.CreatedAtUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BR09_Create_Rejects_MissingSupplier_BeforeValidatingTheSku(bool sendEmptyGuid)
    {
        Guid? supplierId = sendEmptyGuid ? Guid.Empty : null;
        var exception = Assert.Throws<ValidationException>(() => ProductSupplier.Create(new ProductSupplierDraft(ProductId, supplierId, new string('A', 51), CreatedAtUtc)));
        Assert.Equal("El proveedor es obligatorio.", exception.Message);
    }

    [Fact]
    public void ValidateSupplierSku_TrimsSurroundingSpaces()
    {
        Assert.Equal("FA-778", ProductSupplierRules.ValidateSupplierSku("  FA-778  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateSupplierSku_Returns_NullForMissingOrWhitespaceSku(string? supplierSku)
    {
        Assert.Null(ProductSupplierRules.ValidateSupplierSku(supplierSku));
    }

    [Fact]
    public void ValidateSupplierSku_Accepts_SkuOf50Characters()
    {
        var supplierSku = new string('A', 50);
        Assert.Equal(supplierSku, ProductSupplierRules.ValidateSupplierSku(supplierSku));
    }

    [Fact]
    public void ValidateSupplierSku_Rejects_SkuOf51Characters()
    {
        var exception = Assert.Throws<ValidationException>(() => ProductSupplierRules.ValidateSupplierSku(new string('A', 51)));
        Assert.Equal("El código del producto en el proveedor no puede superar los 50 caracteres.", exception.Message);
    }
}
