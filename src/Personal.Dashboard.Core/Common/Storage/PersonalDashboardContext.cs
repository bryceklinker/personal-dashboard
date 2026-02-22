using Microsoft.EntityFrameworkCore;

namespace Personal.Dashboard.Core.Common.Storage;

public class PersonalDashboardContext(DbContextOptions<PersonalDashboardContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PersonalDashboardContext).Assembly);
    }
};