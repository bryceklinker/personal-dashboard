using Microsoft.EntityFrameworkCore;
using Personal.Dashboard.Test.Support;

namespace Personal.Dashboard.Core.Tests.Support;

public static class PersonalDashboardCoreTestingProviderFactory
{
    public static IServiceProvider Create(Action<PersonalDashboardCoreOptions>? configure = null)
    {
        return PersonalDashboardTestingProviderFactory.CreateProvider(services =>
        {
            services.AddPersonalDashboardCore(opts =>
            {
                opts.ConfigureDbContext = db =>
                {
                    db.UseInMemoryDatabase($"{Guid.NewGuid():N}");
                };
                
                configure?.Invoke(opts);
            });
        });
    }
}