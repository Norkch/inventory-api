using Inventory.Domain.Entities;
using Inventory.Domain.Exceptions;

namespace Inventory.UnitTests.Domain;

public sealed class SupplierTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 11, 10, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_ValidatesAndTrimsEveryField()
    {
        var supplier = Supplier.Create(new SupplierDraft(" Ferretería Andina ", " 900123456-7 ", " ventas@andina.com ", " +57 300 123 4567 ", CreatedAtUtc));
        Assert.Equal("Ferretería Andina", supplier.Name);
        Assert.Equal("900123456-7", supplier.Nit);
        Assert.Equal("ventas@andina.com", supplier.Email);
        Assert.Equal("+57 300 123 4567", supplier.Phone);
    }

    [Fact]
    public void Create_IsActive_WithCreationDateAndNoUpdateDate()
    {
        var supplier = Supplier.Create(new SupplierDraft("Andina", "900123456-7", null, null, CreatedAtUtc));
        Assert.NotEqual(Guid.Empty, supplier.Id);
        Assert.True(supplier.IsActive);
        Assert.Equal(CreatedAtUtc, supplier.CreatedAtUtc);
        Assert.Null(supplier.UpdatedAtUtc);
        Assert.Null(supplier.Email);
        Assert.Null(supplier.Phone);
    }

    [Fact]
    public void BR08_Create_ReportsTheNameFirst_WhenEveryFieldIsInvalid()
    {
        var exception = Assert.Throws<ValidationException>(() => Supplier.Create(new SupplierDraft("", "", "juan@mail", "123", CreatedAtUtc)));
        Assert.Equal("El nombre del proveedor es obligatorio.", exception.Message);
    }

    [Fact]
    public void BR08_Create_ReportsTheNitBeforeTheEmail()
    {
        var exception = Assert.Throws<ValidationException>(() => Supplier.Create(new SupplierDraft("Andina", " ", "juan@mail", "123", CreatedAtUtc)));
        Assert.Equal("El NIT del proveedor es obligatorio.", exception.Message);
    }

    [Fact]
    public void Create_ReportsTheEmailBeforeThePhone()
    {
        var exception = Assert.Throws<ValidationException>(() => Supplier.Create(new SupplierDraft("Andina", "900123456-7", "juan@mail", "123", CreatedAtUtc)));
        Assert.Equal("El correo del proveedor no tiene un formato válido.", exception.Message);
    }

    [Fact]
    public void Create_Rejects_MalformedPhone()
    {
        var exception = Assert.Throws<ValidationException>(() => Supplier.Create(new SupplierDraft("Andina", "900123456-7", null, "123", CreatedAtUtc)));
        Assert.Equal("El teléfono del proveedor no tiene un formato válido.", exception.Message);
    }

    [Fact]
    public void Restore_KeepsEverySnapshotValue()
    {
        var snapshot = new SupplierSnapshot(Id: Guid.NewGuid(), Name: "Andina", Nit: "900123456-7", Email: "ventas@andina.com", Phone: "6011234567", IsActive: false, CreatedAtUtc: CreatedAtUtc, UpdatedAtUtc: CreatedAtUtc.AddHours(1));
        var supplier = Supplier.Restore(snapshot);
        Assert.Equal(snapshot.Id, supplier.Id);
        Assert.Equal(snapshot.Name, supplier.Name);
        Assert.Equal(snapshot.Nit, supplier.Nit);
        Assert.Equal(snapshot.Email, supplier.Email);
        Assert.Equal(snapshot.Phone, supplier.Phone);
        Assert.False(supplier.IsActive);
        Assert.Equal(snapshot.CreatedAtUtc, supplier.CreatedAtUtc);
        Assert.Equal(snapshot.UpdatedAtUtc, supplier.UpdatedAtUtc);
    }
}
