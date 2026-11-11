namespace Inventory.Domain.Entities;

public sealed class SupplierChanges
{
    private SupplierChanges(SupplierEdit validEdit)
    {
        SupplierId = validEdit.SupplierId;
        Name = validEdit.Name!;
        Email = validEdit.Email;
        Phone = validEdit.Phone;
        UpdatedAtUtc = validEdit.UpdatedAtUtc;
    }

    public Guid SupplierId { get; }

    public string Name { get; }

    public string? Email { get; }

    public string? Phone { get; }

    public DateTime UpdatedAtUtc { get; }

    public static SupplierChanges Create(SupplierEdit edit)
    {
        var validEdit = edit with
        {
            Name = SupplierRules.ValidateName(edit.Name),
            Email = SupplierRules.ValidateEmail(edit.Email),
            Phone = SupplierRules.ValidatePhone(edit.Phone)
        };

        return new SupplierChanges(validEdit);
    }
}
