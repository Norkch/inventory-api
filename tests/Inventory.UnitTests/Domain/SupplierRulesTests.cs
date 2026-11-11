using Inventory.Domain;
using Inventory.Domain.Exceptions;

namespace Inventory.UnitTests.Domain;

public sealed class SupplierRulesTests
{
    private const string InvalidEmailMessage = "El correo del proveedor no tiene un formato válido.";
    private const string InvalidPhoneMessage = "El teléfono del proveedor no tiene un formato válido.";

    [Fact]
    public void BR08_ValidateName_TrimsSurroundingSpaces()
    {
        Assert.Equal("Ferretería Andina", SupplierRules.ValidateName("  Ferretería Andina  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BR08_ValidateName_Rejects_MissingOrWhitespaceName(string? name)
    {
        var exception = Assert.Throws<ValidationException>(() => SupplierRules.ValidateName(name));
        Assert.Equal("El nombre del proveedor es obligatorio.", exception.Message);
    }

    [Fact]
    public void BR08_ValidateName_Accepts_NameOf200Characters()
    {
        var name = new string('a', 200);
        Assert.Equal(name, SupplierRules.ValidateName(name));
    }

    [Fact]
    public void BR08_ValidateName_Rejects_NameOf201Characters()
    {
        var exception = Assert.Throws<ValidationException>(() => SupplierRules.ValidateName(new string('a', 201)));
        Assert.Equal("El nombre del proveedor no puede superar los 200 caracteres.", exception.Message);
    }

    [Fact]
    public void BR08_ValidateNit_TrimsSurroundingSpaces()
    {
        Assert.Equal("900123456-7", SupplierRules.ValidateNit("  900123456-7  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BR08_ValidateNit_Rejects_MissingOrWhitespaceNit(string? nit)
    {
        var exception = Assert.Throws<ValidationException>(() => SupplierRules.ValidateNit(nit));
        Assert.Equal("El NIT del proveedor es obligatorio.", exception.Message);
    }

    [Fact]
    public void BR08_ValidateNit_Accepts_NitOf30Characters()
    {
        var nit = new string('9', 30);
        Assert.Equal(nit, SupplierRules.ValidateNit(nit));
    }

    [Fact]
    public void BR08_ValidateNit_Rejects_NitOf31Characters()
    {
        var exception = Assert.Throws<ValidationException>(() => SupplierRules.ValidateNit(new string('9', 31)));
        Assert.Equal("El NIT del proveedor no puede superar los 30 caracteres.", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BR08_ValidateEmail_Returns_NullForMissingOrWhitespaceEmail(string? email)
    {
        Assert.Null(SupplierRules.ValidateEmail(email));
    }

    [Fact]
    public void BR08_ValidateEmail_TrimsAndKeepsLetterCase()
    {
        Assert.Equal("Juan@Mail.com", SupplierRules.ValidateEmail("  Juan@Mail.com  "));
    }

    [Fact]
    public void BR08_ValidateEmail_Accepts_WellFormedEmail()
    {
        Assert.Equal("juan@mail.com", SupplierRules.ValidateEmail("juan@mail.com"));
    }

    [Theory]
    [InlineData("juan.mail.com")]
    [InlineData("juan@mail")]
    [InlineData("juan perez@mail.com")]
    [InlineData("juan@@mail.com")]
    public void BR08_ValidateEmail_Rejects_MalformedEmail(string email)
    {
        var exception = Assert.Throws<ValidationException>(() => SupplierRules.ValidateEmail(email));
        Assert.Equal(InvalidEmailMessage, exception.Message);
    }

    [Fact]
    public void BR08_ValidateEmail_Rejects_EmailOf201Characters()
    {
        var email = new string('a', 192) + "@mail.com";
        var exception = Assert.Throws<ValidationException>(() => SupplierRules.ValidateEmail(email));
        Assert.Equal("El correo del proveedor no puede superar los 200 caracteres.", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BR08_ValidatePhone_Returns_NullForMissingOrWhitespacePhone(string? phone)
    {
        Assert.Null(SupplierRules.ValidatePhone(phone));
    }

    [Theory]
    [InlineData("+57 300-123-4567")]
    [InlineData("123-4567")]
    public void BR08_ValidatePhone_Accepts_WellFormedPhone(string phone)
    {
        Assert.Equal(phone, SupplierRules.ValidatePhone($"  {phone}  "));
    }

    [Theory]
    [InlineData("123-456")]
    [InlineData("1234567890123456")]
    [InlineData("300 ABC 4567")]
    [InlineData("300+1234567")]
    public void BR08_ValidatePhone_Rejects_MalformedPhone(string phone)
    {
        var exception = Assert.Throws<ValidationException>(() => SupplierRules.ValidatePhone(phone));
        Assert.Equal(InvalidPhoneMessage, exception.Message);
    }

    [Fact]
    public void BR08_ValidatePhone_Rejects_PhoneOf31Characters()
    {
        var phone = "1--2-3-4-5-6-7-8-9-0-1-2-3-4--5";
        var exception = Assert.Throws<ValidationException>(() => SupplierRules.ValidatePhone(phone));
        Assert.Equal("El teléfono del proveedor no puede superar los 30 caracteres.", exception.Message);
    }
}
