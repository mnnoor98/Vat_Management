using Vat.Hosting;
using Vat.Services.Company.Infrastructure;
using Vat.Services.Compliance.Infrastructure;
using Vat.Services.Identity.Infrastructure;
using Vat.Services.Integration.Infrastructure;
using Vat.Services.Inventory.Infrastructure;
using Vat.Services.Manufacturing.Infrastructure;
using Vat.Services.MasterData.Infrastructure;
using Vat.Services.Purchase.Infrastructure;
using Vat.Services.Sales.Infrastructure;
using Vat.Services.VatEngine.Infrastructure;
using Vat.Services.Workflow.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatBackend("Compatibility");

// Register persistence & business services for all isolated microservice domains
builder.Services.AddIdentityPersistence(builder.Configuration);
builder.Services.AddCompanyPersistence(builder.Configuration);
builder.Services.AddMasterDataPersistence(builder.Configuration);
builder.Services.AddPurchasePersistence(builder.Configuration);
builder.Services.AddSalesPersistence(builder.Configuration);
builder.Services.AddInventoryPersistence(builder.Configuration);
builder.Services.AddManufacturingPersistence(builder.Configuration);
builder.Services.AddVatEnginePersistence(builder.Configuration);
builder.Services.AddCompliancePersistence(builder.Configuration);
builder.Services.AddWorkflowPersistence(builder.Configuration);
builder.Services.AddIntegrationPersistence(builder.Configuration);

var app = builder.Build();
app.MapVatBackend();
if (builder.Configuration.GetValue<bool>("OperatorServer:Enabled")) app.MapMessageOperatorV1();
if (builder.Configuration.GetValue<bool>("ReportingSourceServer:Enabled")) app.MapReportingSourceV1();
app.Run();

public partial class Program;
