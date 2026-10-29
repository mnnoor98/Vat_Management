using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Vat.Services.Reporting.Infrastructure;

internal static class FinancialGenerationLock
{
    // Every inbox write, retirement and coverage verification must take the same lock.
    public static Task Acquire(ReportingDbContext db, Guid tenant, Guid company, CancellationToken ct)
    {
        var key = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"reporting-live-inbox:{tenant}:{company}")));
        return db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
    }
}
