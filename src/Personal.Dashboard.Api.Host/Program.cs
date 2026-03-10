using Personal.Dashboard.Api.Host;
using Personal.Dashboard.Api.Host.Common.SignalR;

var builder = WebApplication.CreateBuilder(args)
    .AddPersonalDashboardApi();

var app = builder.Build();
app.UseCors();
app.MapDefaultEndpoints();
app.MapControllers();
app.MapHub<EventsHub>("/hubs/events");
app.Run();

public partial class Program;