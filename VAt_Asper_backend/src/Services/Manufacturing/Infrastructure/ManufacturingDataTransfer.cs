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
        await Execute(source, $"SET LOCAL lock_timeout = '15s'; SET LOCAL TIME ZONE 'UTC'; LOCK TABLE {tableList} IN SHARE MODE", ct);
        var workflowTables = "manufacturing.coefficient_documents, manufacturing.coefficient_commands, manufacturing.coefficient_audit";
        await Execute(target, $"SET LOCAL lock_timeout = '15s'; SET LOCAL TIME ZONE 'UTC'; LOCK TABLE {tableList}, {workflowTables} IN ACCESS EXCLUSIVE MODE", ct);
        await using (var active = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM manufacturing.coefficient_documents) OR EXISTS (SELECT 1 FROM manufacturing.coefficient_commands) OR EXISTS (SELECT 1 FROM manufacturing.coefficient_audit)", target))
            if ((bool)(await active.ExecuteScalarAsync(ct))!)
                throw new InvalidOperationException("Manufacturing target has active coefficient workflow history; metadata import is refused.");
        var expected = await State(source, ct);
        var existing = await State(target, ct);
        var matched = expected.SequenceEqual(existing);
        if (!matched && existing.Any(x => x.Rows != 0))
            throw new InvalidOperationException("Manufacturing target contains different data; automatic overwrite is refused.");

        if (!matched)
        {
            foreach (var table in Tables)
            {
                var entity = db.Model.GetEntityTypes().Single(x => x.GetTableName() == table && x.GetSchema() == "manufacturing");
                var store = StoreObjectIdentifier.Table(table, "manufacturing");
                var columns = string.Join(", ", entity.GetProperties().Select(x => x.GetColumnName(store)!)
                    .Order(StringComparer.Ordinal).Select(x => '"' + x.Replace("\"", "\"\"") + '"'));
                var relation = $"manufacturing.\"{table}\" ({columns})";
                // PostgreSQL binary COPY preserves UUIDs, timestamps, nulls, escaped text
                // and versions without converting private data through log/files.
                await using (var read = await source.BeginRawBinaryCopyAsync($"COPY {relation} TO STDOUT (FORMAT BINARY)", ct))
                await using (var write = await target.BeginRawBinaryCopyAsync($"COPY {relation} FROM STDIN (FORMAT BINARY)", ct))
                    await read.CopyToAsync(write, ct);
            }
            var actual = await State(target, ct);
            if (!expected.SequenceEqual(actual))
                throw new InvalidOperationException("Manufacturing copy verification failed; target data transaction will roll back.");
        }
        await targetTransaction.CommitAsync(ct);
        await sourceTransaction.CommitAsync(ct);
        return new(sourceSettings.Database, targetSettings.Database, matched, expected);
    }

    private static async Task<IReadOnlyList<ManufacturingTableState>> State(NpgsqlConnection connection, CancellationToken ct)
    {
        var rows = new List<ManufacturingTableState>();
        foreach (var table in Tables)
        {
            await using var command = new NpgsqlCommand($"""
                SELECT count(*), md5(coalesce(string_agg(to_jsonb(t)::text, E'\n'
                    ORDER BY t."TenantId", t."CompanyId", t."Id"), ''))
                FROM manufacturing."{table}" t
                """, connection);
            await using var reader = await command.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            rows.Add(new(table, reader.GetInt64(0), reader.GetString(1)));
        }
        return rows;
    }

    private static async Task Execute(NpgsqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }
}
