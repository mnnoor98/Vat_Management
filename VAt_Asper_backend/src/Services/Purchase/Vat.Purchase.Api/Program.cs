using Vat.Services.Purchase.Infrastructure;
using Vat.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatHttp("Purchase");
builder.Services.AddPurchasePersistence(builder.Configuration);

var app = builder.Build();
app.MapVatHttp();
await app.ApplyPurchaseMigrationsAsync();
app.Run();

public partial class Program;
