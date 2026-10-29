using System.Security.Claims;
using Vat.Application.Security;

namespace Vat.Api.Security;

public static class ClaimsActor
{
    public static bool TryRead(ClaimsPrincipal principal, out Actor? actor)
    {
        actor = null;
        var tenants = principal.FindAll("tenant_id").Select(x => x.Value).ToArray();
        var subjects = principal.FindAll("sub").Select(x => x.Value).ToArray();
        if (tenants.Length != 1 || !Guid.TryParse(tenants[0], out var tenant) || tenant == Guid.Empty ||
            subjects.Length != 1 || string.IsNullOrWhiteSpace(subjects[0]) || subjects[0].Length > 200)
            return false;
        var companies = new HashSet<Guid>();
        foreach (var claim in principal.FindAll("company_id"))
        {
            if (!Guid.TryParse(claim.Value, out var company) || company == Guid.Empty) return false;
            companies.Add(company);
        }
        actor = new Actor(tenant, subjects[0], companies,
            principal.FindAll("permission").Select(x => x.Value).ToHashSet(StringComparer.Ordinal));
        return true;
    }

    public static Actor GetActor(this HttpContext context) => TryRead(context.User, out var actor)
        ? actor! : throw new InvalidOperationException("The authenticated principal has invalid application claims.");
}
