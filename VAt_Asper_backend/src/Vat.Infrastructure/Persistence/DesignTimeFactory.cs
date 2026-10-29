using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Vat.Infrastructure.Persistence;

public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<VatDbContext>
{
    public VatDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__VatDatabase")
            ?? throw new InvalidOperationException("Set ConnectionStrings__VatDatabase before running EF tools.");
        return new(new DbContextOptionsBuilder<VatDbContext>().UseNpgsql(connection).Options);
    }
}
