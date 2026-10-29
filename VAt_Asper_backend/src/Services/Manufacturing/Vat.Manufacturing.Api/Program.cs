using Microsoft.EntityFrameworkCore;
using Vat.Services.Manufacturing.Infrastructure;
using Vat.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatHttp("Manufacturing");
builder.Services.AddManufacturingPersistence(builder.Configuration);
builder.Services.AddScoped<IBackendReadiness, ManufacturingReadiness>();
var app = builder.Build();
app.MapVatHttp();
app.Run();

public partial class Program;

public sealed class ManufacturingReadiness(ManufacturingDbContext db) : IBackendReadiness
{
    public async Task<bool> IsReady(CancellationToken ct) => !(await db.Database.GetPendingMigrationsAsync(ct)).Any();
}
