using System.Security.Claims;
using Vat.Api.Security;
using Xunit;

namespace Vat.Tests;

public sealed class ClaimsActorTests
{
    [Fact]
    public void Duplicate_tenant_or_malformed_company_claims_are_rejected()
    {
        var claims = new List<Claim> { new("sub", "maker"), new("tenant_id", Guid.NewGuid().ToString()) };
        Assert.True(ClaimsActor.TryRead(new ClaimsPrincipal(new ClaimsIdentity(claims)), out _));
        claims.Add(new("tenant_id", Guid.NewGuid().ToString()));
        Assert.False(ClaimsActor.TryRead(new ClaimsPrincipal(new ClaimsIdentity(claims)), out _));
        claims.RemoveAt(2);
        claims.Add(new("company_id", "untrusted-input"));
        Assert.False(ClaimsActor.TryRead(new ClaimsPrincipal(new ClaimsIdentity(claims)), out _));
    }

    [Fact]
    public void Missing_subject_is_rejected()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenant_id", Guid.NewGuid().ToString())]));
        Assert.False(ClaimsActor.TryRead(principal, out _));
    }
}
