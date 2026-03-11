using Microsoft.AspNetCore.Mvc;
using Personal.Dashboard.Api.Host.Common;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Leagues.Commands;
using Personal.Dashboard.Core.Leagues.Queries;

namespace Personal.Dashboard.Api.Host.Leagues;

[ApiController]
[Route("[controller]")]
public class LeaguesController(ICqrsBus cqrsBus) : CqrsController(cqrsBus)
{
    [HttpGet]
    public async Task<IActionResult> GetLeagues()
    {
        return await QueryAsync(new GetLeaguesQuery());
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshLeagues()
    {
        return await ExecuteAsync(new RefreshLeaguesCommand());
    }

    [HttpPost("{id:guid}/favorite")]
    public async Task<IActionResult> FavoriteLeague(Guid id)
    {
        return await ExecuteAsync(new FavoriteLeagueCommand(id), statusCode: 204);
    }

    [HttpPost("{id:guid}/unfavorite")]
    public async Task<IActionResult> UnfavoriteLeague(Guid id)
    {
        return await ExecuteAsync(new UnfavoriteLeagueCommand(id), statusCode: 204);
    }
}