using Personal.Dashboard.Core.Common.Storage;

namespace Personal.Dashboard.Api.Host;

public static class PersonalDashboardApiWebApplicationBuilderExtensions
{
    public static WebApplicationBuilder AddPersonalDashboardApi(this WebApplicationBuilder builder)
    {
        builder.AddServiceDefaults();
        builder.Services.AddPersonalDashboardApi(builder.Configuration);
        builder.EnrichNpgsqlDbContext<PersonalDashboardContext>();
        return builder;
    }
}