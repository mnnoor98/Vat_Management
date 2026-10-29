using Vat.Hosting;
using Vat.Messaging.Http;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatBackend("FinancialOutbox");
builder.Services.AddOutboxHttpServer(builder.Configuration);
var app = builder.Build();
app.MapVatBackend();
app.MapOutboxHttpServer();
if (builder.Configuration.GetValue<bool>("OperatorServer:Enabled")) app.MapMessageOperatorV1();
if (builder.Configuration.GetValue<bool>("ReportingSourceServer:Enabled")) app.MapReportingSourceV1();
app.Run();

public partial class Program;
