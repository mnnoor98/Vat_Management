using Vat.Hosting;
using Vat.Messaging.Http;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatBackend("Reporting");
var inboxEnabled = builder.Configuration.GetValue<bool>("InboxServer:Enabled");
if (inboxEnabled) builder.Services.AddInboxHttpServer(builder.Configuration);
var app = builder.Build();
app.MapVatBackend();
if (inboxEnabled) app.MapInboxHttpServer();
app.Run();

public partial class Program;
