namespace Inventory.UnitTests.Fakes;

public sealed record FakeTokenRequest(string Issuer, string Audience, DateTime Expires)
{
    public static FakeTokenRequest Valid()
    {
        return new FakeTokenRequest(FakeAuthOptions.Issuer, FakeAuthOptions.Audience, DateTime.UtcNow.AddMinutes(5));
    }
}
