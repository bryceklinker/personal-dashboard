using Microsoft.AspNetCore.Mvc;
using Personal.Dashboard.Api.Host.Common;
using Personal.Dashboard.Core.Common.Cqrs;
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
}