namespace Inventory.Domain.Entities;

public sealed class Supplier
{
    private Supplier(SupplierSnapshot snapshot)
    {
        Id = snapshot.Id;
        Name = snapshot.Name;
        Nit = snapshot.Nit;
        Email = snapshot.Email;
        Phone = snapshot.Phone;
        IsActive = snapshot.IsActive;
        CreatedAtUtc = snapshot.CreatedAtUtc;
        UpdatedAtUtc = snapshot.UpdatedAtUtc;
    }

    public Guid Id { get; }

    public string Name { get; }

    public string Nit { get; }

    public string? Email { get; }

    public string? Phone { get; }

    public bool IsActive { get; }

    public DateTime CreatedAtUtc { get; }

    public DateTime? UpdatedAtUtc { get; }

    public static Supplier Create(SupplierDraft draft)
    {
        var snapshot = new SupplierSnapshot(
            Id: Guid.NewGuid(),
            Name: SupplierRules.ValidateName(draft.Name),
            Nit: SupplierRules.ValidateNit(draft.Nit),
            Email: SupplierRules.ValidateEmail(draft.Email),
            Phone: SupplierRules.ValidatePhone(draft.Phone),
            IsActive: true,
            CreatedAtUtc: draft.CreatedAtUtc,
            UpdatedAtUtc: null);

        return new Supplier(snapshot);
    }

    public static Supplier Restore(SupplierSnapshot snapshot)
    {
        return new Supplier(snapshot);
    }
}
