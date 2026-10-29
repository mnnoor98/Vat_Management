using Vat.Hosting;
using Vat.Services.Integration.Infrastructure;
using Vat.Services.Manufacturing.Infrastructure;
using Vat.Services.Compliance.Infrastructure;
using Vat.Services.Workflow.Infrastructure;
using Vat.Services.Identity.Infrastructure;
using Vat.Services.Company.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatBackend("Compatibility");
builder.Services.AddIdentityPersistence(builder.Configuration);
builder.Services.AddWorkflowPersistence(builder.Configuration);
builder.Services.AddCompliancePersistence(builder.Configuration);
builder.Services.AddManufacturingPersistence(builder.Configuration);
builder.Services.AddIntegrationPersistence(builder.Configuration);
builder.Services.AddCompanyPersistence(builder.Configuration);
var app = builder.Build();
app.MapVatBackend();
if (builder.Configuration.GetValue<bool>("OperatorServer:Enabled")) app.MapMessageOperatorV1();
if (builder.Configuration.GetValue<bool>("ReportingSourceServer:Enabled")) app.MapReportingSourceV1();
app.Run();

public partial class Program;
