using Microsoft.EntityFrameworkCore;
using Vat.Infrastructure.Messaging;
using Vat.Messaging;
using Vat.Messaging.Http;
using Vat.Services.Integration.Infrastructure;
using Vat.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatHttp("Integration");
builder.Services.AddIntegrationPersistence(builder.Configuration);
builder.Services.AddIntegrationOperators();
builder.Services.AddScoped<IBackendReadiness, IntegrationReadiness>();
builder.Services.AddOptions<MessagingSettings>().BindConfiguration("Messaging")
    .Validate(settings => settings.IsValid(), "Messaging settings are invalid.")
    .Validate(settings => !settings.Enabled ||
        ((builder.Configuration["OutboxSource:Mode"] ?? "Remote") == "Remote" &&
         (builder.Configuration["InboxTarget:Mode"] ?? "Remote") == "Remote" &&
         !string.Equals(builder.Configuration["OutboxSource:ServiceKey"], builder.Configuration["InboxTarget:ServiceKey"], StringComparison.OrdinalIgnoreCase)),
        "Independent Integration requires remote outbox and inbox owners with distinct service keys; Local mode is no longer supported.")
    .ValidateOnStart();
builder.Services.AddRemoteOutboxSource(builder.Configuration, whenMessagingEnabledOnly: true);
builder.Services.AddRemoteInboxTarget(builder.Configuration, whenMessagingEnabledOnly: true);
builder.Services.AddSingleton<RabbitMqTransport>();
builder.Services.AddSingleton<IEventPublisher>(services => services.GetRequiredService<RabbitMqTransport>());
builder.Services.AddScoped<OutboxDispatcher>();
builder.Services.AddScoped<InboxDeliveryHandler>();
builder.Services.AddHostedService<MessagingWorker>();
var app = builder.Build();
app.MapVatHttp();
app.Run();

public partial class Program;

public sealed class IntegrationReadiness(IntegrationDbContext db) : IBackendReadiness
{
    public async Task<bool> IsReady(CancellationToken ct) => !(await db.Database.GetPendingMigrationsAsync(ct)).Any();
}
