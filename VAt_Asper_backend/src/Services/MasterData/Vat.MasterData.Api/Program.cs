using Vat.Services.MasterData.Infrastructure;
using Vat.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatHttp("MasterData");
builder.Services.AddMasterDataPersistence(builder.Configuration);

var app = builder.Build();
app.MapVatHttp();
await app.ApplyMasterDataMigrationsAsync();
app.Run();

public partial class Program;
