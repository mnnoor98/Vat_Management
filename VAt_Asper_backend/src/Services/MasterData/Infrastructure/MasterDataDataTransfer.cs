using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace Vat.Services.MasterData.Infrastructure;

public sealed record MasterDataTableState(string Table, long Rows, string Fingerprint);
public sealed record MasterDataTransferResult(string SourceDatabase, string TargetDatabase, bool AlreadyMatched,
    IReadOnlyList<MasterDataTableState> Tables);

public static class MasterDataDataTransfer
{
    private static readonly string[] Tables = ["products", "suppliers", "customers"];

    public static async Task<MasterDataTransferResult> Transfer(string sourceConnection, string targetConnection, CancellationToken ct = default)
    {
        var sourceSettings = new NpgsqlConnectionStringBuilder(sourceConnection);
        var targetSettings = new NpgsqlConnectionStringBuilder(targetConnection);
        if (string.IsNullOrWhiteSpace(sourceSettings.Database) || string.IsNullOrWhiteSpace(targetSettings.Database) ||
            string.Equals(sourceSettings.Database, targetSettings.Database, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Source and MasterData target must have distinct database names.");

        await using (var targetSetup = new NpgsqlConnection(targetConnection))
        {
            await targetSetup.OpenAsync(ct);
            await Execute(targetSetup, "CREATE SCHEMA IF NOT EXISTS masterdata;", ct);
        }

        var options = new DbContextOptionsBuilder<MasterDataDbContext>();
        MasterDataPersistence.Configure(options, targetConnection);
        await using var db = new MasterDataDbContext(options.Options);
        await db.Database.EnsureCreatedAsync(ct);

        await using var source = new NpgsqlConnection(sourceConnection);
        await using var target = new NpgsqlConnection(targetConnection);
        await source.OpenAsync(ct);
        await target.OpenAsync(ct);
        await using var sourceTransaction = await source.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using var targetTransaction = await target.BeginTransactionAsync(ct);

        var tableList = string.Join(", ", Tables.Select(x => $"masterdata.\"{x}\""));
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
                var entity = db.Model.GetEntityTypes().FirstOrDefault(x => x.GetTableName() == table && x.GetSchema() == "masterdata");
                if (entity == null) continue;

                var store = StoreObjectIdentifier.Table(table, "masterdata");
                var columns = string.Join(", ", entity.GetProperties().Select(x => x.GetColumnName(store)!)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Order(StringComparer.Ordinal).Select(x => '"' + x.Replace("\"", "\"\"") + '"'));

                var sourceSchema = "masterdata";
                var checkSql = $"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'masterdata' AND table_name = '{table}')";
                await using var checkCmd = new NpgsqlCommand(checkSql, source);
                var exists = (bool)(await checkCmd.ExecuteScalarAsync(ct) ?? false);
                if (!exists && table == "customers") sourceSchema = "sales";

                var sourceRelation = $"{sourceSchema}.\"{table}\" ({columns})";
                var targetRelation = $"masterdata.\"{table}\" ({columns})";

                if (current.FirstOrDefault(x => x.Table == table)?.Rows > 0)
                {
                    try
                    {
                        await Execute(target, $"TRUNCATE TABLE masterdata.\"{table}\" CASCADE", ct);
                    }
                    catch
                    {
                    }
                }

                try
                {
                    await using var read = await source.BeginRawBinaryCopyAsync($"COPY {sourceRelation} TO STDOUT (FORMAT BINARY)", ct);
                    await using var write = await target.BeginRawBinaryCopyAsync($"COPY {targetRelation} FROM STDIN (FORMAT BINARY)", ct);
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
        return new MasterDataTransferResult(sourceSettings.Database, targetSettings.Database, matched, verified);
    }

    private static async Task<IReadOnlyList<MasterDataTableState>> State(NpgsqlConnection conn, CancellationToken ct)
    {
        var list = new List<MasterDataTableState>();
        foreach (var table in Tables)
        {
            var actualSchema = "masterdata";
            var checkSql = $"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'masterdata' AND table_name = '{table}')";
            await using var checkCmd = new NpgsqlCommand(checkSql, conn);
            var exists = (bool)(await checkCmd.ExecuteScalarAsync(ct) ?? false);
            if (!exists && table == "customers") actualSchema = "sales";

            var sql = $"SELECT count(*)::bigint, coalesce(md5(string_agg(t::text, '' ORDER BY t.*)), 'empty') FROM (SELECT * FROM {actualSchema}.\"{table}\") t";
            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    list.Add(new MasterDataTableState(table, reader.GetInt64(0), reader.GetString(1)));
                }
            }
            catch
            {
                list.Add(new MasterDataTableState(table, 0, "none"));
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
