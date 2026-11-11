using Inventory.Application.Suppliers;
using Inventory.Domain.Exceptions;
using Inventory.UnitTests.Fakes;

namespace Inventory.UnitTests.Suppliers;

public sealed class GetSupplierByIdHandlerTests
{
    private readonly FakeSupplierReadRepository _readRepository = new();

    [Fact]
    public async Task Returns_ActiveSupplier()
    {
        var active = new SupplierDto(Guid.NewGuid(), "Andina", "900123456-7", null, null, FakeClock.FixedUtcNow, null);
        _readRepository.ActiveSuppliers.Add(active);
        var supplier = await CreateHandler().HandleAsync(new GetSupplierByIdQuery(active.Id), CancellationToken.None);
        Assert.Equal(active, supplier);
    }

    [Fact]
    public async Task BR09_Rejects_SupplierThatIsNotActive()
    {
        var supplierId = Guid.NewGuid();
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => CreateHandler().HandleAsync(new GetSupplierByIdQuery(supplierId), CancellationToken.None));
        Assert.Equal($"No existe un proveedor activo con el id '{supplierId}'.", exception.Message);
    }

    private GetSupplierByIdHandler CreateHandler()
    {
        return new GetSupplierByIdHandler(_readRepository);
    }
}
