using FluentValidation;
using MediatR;

namespace Personal.Dashboard.Core.Common.Validation;

public class CqrsValidationPipelineBehavior<TRequest, TResult>(IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResult>
{
    public async Task<TResult> Handle(TRequest request, RequestHandlerDelegate<TResult> next, CancellationToken cancellationToken)
    {
        var tasks = validators.Select(v => v.ValidateAsync(request, cancellationToken));
        var results = await Task.WhenAll(tasks);
        var errors = results.SelectMany(r => r.Errors).ToArray();
        if (errors.Any())
            throw new ValidationException(errors);
        return await next(cancellationToken);
    }
}