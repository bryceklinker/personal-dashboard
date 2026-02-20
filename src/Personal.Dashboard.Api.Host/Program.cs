var builder = WebApplication.CreateBuilder(args)
    .AddServiceDefaults();
builder.Services.AddControllers();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapControllers();
app.Run();

public partial class Program;