using Vat.Services.VatEngine.Infrastructure;
using Vat.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatHttp("VatEngine");
builder.Services.AddVatEnginePersistence(builder.Configuration);

var app = builder.Build();
app.MapVatHttp();
await app.ApplyVatEngineMigrationsAsync();
app.Run();

public partial class Program;
