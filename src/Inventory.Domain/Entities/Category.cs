namespace Inventory.Domain.Entities;

public sealed class Category
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    private Category(Guid id, string name, string? description, DateTime createdAtUtc)
    {
        Id = id;
        Name = name;
        Description = description;
        CreatedAtUtc = createdAtUtc;
        IsActive = true;
    }

    public Guid Id { get; }

    public string Name { get; }

    public string? Description { get; }

    public bool IsActive { get; }

    public DateTime CreatedAtUtc { get; }

    public static Category Create(string? name, string? description, DateTime createdAtUtc)
    {
        var validName = ValidateName(name);
        var validDescription = ValidateDescription(description);

        return new Category(Guid.NewGuid(), validName, validDescription, createdAtUtc);
    }

    private static string ValidateName(string? name)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        Guard.AgainstBlank(trimmedName, "El nombre de la categoría es obligatorio.");
        Guard.AgainstTooLong(trimmedName, NameMaxLength, $"El nombre de la categoría no puede superar los {NameMaxLength} caracteres.");

        return trimmedName;
    }

    private static string? ValidateDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }
        var trimmedDescription = description.Trim();
        Guard.AgainstTooLong(trimmedDescription, DescriptionMaxLength, $"La descripción de la categoría no puede superar los {DescriptionMaxLength} caracteres.");

        return trimmedDescription;
    }
}
