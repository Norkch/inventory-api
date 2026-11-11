namespace Inventory.Infrastructure.Persistence.Read;

public sealed class SupplierRecord
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Nit { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
