using Personal.Dashboard.Host;

var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.Personal_Dashboard_Api_Host>(PersonalDashboardServiceName.Api)
    .WithDeveloperCertificateTrust(trust: true)
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.Personal_Dashboard_Web_Host>(PersonalDashboardServiceName.Web)
    .WithReference(api)
    .WithDeveloperCertificateTrust(trust: true)
    .WithExternalHttpEndpoints();

builder.Build().Run();
