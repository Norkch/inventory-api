using Inventory.Application.Suppliers;
using Inventory.Domain.Exceptions;
using Inventory.UnitTests.Fakes;

namespace Inventory.UnitTests.Suppliers;

public sealed class DeactivateSupplierHandlerTests
{
    private static readonly Guid SupplierId = Guid.NewGuid();

    private readonly FakeSupplierWriteRepository _writeRepository = new();

    [Fact]
    public async Task BR09_Deactivates_ActiveSupplierWithClockDate()
    {
        _writeRepository.ActiveSupplierIds.Add(SupplierId);
        await CreateHandler().HandleAsync(new DeactivateSupplierCommand(SupplierId), CancellationToken.None);
        Assert.Equal(new SupplierDeactivation(SupplierId, FakeClock.FixedUtcNow), _writeRepository.AppliedDeactivation);
        Assert.Empty(_writeRepository.ActiveSupplierIds);
    }

    [Fact]
    public async Task BR09_Rejects_SupplierThatIsNotActive()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => CreateHandler().HandleAsync(new DeactivateSupplierCommand(SupplierId), CancellationToken.None));
        Assert.Equal($"No existe un proveedor activo con el id '{SupplierId}'.", exception.Message);
    }

    private DeactivateSupplierHandler CreateHandler()
    {
        return new DeactivateSupplierHandler(_writeRepository, new FakeClock());
    }
}
