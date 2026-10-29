using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace Vat.Services.Inventory.Infrastructure;

public sealed record InventoryTableState(string Table, long Rows, string Fingerprint);
public sealed record InventoryTransferResult(string SourceDatabase, string TargetDatabase, bool AlreadyMatched,
    IReadOnlyList<InventoryTableState> Tables);

public static class InventoryDataTransfer
{
    private static readonly string[] Tables =
    [
        "warehouses",
        "stock_balances",
        "stock_movements",
        "transfers",
        "transfer_lines",
        "transfer_journal",
        "adjustments",
        "adjustment_lines"
    ];

    public static async Task<InventoryTransferResult> Transfer(string sourceConnection, string targetConnection, CancellationToken ct = default)
    {
        var sourceSettings = new NpgsqlConnectionStringBuilder(sourceConnection);
        var targetSettings = new NpgsqlConnectionStringBuilder(targetConnection);
        if (string.IsNullOrWhiteSpace(sourceSettings.Database) || string.IsNullOrWhiteSpace(targetSettings.Database) ||
            string.Equals(sourceSettings.Database, targetSettings.Database, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Source and Inventory target must have distinct database names.");

        await using (var targetSetup = new NpgsqlConnection(targetConnection))
        {
            await targetSetup.OpenAsync(ct);
            await Execute(targetSetup, "CREATE SCHEMA IF NOT EXISTS inventory;", ct);
        }

        var options = new DbContextOptionsBuilder<InventoryDbContext>();
        InventoryPersistence.Configure(options, targetConnection);
        await using var db = new InventoryDbContext(options.Options);
        try
        {
            var creator = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>(db.Database);
            await creator.CreateTablesAsync(ct);
        }
        catch
        {
        }

        await using var source = new NpgsqlConnection(sourceConnection);
        await using var target = new NpgsqlConnection(targetConnection);
        await source.OpenAsync(ct);
        await target.OpenAsync(ct);
        await using var sourceTransaction = await source.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using var targetTransaction = await target.BeginTransactionAsync(ct);

        var tableList = string.Join(", ", Tables.Select(x => $"inventory.\"{x}\""));
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
                var entity = db.Model.GetEntityTypes().FirstOrDefault(x => x.GetTableName() == table && x.GetSchema() == "inventory");
                if (entity is null) continue;

                var store = StoreObjectIdentifier.Table(table, "inventory");
                var columns = string.Join(", ", entity.GetProperties().Select(x => x.GetColumnName(store)!)
                    .Where(x => x != null)
                    .Order(StringComparer.Ordinal).Select(x => '"' + x.Replace("\"", "\"\"") + '"'));

                var relation = $"inventory.\"{table}\" ({columns})";

                if (current.FirstOrDefault(x => x.Table == table)?.Rows > 0)
                {
                    try
                    {
                        await Execute(target, $"TRUNCATE TABLE inventory.\"{table}\" CASCADE", ct);
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
        return new InventoryTransferResult(sourceSettings.Database, targetSettings.Database, matched, verified);
    }

    private static async Task<IReadOnlyList<InventoryTableState>> State(NpgsqlConnection conn, CancellationToken ct)
    {
        var list = new List<InventoryTableState>();
        foreach (var table in Tables)
        {
            var sql = $"SELECT count(*)::bigint, coalesce(md5(string_agg(t::text, '' ORDER BY t.*)), 'empty') FROM (SELECT * FROM inventory.\"{table}\") t";
            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    list.Add(new InventoryTableState(table, reader.GetInt64(0), reader.GetString(1)));
                }
            }
            catch
            {
                list.Add(new InventoryTableState(table, 0, "none"));
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
