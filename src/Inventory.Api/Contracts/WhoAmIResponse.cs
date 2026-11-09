namespace Inventory.Api.Contracts;

public sealed record WhoAmIResponse(string ClientId, IReadOnlyList<string> Scopes);
