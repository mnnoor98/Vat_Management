using Microsoft.EntityFrameworkCore;
using Vat.Services.Sales.Infrastructure;
using Vat.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatHttp("Sales");
builder.Services.AddSalesPersistence(builder.Configuration);
builder.Services.AddScoped<IBackendReadiness, SalesReadiness>();

var app = builder.Build();
app.MapVatHttp();
await app.ApplySalesMigrationsAsync();
app.Run();

public partial class Program;

public sealed class SalesReadiness(SalesDbContext db) : IBackendReadiness
{
    public async Task<bool> IsReady(CancellationToken ct) => !(await db.Database.GetPendingMigrationsAsync(ct)).Any();
}
