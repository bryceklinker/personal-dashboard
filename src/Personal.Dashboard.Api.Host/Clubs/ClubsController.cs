using Microsoft.AspNetCore.Mvc;
using Personal.Dashboard.Api.Host.Common;
using Personal.Dashboard.Core.Clubs.Commands;
using Personal.Dashboard.Core.Clubs.Queries;
using Personal.Dashboard.Core.Common.Cqrs;

namespace Personal.Dashboard.Api.Host.Clubs;

[ApiController]
[Route("[controller]")]
public class ClubsController(ICqrsBus cqrsBus) : CqrsController(cqrsBus)
{
    [HttpGet]
    public async Task<IActionResult> GetClubs([FromQuery] int offset = 0, [FromQuery] int limit = 10)
    {
        return await QueryAsync(new GetClubsQuery(offset, limit));
    }

    [HttpPost("{id:guid}/favorite")]
    public async Task<IActionResult> FavoriteClub(Guid id)
    {
        return await ExecuteAsync(new FavoriteClubCommand(id), statusCode: 204);
    }

    [HttpPost("{id:guid}/unfavorite")]
    public async Task<IActionResult> UnfavoriteClub(Guid id)
    {
        return await ExecuteAsync(new UnfavoriteClubCommand(id), statusCode: 204);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshClubs()
    {
        return await ExecuteAsync(new RefreshAllFavoritedClubsCommand(), statusCode: 204);
    }
}
