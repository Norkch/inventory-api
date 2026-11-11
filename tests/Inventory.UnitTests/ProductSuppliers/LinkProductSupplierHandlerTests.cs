using Inventory.Application.ProductSuppliers;
using Inventory.Domain.Exceptions;
using Inventory.UnitTests.Fakes;

namespace Inventory.UnitTests.ProductSuppliers;

public sealed class LinkProductSupplierHandlerTests
{
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();

    private readonly FakeProductSupplierWriteRepository _writeRepository = new();

    [Fact]
    public async Task BR10_Links_ProductAndSupplierWithClockDate()
    {
        var link = await CreateHandler().HandleAsync(ValidCommand(), CancellationToken.None);
        var received = _writeRepository.ReceivedLink!;
        Assert.Equal(ProductId, received.ProductId);
        Assert.Equal(SupplierId, received.SupplierId);
        Assert.Equal("FA-778", received.SupplierSku);
        Assert.Equal(FakeClock.FixedUtcNow, received.CreatedAtUtc);
        Assert.Equal(ProductId, link.ProductId);
        Assert.Equal("Ferretería Andina", link.SupplierName);
        Assert.Equal(FakeClock.FixedUtcNow, link.CreatedAtUtc);
    }

    [Fact]
    public async Task Rejects_InvalidBody_WithoutCallingTheRepository()
    {
        var command = ValidCommand() with { SupplierSku = new string('A', 51) };
        await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().HandleAsync(command, CancellationToken.None));
        Assert.Empty(_writeRepository.Calls);
    }

    [Fact]
    public async Task BR09_Rejects_MissingSupplier_WithoutCallingTheRepository()
    {
        var command = ValidCommand() with { SupplierId = null };
        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().HandleAsync(command, CancellationToken.None));
        Assert.Equal("El proveedor es obligatorio.", exception.Message);
        Assert.Empty(_writeRepository.Calls);
    }

    [Fact]
    public async Task Rejects_ProductThatIsNotActive()
    {
        _writeRepository.Status = ProductSupplierLinkStatus.ProductNotFound;
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => CreateHandler().HandleAsync(ValidCommand(), CancellationToken.None));
        Assert.Equal($"No existe un producto activo con el id '{ProductId}'.", exception.Message);
    }

    [Fact]
    public async Task BR09_Rejects_SupplierThatIsNotActive()
    {
        _writeRepository.Status = ProductSupplierLinkStatus.SupplierNotAvailable;
        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().HandleAsync(ValidCommand(), CancellationToken.None));
        Assert.Equal(ProductSupplierMessages.SupplierNotAvailable, exception.Message);
    }

    [Fact]
    public async Task BR10_Rejects_PairThatIsAlreadyLinked()
    {
        _writeRepository.Status = ProductSupplierLinkStatus.AlreadyLinked;
        var exception = await Assert.ThrowsAsync<ConflictException>(() => CreateHandler().HandleAsync(ValidCommand(), CancellationToken.None));
        Assert.Equal(ProductSupplierMessages.AlreadyLinked, exception.Message);
    }

    private LinkProductSupplierHandler CreateHandler()
    {
        return new LinkProductSupplierHandler(_writeRepository, new FakeClock());
    }

    private static LinkProductSupplierCommand ValidCommand()
    {
        return new LinkProductSupplierCommand(ProductId, SupplierId, " FA-778 ");
    }
}
