using System.Security.Claims;

// Servern äger regeln om vem som får vara i vilken grupp.
// Klientens gruppnamn är ett önskemål, inte ett beslut.
public sealed class GroupPolicy
{
    // Grupp -> roller som krävs. Tom lista = öppen för alla inloggade användare.
    // Grupper som inte finns här nekas. Okänt är inte tillåtet.
    private static readonly Dictionary<string, string[]> RequiredRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["General"] = [],
        ["Support"] = ["Admin", "Support"],
        ["Administrators"] = ["Admin"],
    };

    public IEnumerable<string> KnownGroups => RequiredRoles.Keys;

    public bool MayAccess(ClaimsPrincipal user, string groupName)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (!RequiredRoles.TryGetValue(groupName, out var roles))
        {
            return false;
        }

        return roles.Length == 0 || roles.Any(user.IsInRole);
    }
}
