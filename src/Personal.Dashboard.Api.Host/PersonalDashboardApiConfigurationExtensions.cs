namespace Personal.Dashboard.Api.Host;

public static class PersonalDashboardApiConfigurationExtensions
{
    public static string? DbConnectionString(this IConfiguration configuration)
    {
        return  configuration.GetConnectionString("db");
    }
    
    public static bool HasDbConnectionString(this IConfiguration configuration)
    {
        return !string.IsNullOrWhiteSpace(configuration.DbConnectionString());
    }
}