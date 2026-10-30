using Vat.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddVatBackend("Company");
var app = builder.Build();
app.MapVatBackend();
app.Run();

public partial class Program;
