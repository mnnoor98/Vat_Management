using Microsoft.EntityFrameworkCore;
using Vat.Services.VatEngine.Infrastructure;
using Vat.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatHttp("VatEngine");
builder.Services.AddVatEnginePersistence(builder.Configuration);
builder.Services.AddScoped<IBackendReadiness, VatEngineReadiness>();

var app = builder.Build();
app.MapVatHttp();
await app.ApplyVatEngineMigrationsAsync();
app.Run();

public partial class Program;

public sealed class VatEngineReadiness(VatEngineDbContext db) : IBackendReadiness
{
    public async Task<bool> IsReady(CancellationToken ct) => !(await db.Database.GetPendingMigrationsAsync(ct)).Any();
}
