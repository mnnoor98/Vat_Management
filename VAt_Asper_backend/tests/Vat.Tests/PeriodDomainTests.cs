using Vat.Domain;
using Vat.Domain.Company;
using Xunit;

namespace Vat.Tests;

public sealed class PeriodDomainTests
{
    [Fact]
    public void Close_requires_a_different_checker_and_freezes_the_snapshot()
    {
        var period = FiscalPeriod.Create(Guid.NewGuid(), Guid.NewGuid(), "October", new(2026, 10, 1), new(2026, 11, 1));
        period.RequestClose("maker", 1);
        var snapshot = new PeriodCloseSnapshot(period.Id, period.From, period.Until, DateTimeOffset.UtcNow, [], [], [], 0, 0, 0, "TEST");
        Assert.Equal("self_approval_forbidden", Assert.Throws<DomainException>(() => period.Close("maker", 2, snapshot, DateTimeOffset.UtcNow)).Code);
        period.Close("checker", 2, snapshot, DateTimeOffset.UtcNow);
        Assert.Equal(FiscalPeriodStatus.Closed, period.Status);
        Assert.Equal(snapshot, period.Snapshot);
        Assert.Throws<DomainException>(() => period.CancelClose(3));
    }

    [Fact]
    public void Cancelling_close_advances_version_and_allows_another_request()
    {
        var period = FiscalPeriod.Create(Guid.NewGuid(), Guid.NewGuid(), "October", new(2026, 10, 1), new(2026, 11, 1));
        period.RequestClose("maker", 1);
        period.CancelClose(2);
        Assert.Equal(3, period.Version);
        Assert.Null(period.CloseRequestedBy);
        Assert.Equal("version_conflict", Assert.Throws<DomainException>(() => period.RequestClose("maker", 1)).Code);
        period.RequestClose("maker", 3);
        Assert.Equal(FiscalPeriodStatus.Closing, period.Status);
    }

    [Fact]
    public void Empty_or_reversed_intervals_are_rejected()
    {
        Assert.Throws<DomainException>(() => FiscalPeriod.Create(Guid.NewGuid(), Guid.NewGuid(), "Invalid", new(2026, 10, 1), new(2026, 10, 1)));
    }
}
