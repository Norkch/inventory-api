using Inventory.Application.Common;

namespace Inventory.Application.Products;

public sealed record ProductListCriteria(PageRequest PageRequest, Guid? CategoryId, string? Search);
