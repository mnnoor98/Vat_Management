using Vat.Services.Inventory.Infrastructure;
using Vat.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatHttp("Inventory");
builder.Services.AddInventoryPersistence(builder.Configuration);

var app = builder.Build();
app.MapVatHttp();
await app.ApplyInventoryMigrationsAsync();
app.Run();

public partial class Program;
