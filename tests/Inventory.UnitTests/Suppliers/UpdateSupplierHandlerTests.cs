using Inventory.Application.Suppliers;
using Inventory.Domain.Entities;
using Inventory.Domain.Exceptions;
using Inventory.UnitTests.Fakes;

namespace Inventory.UnitTests.Suppliers;

public sealed class UpdateSupplierHandlerTests
{
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly DateTime CreatedAtUtc = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

    private readonly FakeSupplierWriteRepository _writeRepository = new();

    [Fact]
    public async Task Updates_ActiveSupplierWithClockDate()
    {
        _writeRepository.UpdateResult = UpdatedSupplier();
        var supplier = await CreateHandler().HandleAsync(ValidCommand(), CancellationToken.None);
        var changes = _writeRepository.AppliedChanges!;
        Assert.Equal(SupplierId, changes.SupplierId);
        Assert.Equal("Andina S.A.S.", changes.Name);
        Assert.Equal("compras@andina.com", changes.Email);
        Assert.Null(changes.Phone);
        Assert.Equal(FakeClock.FixedUtcNow, changes.UpdatedAtUtc);
        Assert.Equal("900123456-7", supplier.Nit);
        Assert.Equal(CreatedAtUtc, supplier.CreatedAtUtc);
        Assert.Equal(FakeClock.FixedUtcNow, supplier.UpdatedAtUtc);
    }

    [Fact]
    public async Task Rejects_InvalidBody_WithoutCallingTheRepository()
    {
        var command = ValidCommand() with { Name = "" };
        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().HandleAsync(command, CancellationToken.None));
        Assert.Equal("El nombre del proveedor es obligatorio.", exception.Message);
        Assert.Empty(_writeRepository.Calls);
    }

    [Fact]
    public async Task Rejects_SupplierThatIsNotActive()
    {
        _writeRepository.UpdateResult = null;
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => CreateHandler().HandleAsync(ValidCommand(), CancellationToken.None));
        Assert.Equal($"No existe un proveedor activo con el id '{SupplierId}'.", exception.Message);
    }

    private UpdateSupplierHandler CreateHandler()
    {
        return new UpdateSupplierHandler(_writeRepository, new FakeClock());
    }

    private static UpdateSupplierCommand ValidCommand()
    {
        return new UpdateSupplierCommand(SupplierId, " Andina S.A.S. ", "compras@andina.com", null);
    }

    private static Supplier UpdatedSupplier()
    {
        return Supplier.Restore(new SupplierSnapshot(SupplierId, "Andina S.A.S.", "900123456-7", "compras@andina.com", null, true, CreatedAtUtc, FakeClock.FixedUtcNow));
    }
}
