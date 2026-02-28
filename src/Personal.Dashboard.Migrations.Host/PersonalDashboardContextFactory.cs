using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Personal.Dashboard.Core.Common.Storage;

namespace Personal.Dashboard.Migrations.Host;

public class PersonalDashboardContextFactory : IDesignTimeDbContextFactory<PersonalDashboardContext>
{
    public PersonalDashboardContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PersonalDashboardContext>()
            .UseNpgsql(connectionString: "server=localhost;user id=default;password=pwd", opts =>
            {
                opts.MigrationsAssembly(typeof(HostApplicationBuilderExtensions).Assembly);
            })
            .Options;
        
        return new PersonalDashboardContext(options);
    }
}