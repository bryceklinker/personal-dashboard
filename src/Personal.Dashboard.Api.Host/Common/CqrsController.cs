using Microsoft.AspNetCore.Mvc;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Common.Cqrs.Queries;
using Personal.Dashboard.Core.Common.Exceptions;

namespace Personal.Dashboard.Api.Host.Common;

public abstract class CqrsController(ICqrsBus cqrsBus) : ControllerBase
{
    protected async Task<IActionResult> QueryAsync<TResult>(IQuery<TResult> query, int statusCode = 200)
    {
        try
        {
            var result = await cqrsBus.QueryAsync(query).ConfigureAwait(false);
            return StatusCode(statusCode, result);
        }
        catch (EntityNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    protected async Task<IActionResult> ExecuteAsync<TCommand>(TCommand command, int statusCode = 200)
        where TCommand : ICommand
    {
        try
        {
            await cqrsBus.ExecuteAsync(command).ConfigureAwait(false);
            return StatusCode(statusCode, command);
        }
        catch (EntityNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    protected async Task<IActionResult> ExecuteAsync<TResult>(ICommand<TResult> query, int statusCode = 200)
    {
        try
        {
            var result = await cqrsBus.ExecuteAsync(query).ConfigureAwait(false);
            return StatusCode(statusCode, result);
        }
        catch (EntityNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
