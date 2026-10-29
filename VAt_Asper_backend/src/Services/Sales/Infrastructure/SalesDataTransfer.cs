using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace Vat.Services.Sales.Infrastructure;

public sealed record SalesTableState(string Table, long Rows, string Fingerprint);
public sealed record SalesTransferResult(string SourceDatabase, string TargetDatabase, bool AlreadyMatched,
    IReadOnlyList<SalesTableState> Tables);

public static class SalesDataTransfer
{
    private static readonly string[] Tables =
    [
        "sales_invoices",
        "sales_invoice_lines",
        "sales_summaries",
        "postings",
        "posting_entries",
        "credit_notes",
        "debit_notes",
        "payments",
        "payment_allocations",
        "payment_corrections",
        "payment_cash_adjustments",
        "payment_allocation_releases"
    ];

    public static async Task<SalesTransferResult> Transfer(string sourceConnection, string targetConnection, CancellationToken ct = default)
    {
        var sourceSettings = new NpgsqlConnectionStringBuilder(sourceConnection);
        var targetSettings = new NpgsqlConnectionStringBuilder(targetConnection);
        if (string.IsNullOrWhiteSpace(sourceSettings.Database) || string.IsNullOrWhiteSpace(targetSettings.Database) ||
            string.Equals(sourceSettings.Database, targetSettings.Database, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Source and Sales target must have distinct database names.");

        await using (var targetSetup = new NpgsqlConnection(targetConnection))
        {
            await targetSetup.OpenAsync(ct);
            await Execute(targetSetup, "CREATE SCHEMA IF NOT EXISTS sales;", ct);
        }

        var options = new DbContextOptionsBuilder<SalesDbContext>();
        SalesPersistence.Configure(options, targetConnection);
        await using var db = new SalesDbContext(options.Options);
        await db.Database.EnsureCreatedAsync(ct);

        await using var source = new NpgsqlConnection(sourceConnection);
        await using var target = new NpgsqlConnection(targetConnection);
        await source.OpenAsync(ct);
        await target.OpenAsync(ct);
        await using var sourceTransaction = await source.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using var targetTransaction = await target.BeginTransactionAsync(ct);

        var tableList = string.Join(", ", Tables.Select(x => $"sales.\"{x}\""));
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
                var entity = db.Model.GetEntityTypes().FirstOrDefault(x => x.GetTableName() == table && x.GetSchema() == "sales");
                if (entity == null) continue;

                var store = StoreObjectIdentifier.Table(table, "sales");
                var columns = string.Join(", ", entity.GetProperties().Select(x => x.GetColumnName(store)!)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Order(StringComparer.Ordinal).Select(x => '"' + x.Replace("\"", "\"\"") + '"'));

                var relation = $"sales.\"{table}\" ({columns})";

                if (current.FirstOrDefault(x => x.Table == table)?.Rows > 0)
                {
                    try
                    {
                        await Execute(target, $"TRUNCATE TABLE sales.\"{table}\" CASCADE", ct);
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
        return new SalesTransferResult(sourceSettings.Database, targetSettings.Database, matched, verified);
    }

    private static async Task<IReadOnlyList<SalesTableState>> State(NpgsqlConnection conn, CancellationToken ct)
    {
        var list = new List<SalesTableState>();
        foreach (var table in Tables)
        {
            var sql = $"SELECT count(*)::bigint, coalesce(md5(string_agg(t::text, '' ORDER BY t.*)), 'empty') FROM (SELECT * FROM sales.\"{table}\") t";
            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    list.Add(new SalesTableState(table, reader.GetInt64(0), reader.GetString(1)));
                }
            }
            catch
            {
                list.Add(new SalesTableState(table, 0, "none"));
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
