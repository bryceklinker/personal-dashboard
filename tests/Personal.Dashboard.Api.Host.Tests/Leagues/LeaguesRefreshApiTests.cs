using System.Net;
using Microsoft.AspNetCore.SignalR.Client;
using Personal.Dashboard.Api.Host.Tests.Support;
using Personal.Dashboard.Core.Tests.Support;
using Personal.Dashboard.Models;
using Personal.Dashboard.Test.Support;

namespace Personal.Dashboard.Api.Host.Tests.Leagues;

public class LeaguesRefreshApiTests(PersonalDashboardApiApplication app)
    : IClassFixture<PersonalDashboardApiApplication>
{
    private readonly HttpClient _client = app.CreateClient();

    [Fact]
    public async Task WhenRefreshingLeaguesThenReturnsSuccess()
    {
        await app.HttpHandler.SetupGetLeagues(
            PersonalDashboardApiApplication.FootballApiBaseUrl,
            [FootballApiDataFactory.League()]
        );

        var response = await _client.PostAsync("/leagues/refresh", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WhenRefreshingLeaguesThenSendsLeaguesRefreshedEventViaSignalR()
    {
        await app.HttpHandler.SetupGetLeagues(
            PersonalDashboardApiApplication.FootballApiBaseUrl,
            [FootballApiDataFactory.League()]
        );

        DashboardEvent? receivedEvent = null;
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(app.Server.BaseAddress, "hubs/events"), opts =>
            {
                opts.HttpMessageHandlerFactory = _ => app.Server.CreateHandler();
            })
            .Build();

        connection.On<DashboardEvent>("ReceiveEvent", e => receivedEvent = e);
        await connection.StartAsync();

        await _client.PostAsync("/leagues/refresh", null);

        await Eventually.Assert(() =>
        {
            Assert.NotNull(receivedEvent);
            Assert.Equal("LeaguesRefreshed", receivedEvent.Type);
        });

        await connection.StopAsync();
    }
}
