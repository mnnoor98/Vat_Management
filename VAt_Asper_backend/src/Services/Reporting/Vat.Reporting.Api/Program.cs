using Vat.Hosting;
using Vat.Messaging.Http;
using Vat.Services.Reporting.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatBackend("Reporting");
builder.Services.AddReportingPersistence(builder.Configuration);
var inboxEnabled = builder.Configuration.GetValue<bool>("InboxServer:Enabled");
if (inboxEnabled) builder.Services.AddInboxHttpServer(builder.Configuration);
var app = builder.Build();
app.MapVatBackend();
await app.ApplyReportingMigrationsAsync();
if (inboxEnabled) app.MapInboxHttpServer();
app.Run();

public partial class Program;
