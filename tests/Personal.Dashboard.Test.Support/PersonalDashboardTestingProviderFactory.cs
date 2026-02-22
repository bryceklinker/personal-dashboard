using Microsoft.Extensions.DependencyInjection;

namespace Personal.Dashboard.Test.Support;

public static class PersonalDashboardTestingProviderFactory
{
    public static IServiceProvider CreateProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        services.AddPersonalDashboardTestingServices();
        return services.BuildServiceProvider();
    }
}