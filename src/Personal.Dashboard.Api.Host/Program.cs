using Personal.Dashboard.Api.Host;

var builder = WebApplication.CreateBuilder(args)
    .AddPersonalDashboardApi();

var app = builder.Build();
app.UseCors();
app.MapDefaultEndpoints();
app.MapControllers();
app.Run();

public partial class Program;