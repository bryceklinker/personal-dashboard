namespace Personal.Dashboard.Host;

public static class PersonalDashboardServiceName
{
    private static readonly string[] All = [Web, Api];

    public const string Db = "db";
    public const string Web = "web";
    public const string Api = "api";
    public const string Migrations = "migrations";


    public static void EnsureValid(string service)
    {
        if (!All.Contains(service))
            throw new ArgumentException($"The service '{service}' is not valid.");
    }
}