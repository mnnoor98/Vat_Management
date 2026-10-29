using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vat.Infrastructure.Persistence;

if (args.Length is < 4 or > 5 || !Guid.TryParse(args[0], out var tenant) || !Guid.TryParse(args[1], out var company)
    || (args.Length == 5 && args[4] != "--apply"))
    throw new ArgumentException("Usage: <tenant UUID> <company UUID> <operator identity> <reason> [--apply]. Default is preview.");
var connection = Environment.GetEnvironmentVariable("ConnectionStrings__VatMigrationDatabase")
    ?? throw new InvalidOperationException("Supply the operator-only ConnectionStrings__VatMigrationDatabase securely.");
await using var db = new VatDbContext(new DbContextOptionsBuilder<VatDbContext>().UseNpgsql(connection).Options);
var result = await new LegacyTransferJournalRepair(db, TimeProvider.System).Run(tenant, company, args[2], args[3], args.Length == 5, default);
Console.WriteLine(JsonSerializer.Serialize(result));
