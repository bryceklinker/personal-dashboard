using System.Collections.Immutable;
using System.Reflection;
using Personal.Dashboard.Core.Common.Apis.FootballApi;

namespace Personal.Dashboard.Core;

public record PersonalDashboardCoreOptions
{
    public ImmutableArray<Assembly> Assemblies { get; private set; }

    public Action<FootballApiClientSettings>? ConfigureFootballApi { get; set; }

    public PersonalDashboardCoreOptions(
        params Assembly[] assemblies
    )
    {
        Assemblies = [..assemblies];
    }
    
    public void AddAssembly(Assembly assembly)
    {
        Assemblies = Assemblies.Add(assembly);
    }
}