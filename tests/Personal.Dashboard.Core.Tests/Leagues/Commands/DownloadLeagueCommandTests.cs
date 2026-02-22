using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Common;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Core.Leagues.Commands;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Core.Tests.Support;
using Personal.Dashboard.Test.Support.Common.Http;

namespace Personal.Dashboard.Core.Tests.Leagues.Commands;

public class DownloadLeagueCommandTests
{
    private const string BaseUrl = "https://football.api.com";
    private readonly FakeHttpMessageHandler _handler;
    private readonly PersonalDashboardContext _context;
    private readonly ICqrsBus _cqrsBus;

    public DownloadLeagueCommandTests()
    {
        var provider = PersonalDashboardCoreTestingProviderFactory.Create(opts =>
        {
            opts.ConfigureFootballApi = api =>
            {
                api.BaseUrl = BaseUrl;
            };
        });
        
        _context = provider.GetRequiredService<PersonalDashboardContext>();
        _handler = provider.GetRequiredService<FakeHttpMessageHandler>();
        _cqrsBus = provider.GetRequiredService<ICqrsBus>();
    }

    [Fact]
    public async Task WhenLeagueIsDownloadedThenSavesLeagueToDatabase()
    {
        var league = FootballApiDataFactory.League();
        await _handler.SetupGetLeagues(BaseUrl, [league]);

        await _cqrsBus.ExecuteAsync(new DownloadLeagueCommand(league.League.Name));

        var dbLeagues = await _context.Set<FootballLeagueEntity>().ToArrayAsync();
        Assert.Single(dbLeagues);
        Assert.Equal(league.League.Name, dbLeagues[0].Name);
    }
 
    [Fact]
    public async Task WhenLeagueIsDownloadedThenSavesLeagueAliasToDatabase()
    {
        var league = FootballApiDataFactory.League();
        await _handler.SetupGetLeagues(BaseUrl, [league]);

        await _cqrsBus.ExecuteAsync(new DownloadLeagueCommand(league.League.Name));
        
        var aliases = await _context.Set<FootballLeagueAlias>().ToArrayAsync();
        Assert.Single(aliases);
        Assert.Equal($"{league.League.Id}", aliases[0].Alias);
        Assert.Equal(DataSource.FootballApi, aliases[0].AliasSource);
    }

    [Fact]
    public async Task WhenMoreThanOneLeagueReturnedThenSavesAllLeaguesToDatabase()
    {
        var first = FootballApiDataFactory.League();
        var second = FootballApiDataFactory.League();
        var third = FootballApiDataFactory.League();
        await _handler.SetupGetLeagues(BaseUrl, [first, second, third]);
        
        await _cqrsBus.ExecuteAsync(new DownloadLeagueCommand("IDK"));
        
        var leagues = await _context.Set<FootballLeagueEntity>().ToArrayAsync();
        Assert.Equal(3, leagues.Length);
    }
}