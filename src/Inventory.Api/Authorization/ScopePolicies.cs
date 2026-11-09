using Inventory.Application.Security;
using Microsoft.AspNetCore.Authorization;

namespace Inventory.Api.Authorization;

public static class ScopePolicies
{
    public const string InventoryRead = "InventoryRead";
    public const string InventoryWrite = "InventoryWrite";
    private const string ScopeClaimType = "scope";

    public static void AddScopePolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(InventoryRead, policy => RequireScope(policy, InventoryScopes.Read));
        options.AddPolicy(InventoryWrite, policy => RequireScope(policy, InventoryScopes.Write));
    }

    private static void RequireScope(AuthorizationPolicyBuilder policy, string scope)
    {
        policy.RequireAuthenticatedUser()
            .RequireAssertion(context => new ScopeSet(context.User.FindFirst(ScopeClaimType)?.Value).Contains(scope));
    }
}
