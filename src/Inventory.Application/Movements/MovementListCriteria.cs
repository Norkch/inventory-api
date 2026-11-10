using Inventory.Application.Common;

namespace Inventory.Application.Movements;

public sealed record MovementListCriteria(Guid ProductId, PageRequest PageRequest);
