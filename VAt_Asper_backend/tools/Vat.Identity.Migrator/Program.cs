using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;
using Vat.Services.Identity.Infrastructure;

var source = Environment.GetEnvironmentVariable("ConnectionStrings__VatDatabase")
    ?? "Host=127.0.0.1;Port=5432;Database=vat_management;Username=postgres;Password=123456;";
var target = Environment.GetEnvironmentVariable("ConnectionStrings__IdentityDatabase")
    ?? "Host=127.0.0.1;Port=5432;Database=vat_identity;Username=postgres;Password=123456;";
var sourceSettings = new NpgsqlConnectionStringBuilder(source);
var targetSettings = new NpgsqlConnectionStringBuilder(target);
var database = targetSettings.Database ?? "";
if (!Regex.IsMatch(database, "^vat_identity(?:_[a-z0-9]+)*$") || string.Equals(database, sourceSettings.Database, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Target must be a distinct vat_identity database (optional lowercase suffixes are allowed).");
var maintenance = new NpgsqlConnectionStringBuilder(target) { Database = "postgres", Pooling = false };
await using (var admin = new NpgsqlConnection(maintenance.ConnectionString))
{
    await admin.OpenAsync();
    await using var exists = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @database)", admin);
    exists.Parameters.AddWithValue("database", database);
    if (!(bool)(await exists.ExecuteScalarAsync())!)
    {
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin);
        await create.ExecuteNonQueryAsync();
    }
}
var result = await IdentityDataTransfer.Transfer(source, target);
var directory = Path.GetFullPath("artifacts/ownership");
Directory.CreateDirectory(directory);
var report = Path.Combine(directory, "identity-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + Guid.NewGuid().ToString("N") + ".json");
await File.WriteAllTextAsync(report, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Identity ownership verified: {result.SourceDatabase} -> {result.TargetDatabase}; {result.Tables.Count} tables, {result.Tables.Sum(x => x.Rows)} rows; already matched: {result.AlreadyMatched}.");
Console.WriteLine($"Verification report: {report}");
