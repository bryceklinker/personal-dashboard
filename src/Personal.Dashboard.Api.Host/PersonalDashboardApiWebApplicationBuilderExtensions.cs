using Personal.Dashboard.Core.Common.Storage;

namespace Personal.Dashboard.Api.Host;

public static class PersonalDashboardApiWebApplicationBuilderExtensions
{
    public static WebApplicationBuilder AddPersonalDashboardApi(this WebApplicationBuilder builder)
    {
        builder.AddServiceDefaults();
        
        
        builder.Services.AddPersonalDashboardApi(builder.Configuration);
        if (builder.Configuration.HasDbConnectionString()) builder.EnrichNpgsqlDbContext<PersonalDashboardContext>();
        return builder;
    }
}