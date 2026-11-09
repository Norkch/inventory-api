namespace Inventory.Application.Security;

public sealed class ScopeSet
{
    private readonly string[] _scopes;

    public ScopeSet(string? scopeClaim)
    {
        _scopes = (scopeClaim ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    public IReadOnlyList<string> Scopes => _scopes;

    public bool Contains(string scope)
    {
        return _scopes.Contains(scope, StringComparer.Ordinal);
    }
}
