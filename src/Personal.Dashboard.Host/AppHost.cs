using Personal.Dashboard.Host;

var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddPostgres("postgres")
    .WithImage("postgres")
    .WithImageTag("18-alpine")
    .AddDatabase(PersonalDashboardServiceName.Db);

var migrations = builder
    .AddProject<Projects.Personal_Dashboard_Migrations_Host>(PersonalDashboardServiceName.Migrations)
    .WithReference(db)
    .WaitFor(db);

var api = builder.AddProject<Projects.Personal_Dashboard_Api_Host>(PersonalDashboardServiceName.Api)
    .WithReference(db)
    .WithReference(migrations)
    .WaitForCompletion(migrations)
    .WithDeveloperCertificateTrust(trust: true)
    .WithExternalHttpEndpoints();

builder.AddStandaloneBlazorWebAssemblyProject<Projects.Personal_Dashboard_Web_Host>(PersonalDashboardServiceName.Web)
    .WithReference(api)
    .WithDeveloperCertificateTrust(trust: true)
    .WithExternalHttpEndpoints();

builder.Build().Run();
