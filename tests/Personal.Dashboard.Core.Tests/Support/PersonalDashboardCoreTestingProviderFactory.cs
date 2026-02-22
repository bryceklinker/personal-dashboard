using Personal.Dashboard.Test.Support;

namespace Personal.Dashboard.Core.Tests.Support;

public static class PersonalDashboardCoreTestingProviderFactory
{
    public static IServiceProvider Create(Action<PersonalDashboardCoreOptions>? configure = null)
    {
        var configureOptions = configure ?? (_ => {});
        return PersonalDashboardTestingProviderFactory.CreateProvider(services =>
        {
            services.AddPersonalDashboardCore(configureOptions);
        });
    }
}