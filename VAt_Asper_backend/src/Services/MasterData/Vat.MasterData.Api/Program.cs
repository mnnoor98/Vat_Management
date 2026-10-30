using Microsoft.EntityFrameworkCore;
using Vat.Services.MasterData.Infrastructure;
using Vat.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatHttp("MasterData");
builder.Services.AddMasterDataPersistence(builder.Configuration);
builder.Services.AddScoped<IBackendReadiness, MasterDataReadiness>();

var app = builder.Build();
app.MapVatHttp();
await app.ApplyMasterDataMigrationsAsync();
app.Run();

public partial class Program;

public sealed class MasterDataReadiness(MasterDataDbContext db) : IBackendReadiness
{
    public async Task<bool> IsReady(CancellationToken ct) => !(await db.Database.GetPendingMigrationsAsync(ct)).Any();
}
