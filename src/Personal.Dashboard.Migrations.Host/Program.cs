using Personal.Dashboard.Migrations.Host;

var builder = Host.CreateApplicationBuilder(args);
builder.AddPersonalDashboardMigrations(builder.Configuration);

builder.Services.AddHostedService<MigrationsWorker>();

var host = builder.Build();
host.Run();
