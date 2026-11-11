using Inventory.Domain.Entities;
using Inventory.Domain.Exceptions;

namespace Inventory.UnitTests.Domain;

public sealed class InventoryMovementTests
{
    private static readonly DateTime OccurredAtUtc = new(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ProductId = Guid.NewGuid();
    private const string Subject = "3f2a9c1e-0000-4000-8000-000000000001";

    [Fact]
    public void Create_ValidatesEveryFieldAndKeepsWhatItReceives()
    {
        var movement = InventoryMovement.Create(CreateDraft(" entry ", 10, "  Compra inicial  "));
        Assert.NotEqual(Guid.Empty, movement.Id);
        Assert.Equal(ProductId, movement.ProductId);
        Assert.Equal(MovementType.Entry, movement.Type);
        Assert.Equal(10, movement.Quantity);
        Assert.Equal("Compra inicial", movement.Reason);
        Assert.Equal(Subject, movement.RegisteredBy);
        Assert.Equal(OccurredAtUtc, movement.OccurredAtUtc);
    }

    [Fact]
    public void Create_HasNoStockAfterUntilItIsRegistered()
    {
        var movement = InventoryMovement.Create(CreateDraft("Entry", 10, null));
        Assert.Null(movement.StockAfter);
    }

    [Fact]
    public void BR11_Create_Rejects_InvalidType()
    {
        Assert.Throws<ValidationException>(() => InventoryMovement.Create(CreateDraft("Salida", 10, null)));
    }

    [Fact]
    public void BR11_Create_Rejects_ZeroQuantity()
    {
        Assert.Throws<ValidationException>(() => InventoryMovement.Create(CreateDraft("Exit", 0, null)));
    }

    [Fact]
    public void Create_Rejects_BlankAuthor()
    {
        var draft = CreateDraft("Exit", 1, null) with { RegisteredBy = " " };
        Assert.Throws<ValidationException>(() => InventoryMovement.Create(draft));
    }

    [Fact]
    public void StockDelta_IsPositive_ForAnEntry()
    {
        var movement = InventoryMovement.Create(CreateDraft("Entry", 7, null));
        Assert.Equal(7, movement.StockDelta);
    }

    [Fact]
    public void StockDelta_IsNegative_ForAnExit()
    {
        var movement = InventoryMovement.Create(CreateDraft("Exit", 4, null));
        Assert.Equal(-4, movement.StockDelta);
    }

    [Fact]
    public void WithStockAfter_KeepsTheMovementAndSetsTheBalance()
    {
        var movement = InventoryMovement.Create(CreateDraft("Exit", 4, "Venta"));
        var registered = movement.WithStockAfter(6);
        Assert.Equal(6, registered.StockAfter);
        Assert.Equal(movement.Id, registered.Id);
        Assert.Equal(movement.ProductId, registered.ProductId);
        Assert.Equal(movement.Type, registered.Type);
        Assert.Equal(movement.Quantity, registered.Quantity);
        Assert.Equal(movement.Reason, registered.Reason);
        Assert.Equal(movement.RegisteredBy, registered.RegisteredBy);
        Assert.Equal(movement.OccurredAtUtc, registered.OccurredAtUtc);
        Assert.Null(movement.StockAfter);
    }

    [Fact]
    public void BR16_Create_KeepsTheSupplierOfAnEntry()
    {
        var supplierId = Guid.NewGuid();
        var movement = InventoryMovement.Create(CreateDraft("Entry", 3, null) with { SupplierId = supplierId });
        Assert.Equal(supplierId, movement.SupplierId);
        Assert.Equal(supplierId, movement.WithStockAfter(3).SupplierId);
    }

    [Fact]
    public void BR16_Create_Rejects_ExitWithSupplier()
    {
        var draft = CreateDraft("Exit", 1, null) with { SupplierId = Guid.NewGuid() };
        var exception = Assert.Throws<ValidationException>(() => InventoryMovement.Create(draft));
        Assert.Equal("Una salida no puede indicar proveedor.", exception.Message);
    }

    [Fact]
    public void BR16_Create_ValidatesTheSupplierAfterTheTypeAndTheQuantity()
    {
        var invalidType = CreateDraft("Salida", 1, null) with { SupplierId = Guid.NewGuid() };
        var invalidQuantity = CreateDraft("Exit", 0, null) with { SupplierId = Guid.NewGuid() };
        Assert.Equal("El tipo de movimiento debe ser 'Entry' o 'Exit'.", Assert.Throws<ValidationException>(() => InventoryMovement.Create(invalidType)).Message);
        Assert.Equal("La cantidad del movimiento debe ser un entero mayor que 0.", Assert.Throws<ValidationException>(() => InventoryMovement.Create(invalidQuantity)).Message);
    }

    private static MovementDraft CreateDraft(string? type, int? quantity, string? reason)
    {
        return new MovementDraft(ProductId, type, quantity, reason, null, Subject, OccurredAtUtc);
    }
}
