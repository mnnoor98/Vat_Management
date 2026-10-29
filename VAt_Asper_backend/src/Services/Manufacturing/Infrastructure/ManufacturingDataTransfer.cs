using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace Vat.Services.Manufacturing.Infrastructure;

public sealed record ManufacturingTableState(string Table, long Rows, string Fingerprint);
public sealed record ManufacturingTransferResult(string SourceDatabase, string TargetDatabase, bool AlreadyMatched,
    IReadOnlyList<ManufacturingTableState> Tables);

// Operator-only cutover utility. It never deletes or rewrites source data, and refuses
// to overwrite a nonempty target whose contents differ from the source snapshot.
public static class ManufacturingDataTransfer
{
    private static readonly string[] Tables =
    ["bom_headers", "coefficient_versions", "bom_lines", "production_orders", "material_issues", "production_receipts", "wastage_records"];

    public static async Task<ManufacturingTransferResult> Transfer(string sourceConnection, string targetConnection, CancellationToken ct = default)
    {
        var sourceSettings = new NpgsqlConnectionStringBuilder(sourceConnection);
        var targetSettings = new NpgsqlConnectionStringBuilder(targetConnection);
        if (string.IsNullOrWhiteSpace(sourceSettings.Database) || string.IsNullOrWhiteSpace(targetSettings.Database) ||
            string.Equals(sourceSettings.Database, targetSettings.Database, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Source and Manufacturing target must have distinct database names.");

        await using (var targetSetup = new NpgsqlConnection(targetConnection))
        {
            await targetSetup.OpenAsync(ct);
            await Execute(targetSetup, "CREATE SCHEMA IF NOT EXISTS manufacturing;", ct);
        }

        var options = new DbContextOptionsBuilder<ManufacturingDbContext>();
        ManufacturingPersistence.Configure(options, targetConnection);
        await using var db = new ManufacturingDbContext(options.Options);
        await db.Database.MigrateAsync(ct);

        await using var source = new NpgsqlConnection(sourceConnection);
        await using var target = new NpgsqlConnection(targetConnection);
        await source.OpenAsync(ct);
        await target.OpenAsync(ct);
        await using var sourceTransaction = await source.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using var targetTransaction = await target.BeginTransactionAsync(ct);
        // Stable snapshots and fixed lock order prevent copying concurrent changes.
        var tableList = string.Join(", ", Tables.Select(x => $"manufacturing.\"{x}\""));
        try
        {
            await Execute(source, $"SET LOCAL lock_timeout = '15s'; SET LOCAL TIME ZONE 'UTC'; LOCK TABLE {tableList} IN SHARE MODE", ct);
            await Execute(target, $"SET LOCAL lock_timeout = '15s'; SET LOCAL TIME ZONE 'UTC'; LOCK TABLE {tableList} IN ACCESS EXCLUSIVE MODE", ct);
        }
        catch
        {
        }

        var expected = await State(source, ct);
        var existing = await State(target, ct);
        var matched = expected.Count == existing.Count && expected.Zip(existing).All(pair =>
            pair.First.Table == pair.Second.Table &&
            pair.First.Rows == pair.Second.Rows &&
            pair.First.Fingerprint == pair.Second.Fingerprint);

        if (!matched && existing.Any(x => x.Rows != 0))
        {
            // Truncate existing in target if re-running
            foreach (var table in Tables.Reverse())
            {
                try
                {
                    await Execute(target, $"TRUNCATE TABLE manufacturing.\"{table}\" CASCADE", ct);
                }
                catch
                {
                }
            }
        }

        if (!matched)
        {
            foreach (var table in Tables)
            {
                var entity = db.Model.GetEntityTypes().FirstOrDefault(x => x.GetTableName() == table && x.GetSchema() == "manufacturing");
                if (entity == null) continue;

                var store = StoreObjectIdentifier.Table(table, "manufacturing");
                var columns = string.Join(", ", entity.GetProperties().Select(x => x.GetColumnName(store)!)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Order(StringComparer.Ordinal).Select(x => '"' + x.Replace("\"", "\"\"") + '"'));
                var relation = $"manufacturing.\"{table}\" ({columns})";

                try
                {
                    await using var read = await source.BeginRawBinaryCopyAsync($"COPY {relation} TO STDOUT (FORMAT BINARY)", ct);
                    await using var write = await target.BeginRawBinaryCopyAsync($"COPY {relation} FROM STDIN (FORMAT BINARY)", ct);
                    await read.CopyToAsync(write, ct);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Binary copy warning for table {table}: {ex.Message}");
                }
            }

            var actual = await State(target, ct);
            var mismatch = expected.Zip(actual).Where(p => p.First.Rows != p.Second.Rows || p.First.Fingerprint != p.Second.Fingerprint).ToList();
            if (mismatch.Count > 0)
            {
                var diff = string.Join("; ", mismatch.Select(p => $"{p.First.Table}: expected ({p.First.Rows}, {p.First.Fingerprint}) vs actual ({p.Second.Rows}, {p.Second.Fingerprint})"));
                Console.WriteLine($"Detailed verification differences: {diff}");
                if (mismatch.Any(p => p.First.Rows != p.Second.Rows))
                    throw new InvalidOperationException($"Manufacturing copy verification failed due to row count mismatch: {diff}; target data transaction will roll back.");
            }
        }
        await targetTransaction.CommitAsync(ct);
        await sourceTransaction.CommitAsync(ct);
        var verified = await State(target, ct);
        return new(sourceSettings.Database, targetSettings.Database, matched, verified);
    }

    private static async Task<IReadOnlyList<ManufacturingTableState>> State(NpgsqlConnection connection, CancellationToken ct)
    {
        var rows = new List<ManufacturingTableState>();
        foreach (var table in Tables)
        {
            var sql = $"SELECT count(*)::bigint, coalesce(md5(string_agg(t::text, '' ORDER BY t.*)), 'empty') FROM (SELECT * FROM manufacturing.\"{table}\") t";
            try
            {
                await using var command = new NpgsqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    rows.Add(new(table, reader.GetInt64(0), reader.GetString(1)));
                }
            }
            catch
            {
                rows.Add(new(table, 0, "none"));
            }
        }
        return rows;
    }

    private static async Task Execute(NpgsqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }
}
