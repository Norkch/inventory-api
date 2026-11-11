using System.Text.RegularExpressions;
using Inventory.Domain.Exceptions;

namespace Inventory.Domain;

public static class SupplierRules
{
    public const int NameMaxLength = 200;
    public const int NitMaxLength = 30;
    public const int EmailMaxLength = 200;
    public const int PhoneMaxLength = 30;
    public const int PhoneMinDigits = 7;
    public const int PhoneMaxDigits = 15;

    private static readonly TimeSpan PatternTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant, PatternTimeout);
    private static readonly Regex PhonePattern = new(@"^\+?[0-9 -]+$", RegexOptions.CultureInvariant, PatternTimeout);

    public static string ValidateName(string? name)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        Guard.AgainstBlank(trimmedName, "El nombre del proveedor es obligatorio.");
        Guard.AgainstTooLong(trimmedName, NameMaxLength, $"El nombre del proveedor no puede superar los {NameMaxLength} caracteres.");

        return trimmedName;
    }

    public static string ValidateNit(string? nit)
    {
        var trimmedNit = nit?.Trim() ?? string.Empty;
        Guard.AgainstBlank(trimmedNit, "El NIT del proveedor es obligatorio.");
        Guard.AgainstTooLong(trimmedNit, NitMaxLength, $"El NIT del proveedor no puede superar los {NitMaxLength} caracteres.");

        return trimmedNit;
    }

    public static string? ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }
        var trimmedEmail = email.Trim();
        Guard.AgainstTooLong(trimmedEmail, EmailMaxLength, $"El correo del proveedor no puede superar los {EmailMaxLength} caracteres.");
        if (!EmailPattern.IsMatch(trimmedEmail))
        {
            throw new ValidationException("El correo del proveedor no tiene un formato válido.");
        }

        return trimmedEmail;
    }

    public static string? ValidatePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }
        var trimmedPhone = phone.Trim();
        Guard.AgainstTooLong(trimmedPhone, PhoneMaxLength, $"El teléfono del proveedor no puede superar los {PhoneMaxLength} caracteres.");
        if (!HasValidPhoneFormat(trimmedPhone))
        {
            throw new ValidationException("El teléfono del proveedor no tiene un formato válido.");
        }

        return trimmedPhone;
    }

    private static bool HasValidPhoneFormat(string phone)
    {
        var digitCount = phone.Count(char.IsDigit);

        return PhonePattern.IsMatch(phone) && digitCount >= PhoneMinDigits && digitCount <= PhoneMaxDigits;
    }
}
