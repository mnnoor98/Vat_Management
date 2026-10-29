using Microsoft.EntityFrameworkCore;
using Vat.Services.Company.Infrastructure;
using Vat.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatHttp("Company");
builder.Services.AddCompanyPersistence(builder.Configuration);
builder.Services.AddScoped<IBackendReadiness, CompanyReadiness>();
var app = builder.Build();
app.MapVatHttp();
app.Run();

public partial class Program;

public sealed class CompanyReadiness(CompanyDbContext db) : IBackendReadiness
{
    public async Task<bool> IsReady(CancellationToken ct) => !(await db.Database.GetPendingMigrationsAsync(ct)).Any();
}
