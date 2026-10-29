using Microsoft.EntityFrameworkCore;
using Vat.Services.Identity.Infrastructure;
using Vat.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatHttp("Identity");
builder.Services.AddIdentityPersistence(builder.Configuration);
builder.Services.AddScoped<IBackendReadiness, IdentityReadiness>();
var app = builder.Build();
app.MapVatHttp();
app.Run();

public partial class Program;

public sealed class IdentityReadiness(IdentityDbContext db) : IBackendReadiness
{
    public async Task<bool> IsReady(CancellationToken ct) => !(await db.Database.GetPendingMigrationsAsync(ct)).Any();
}
