using Inventory.Application.Movements;
using Inventory.Domain.Entities;
using Inventory.Domain.Exceptions;
using Inventory.UnitTests.Fakes;

namespace Inventory.UnitTests.Movements;

public sealed class RegisterMovementHandlerTests
{
    private const string Subject = "3f2a9c1e-0000-4000-8000-000000000001";
    private static readonly Guid ProductId = Guid.NewGuid();

    private readonly FakeMovementWriteRepository _writeRepository = new();

    [Fact]
    public async Task BR11_Registers_MovementWithClockDateAuthorAndStockAfter()
    {
        _writeRepository.StockAfter = 10;
        var movement = await CreateHandler().HandleAsync(CreateCommand("Entry", 10, "  Compra inicial  "), CancellationToken.None);
        var received = Assert.Single(_writeRepository.Received);
        Assert.Equal(received.Id, movement.Id);
        Assert.Equal(ProductId, movement.ProductId);
        Assert.Equal("Entry", movement.Type);
        Assert.Equal(10, movement.Quantity);
        Assert.Equal(10, movement.StockAfter);
        Assert.Equal("Compra inicial", movement.Reason);
        Assert.Equal(Subject, movement.RegisteredBy);
        Assert.Equal(FakeClock.FixedUtcNow, movement.OccurredAtUtc);
    }

    [Fact]
    public async Task BR11_Registers_TypeInCanonicalForm()
    {
        var movement = await CreateHandler().HandleAsync(CreateCommand("  exit ", 4, null), CancellationToken.None);
        Assert.Equal("Exit", movement.Type);
        Assert.Equal(MovementType.Exit, Assert.Single(_writeRepository.Received).Type);
    }

    [Fact]
    public async Task BR11_Rejects_ProductThatIsNotActive()
    {
        _writeRepository.Status = MovementRegistrationStatus.ProductNotFound;
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => CreateHandler().HandleAsync(CreateCommand("Entry", 1, null), CancellationToken.None));
        Assert.Equal($"No existe un producto activo con el id '{ProductId}'.", exception.Message);
    }

    [Fact]
    public async Task BR12_Rejects_ExitAboveAvailableStock()
    {
        _writeRepository.Status = MovementRegistrationStatus.InsufficientStock;
        var exception = await Assert.ThrowsAsync<InsufficientStockException>(() => CreateHandler().HandleAsync(CreateCommand("Exit", 6, null), CancellationToken.None));
        Assert.Equal(MovementMessages.InsufficientStock, exception.Message);
    }

    [Fact]
    public async Task Rejects_EntryThatOverflowsTheStock()
    {
        _writeRepository.Status = MovementRegistrationStatus.StockOverflow;
        var exception = await Assert.ThrowsAsync<ConflictException>(() => CreateHandler().HandleAsync(CreateCommand("Entry", 1000, null), CancellationToken.None));
        Assert.IsNotType<InsufficientStockException>(exception);
        Assert.Equal(MovementMessages.StockOverflow, exception.Message);
    }

    [Fact]
    public async Task Rejects_InvalidBodyWithoutCallingTheRepository()
    {
        await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().HandleAsync(CreateCommand("Entry", 0, null), CancellationToken.None));
        Assert.Empty(_writeRepository.Received);
    }

    private RegisterMovementHandler CreateHandler()
    {
        return new RegisterMovementHandler(_writeRepository, new FakeClock());
    }

    private static RegisterMovementCommand CreateCommand(string? type, int? quantity, string? reason)
    {
        return new RegisterMovementCommand(ProductId, type, quantity, reason, new MovementRegistrant(Subject));
    }
}
