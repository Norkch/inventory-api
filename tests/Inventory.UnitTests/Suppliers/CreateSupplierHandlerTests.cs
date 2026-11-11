using Inventory.Application.Suppliers;
using Inventory.Domain.Exceptions;
using Inventory.UnitTests.Fakes;

namespace Inventory.UnitTests.Suppliers;

public sealed class CreateSupplierHandlerTests
{
    private readonly FakeSupplierWriteRepository _writeRepository = new();

    [Fact]
    public async Task BR08_Creates_ActiveSupplierWithClockDate()
    {
        var supplier = await CreateHandler().HandleAsync(ValidCommand(), CancellationToken.None);
        var stored = Assert.Single(_writeRepository.Created);
        Assert.Equal(stored.Id, supplier.Id);
        Assert.True(stored.IsActive);
        Assert.Equal("Ferretería Andina", supplier.Name);
        Assert.Equal("900123456-7", supplier.Nit);
        Assert.Equal("ventas@andina.com", supplier.Email);
        Assert.Equal("+57 300 123 4567", supplier.Phone);
        Assert.Equal(FakeClock.FixedUtcNow, supplier.CreatedAtUtc);
        Assert.Null(supplier.UpdatedAtUtc);
    }

    [Fact]
    public async Task BR08_Rejects_InvalidBody_WithoutCheckingTheNit()
    {
        var command = ValidCommand() with { Email = "juan@mail" };
        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().HandleAsync(command, CancellationToken.None));
        Assert.Equal("El correo del proveedor no tiene un formato válido.", exception.Message);
        Assert.Empty(_writeRepository.Calls);
    }

    [Theory]
    [InlineData("900123456-7")]
    [InlineData(" abc-123 ")]
    public async Task BR08_Rejects_NitOfAnotherSupplier_WithoutCreating(string nit)
    {
        _writeRepository.ExistingNits.Add("900123456-7");
        _writeRepository.ExistingNits.Add("ABC-123");
        var command = ValidCommand() with { Nit = nit };
        var exception = await Assert.ThrowsAsync<ConflictException>(() => CreateHandler().HandleAsync(command, CancellationToken.None));
        Assert.Equal($"Ya existe un proveedor con el NIT '{nit.Trim()}'.", exception.Message);
        Assert.Equal(new[] { nameof(FakeSupplierWriteRepository.ExistsWithSameNitAsync) }, _writeRepository.Calls);
    }

    [Fact]
    public async Task BR08_Propagates_ConflictFromTheRepository_WhenTwoCreationsRace()
    {
        var raceConflict = new ConflictException("Ya existe un proveedor con el NIT '900123456-7'.");
        _writeRepository.CreateException = raceConflict;
        var exception = await Assert.ThrowsAsync<ConflictException>(() => CreateHandler().HandleAsync(ValidCommand(), CancellationToken.None));
        Assert.Same(raceConflict, exception);
        Assert.Empty(_writeRepository.Created);
    }

    private CreateSupplierHandler CreateHandler()
    {
        return new CreateSupplierHandler(_writeRepository, new FakeClock());
    }

    private static CreateSupplierCommand ValidCommand()
    {
        return new CreateSupplierCommand(" Ferretería Andina ", "900123456-7", "ventas@andina.com", "+57 300 123 4567");
    }
}
