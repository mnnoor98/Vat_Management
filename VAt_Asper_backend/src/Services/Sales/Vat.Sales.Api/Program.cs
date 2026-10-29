using Vat.Services.Sales.Infrastructure;
using Vat.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatHttp("Sales");
builder.Services.AddSalesPersistence(builder.Configuration);

var app = builder.Build();
app.MapVatHttp();
await app.ApplySalesMigrationsAsync();
app.Run();

public partial class Program;
