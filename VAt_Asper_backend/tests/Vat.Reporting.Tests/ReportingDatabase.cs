using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Vat.Services.Reporting.Infrastructure;
using Xunit;

namespace Vat.Reporting.Tests;

public sealed class ReportingDatabase : IAsyncLifetime
{
    private readonly string _database = "reporting_test_" + Guid.NewGuid().ToString("N");
    private string _admin = "";
    private string _connection = "";
    public ReportingDbContext Open(SaveChangesInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<ReportingDbContext>(); ReportingPersistence.Configure(options, _connection);
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new(options.Options);
    }
    public async Task InitializeAsync()
    {
        _admin = Environment.GetEnvironmentVariable("VAT_TEST_POSTGRES") ?? throw new InvalidOperationException("Use scripts/Test-Local.ps1 for disposable PostgreSQL.");
        _connection = new NpgsqlConnectionStringBuilder(_admin) { Database = _database, Pooling = false }.ConnectionString;
        await using var admin = new NpgsqlConnection(_admin); await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", admin); await command.ExecuteNonQueryAsync();
        await using var db = Open(); await db.Database.MigrateAsync();
    }
    public async Task DisposeAsync()
    {
        if (_admin.Length == 0) return;
        await using var admin = new NpgsqlConnection(_admin); await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\"", admin) { CommandTimeout = 120 }; await command.ExecuteNonQueryAsync();
    }
}
