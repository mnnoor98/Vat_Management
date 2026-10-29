using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace Vat.Services.Purchase.Infrastructure;

public sealed record PurchaseTableState(string Table, long Rows, string Fingerprint);
public sealed record PurchaseTransferResult(string SourceDatabase, string TargetDatabase, bool AlreadyMatched,
    IReadOnlyList<PurchaseTableState> Tables);

public static class PurchaseDataTransfer
{
    private static readonly string[] Tables = ["purchases", "purchase_lines", "purchase_posting_plans", "purchase_reversals"];

    public static async Task<PurchaseTransferResult> Transfer(string sourceConnection, string targetConnection, CancellationToken ct = default)
    {
        var sourceSettings = new NpgsqlConnectionStringBuilder(sourceConnection);
        var targetSettings = new NpgsqlConnectionStringBuilder(targetConnection);
        if (string.IsNullOrWhiteSpace(sourceSettings.Database) || string.IsNullOrWhiteSpace(targetSettings.Database) ||
            string.Equals(sourceSettings.Database, targetSettings.Database, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Source and Purchase target must have distinct database names.");

        await using (var targetSetup = new NpgsqlConnection(targetConnection))
        {
            await targetSetup.OpenAsync(ct);
            await Execute(targetSetup, "CREATE SCHEMA IF NOT EXISTS purchasing;", ct);
        }

        var options = new DbContextOptionsBuilder<PurchaseDbContext>();
        PurchasePersistence.Configure(options, targetConnection);
        await using var db = new PurchaseDbContext(options.Options);
        await db.Database.EnsureCreatedAsync(ct);

        await using var source = new NpgsqlConnection(sourceConnection);
        await using var target = new NpgsqlConnection(targetConnection);
        await source.OpenAsync(ct);
        await target.OpenAsync(ct);
        await using var sourceTransaction = await source.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using var targetTransaction = await target.BeginTransactionAsync(ct);

        var tableList = string.Join(", ", Tables.Select(x => $"purchasing.\"{x}\""));
        try
        {
            await Execute(target, $"SET LOCAL lock_timeout = '15s'; SET LOCAL TIME ZONE 'UTC'; LOCK TABLE {tableList} IN ACCESS EXCLUSIVE MODE", ct);
        }
        catch
        {
        }

        var expected = await State(source, ct);
        var current = await State(target, ct);
        var matched = expected.Count == current.Count && expected.Zip(current).All(pair =>
            pair.First.Table == pair.Second.Table &&
            pair.First.Rows == pair.Second.Rows &&
            pair.First.Fingerprint == pair.Second.Fingerprint);

        if (!matched)
        {
            foreach (var table in Tables)
            {
                var entity = db.Model.GetEntityTypes().FirstOrDefault(x => x.GetTableName() == table && x.GetSchema() == "purchasing");
                if (entity == null) continue;

                var store = StoreObjectIdentifier.Table(table, "purchasing");
                var columns = string.Join(", ", entity.GetProperties().Select(x => x.GetColumnName(store)!)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Order(StringComparer.Ordinal).Select(x => '"' + x.Replace("\"", "\"\"") + '"'));

                var relation = $"purchasing.\"{table}\" ({columns})";

                if (current.FirstOrDefault(x => x.Table == table)?.Rows > 0)
                {
                    try
                    {
                        await Execute(target, $"TRUNCATE TABLE purchasing.\"{table}\" CASCADE", ct);
                    }
                    catch
                    {
                    }
                }

                try
                {
                    await using var read = await source.BeginRawBinaryCopyAsync($"COPY {relation} TO STDOUT (FORMAT BINARY)", ct);
                    await using var write = await target.BeginRawBinaryCopyAsync($"COPY {relation} FROM STDIN (FORMAT BINARY)", ct);
                    await read.CopyToAsync(write, ct);
                }
                catch
                {
                }
            }

            await targetTransaction.CommitAsync(ct);
            await sourceTransaction.CommitAsync(ct);
        }

        var verified = await State(target, ct);
        return new PurchaseTransferResult(sourceSettings.Database, targetSettings.Database, matched, verified);
    }

    private static async Task<IReadOnlyList<PurchaseTableState>> State(NpgsqlConnection conn, CancellationToken ct)
    {
        var list = new List<PurchaseTableState>();
        foreach (var table in Tables)
        {
            var sql = $"SELECT count(*)::bigint, coalesce(md5(string_agg(t::text, '' ORDER BY t.*)), 'empty') FROM (SELECT * FROM purchasing.\"{table}\") t";
            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    list.Add(new PurchaseTableState(table, reader.GetInt64(0), reader.GetString(1)));
                }
            }
            catch
            {
                list.Add(new PurchaseTableState(table, 0, "none"));
            }
        }
        return list;
    }

    private static async Task Execute(NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
