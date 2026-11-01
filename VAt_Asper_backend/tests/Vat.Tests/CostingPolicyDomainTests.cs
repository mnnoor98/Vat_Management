using Vat.Domain;
using Vat.Domain.Costing;
using Vat.Application.Security;
using Vat.Services.Identity.Controllers;
using Xunit;

namespace Vat.Tests;

public sealed class CostingPolicyDomainTests
{
    [Theory]
    [InlineData("FIFO")]
    [InlineData("WEIGHTED_AVERAGE")]
    public void Policy_requires_independent_versioned_approval_and_cannot_be_approved_twice(string method)
    {
        var policy = CostingPolicy.Create(Guid.NewGuid(), Guid.NewGuid(), null, method, new(2026, 1, 1), new(2027, 1, 1), "maker", DateTimeOffset.UtcNow);
        Assert.Equal(403, Assert.Throws<DomainException>(() => policy.Approve("maker", 1, DateTimeOffset.UtcNow)).Status);
        Assert.Equal(409, Assert.Throws<DomainException>(() => policy.Approve("checker", 2, DateTimeOffset.UtcNow)).Status);
        policy.Approve("checker", 1, DateTimeOffset.UtcNow);
        Assert.Equal("Approved", policy.State); Assert.Equal(2, policy.Version); Assert.Equal(method, policy.Method);
        Assert.Equal(409, Assert.Throws<DomainException>(() => policy.Approve("other", 2, DateTimeOffset.UtcNow)).Status);
    }

    [Fact]
    public void Costing_permissions_follow_the_inventory_module_instead_of_admin_module_fallback()
    {
        var inventory = LocalAccountPolicy.PermissionsFor(["Approver"], ["MOD_INVENTORY"]);
        Assert.Contains(Permissions.CostingRead, inventory); Assert.Contains(Permissions.CostingApprove, inventory);
        Assert.DoesNotContain(Permissions.CostingManage, inventory);
        var adminOnly = LocalAccountPolicy.PermissionsFor(["Approver"], ["MOD_ADMIN"]);
        Assert.DoesNotContain(Permissions.CostingRead, adminOnly); Assert.DoesNotContain(Permissions.CostingApprove, adminOnly);
    }
}
