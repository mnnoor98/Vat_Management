using Microsoft.EntityFrameworkCore;
using Vat.Services.Workflow.Infrastructure;
using Vat.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatHttp("Workflow");
builder.Services.AddWorkflowPersistence(builder.Configuration);
builder.Services.AddScoped<IBackendReadiness, WorkflowReadiness>();
var app = builder.Build();
app.MapVatHttp();
app.Run();

public partial class Program;

public sealed class WorkflowReadiness(WorkflowDbContext db) : IBackendReadiness
{
    public async Task<bool> IsReady(CancellationToken ct) => !(await db.Database.GetPendingMigrationsAsync(ct)).Any();
}
