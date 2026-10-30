using Microsoft.EntityFrameworkCore;
using Vat.Services.Inventory.Infrastructure;
using Vat.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatHttp("Inventory");
builder.Services.AddInventoryPersistence(builder.Configuration);
builder.Services.AddScoped<IBackendReadiness, InventoryReadiness>();

var app = builder.Build();
app.MapVatHttp();
await app.ApplyInventoryMigrationsAsync();
app.Run();

public partial class Program;

public sealed class InventoryReadiness(InventoryDbContext db) : IBackendReadiness
{
    public async Task<bool> IsReady(CancellationToken ct) => !(await db.Database.GetPendingMigrationsAsync(ct)).Any();
}
