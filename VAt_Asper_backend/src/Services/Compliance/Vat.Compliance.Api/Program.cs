using Microsoft.EntityFrameworkCore;
using Vat.Services.Compliance.Infrastructure;
using Vat.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatHttp("Compliance");
builder.Services.AddCompliancePersistence(builder.Configuration);
builder.Services.AddScoped<IBackendReadiness, ComplianceReadiness>();
var app = builder.Build();
app.MapVatHttp();
app.Run();

public partial class Program;

public sealed class ComplianceReadiness(ComplianceDbContext db) : IBackendReadiness
{
    public async Task<bool> IsReady(CancellationToken ct) => !(await db.Database.GetPendingMigrationsAsync(ct)).Any();
}
