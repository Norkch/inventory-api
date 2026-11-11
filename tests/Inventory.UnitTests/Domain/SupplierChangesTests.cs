using Inventory.Domain.Entities;
using Inventory.Domain.Exceptions;

namespace Inventory.UnitTests.Domain;

public sealed class SupplierChangesTests
{
    private static readonly DateTime UpdatedAtUtc = new(2026, 11, 10, 11, 0, 0, DateTimeKind.Utc);
    private static readonly Guid SupplierId = Guid.NewGuid();

    [Fact]
    public void Create_ValidatesAndTrimsEveryField()
    {
        var changes = SupplierChanges.Create(new SupplierEdit(SupplierId, " Andina S.A.S. ", " compras@andina.com ", " 6011234567 ", UpdatedAtUtc));
        Assert.Equal(SupplierId, changes.SupplierId);
        Assert.Equal("Andina S.A.S.", changes.Name);
        Assert.Equal("compras@andina.com", changes.Email);
        Assert.Equal("6011234567", changes.Phone);
        Assert.Equal(UpdatedAtUtc, changes.UpdatedAtUtc);
    }

    [Fact]
    public void Create_Replaces_MissingEmailAndPhoneWithNull()
    {
        var changes = SupplierChanges.Create(new SupplierEdit(SupplierId, "Andina", null, "   ", UpdatedAtUtc));
        Assert.Null(changes.Email);
        Assert.Null(changes.Phone);
    }

    [Fact]
    public void BR08_Create_Rejects_MissingName()
    {
        var exception = Assert.Throws<ValidationException>(() => SupplierChanges.Create(new SupplierEdit(SupplierId, "", "juan@mail", "123", UpdatedAtUtc)));
        Assert.Equal("El nombre del proveedor es obligatorio.", exception.Message);
    }

    [Fact]
    public void Create_Rejects_MalformedEmail()
    {
        var exception = Assert.Throws<ValidationException>(() => SupplierChanges.Create(new SupplierEdit(SupplierId, "Andina", "juan@mail", "123", UpdatedAtUtc)));
        Assert.Equal("El correo del proveedor no tiene un formato válido.", exception.Message);
    }

    [Fact]
    public void Create_Rejects_MalformedPhone()
    {
        var exception = Assert.Throws<ValidationException>(() => SupplierChanges.Create(new SupplierEdit(SupplierId, "Andina", null, "123", UpdatedAtUtc)));
        Assert.Equal("El teléfono del proveedor no tiene un formato válido.", exception.Message);
    }

    [Fact]
    public void BR08_Changes_DoNotCarryTheNitOrTheCreationDate()
    {
        var propertyNames = typeof(SupplierChanges).GetProperties().Select(property => property.Name);
        Assert.DoesNotContain("Nit", propertyNames);
        Assert.DoesNotContain("CreatedAtUtc", propertyNames);
    }
}
