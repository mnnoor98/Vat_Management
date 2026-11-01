using Microsoft.EntityFrameworkCore;
using Vat.Services.Manufacturing.Infrastructure;
using Vat.Web;
using Vat.Infrastructure.Messaging;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatHttp("Manufacturing");
builder.Services.AddManufacturingPersistence(builder.Configuration);
builder.Services.AddScoped<IBackendReadiness, ManufacturingReadiness>();
builder.Services.AddOptions<MessagingSettings>().BindConfiguration("Messaging")
    .Validate(x=>x.IsValid(),"Manufacturing messaging settings are invalid.").ValidateOnStart();
builder.Services.AddSingleton<RabbitMqTransport>();
builder.Services.AddHostedService<ManufacturingExecutionWorker>();
var app = builder.Build();
app.MapVatHttp();
app.Run();

public partial class Program;

public sealed class ManufacturingReadiness(ManufacturingDbContext db) : IBackendReadiness
{
    public async Task<bool> IsReady(CancellationToken ct) => !(await db.Database.GetPendingMigrationsAsync(ct)).Any();
}
