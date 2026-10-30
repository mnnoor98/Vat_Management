using Vat.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatBackend("VatEngine");
var app = builder.Build();
app.MapVatBackend();
app.Run();

public partial class Program;
