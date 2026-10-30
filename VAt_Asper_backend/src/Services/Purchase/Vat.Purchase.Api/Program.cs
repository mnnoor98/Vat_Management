using Microsoft.EntityFrameworkCore;
using Vat.Services.Purchase.Infrastructure;
using Vat.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatHttp("Purchase");
builder.Services.AddPurchasePersistence(builder.Configuration);
builder.Services.AddScoped<IBackendReadiness, PurchaseReadiness>();

var app = builder.Build();
app.MapVatHttp();
await app.ApplyPurchaseMigrationsAsync();
app.Run();

public partial class Program;

public sealed class PurchaseReadiness(PurchaseDbContext db) : IBackendReadiness
{
    public async Task<bool> IsReady(CancellationToken ct) => !(await db.Database.GetPendingMigrationsAsync(ct)).Any();
}
