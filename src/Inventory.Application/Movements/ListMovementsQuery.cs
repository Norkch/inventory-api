namespace Inventory.Application.Movements;

public sealed record ListMovementsQuery(Guid ProductId, int? Page, int? PageSize);
