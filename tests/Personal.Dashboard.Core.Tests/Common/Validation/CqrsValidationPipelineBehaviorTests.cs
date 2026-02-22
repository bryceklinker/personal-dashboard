using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core.Common.Cqrs;
using Personal.Dashboard.Core.Common.Cqrs.Commands;
using Personal.Dashboard.Core.Tests.Support;

namespace Personal.Dashboard.Core.Tests.Common.Validation;

public class CqrsValidationPipelineBehaviorTests
{
    private readonly ICqrsBus _cqrsBus;
    
    public CqrsValidationPipelineBehaviorTests()
    {
        var provider = PersonalDashboardCoreTestingProviderFactory.Create(opts =>
        {
            opts.AddAssembly(typeof(CqrsValidationPipelineBehaviorTests).Assembly);
        });
        
        _cqrsBus = provider.GetRequiredService<ICqrsBus>();
    }

    [Fact]
    public async Task WhenCommandHasValidatorsThenCommandIsValidated()
    {
        var command = new ValidatedCommand("");
        
        await Assert.ThrowsAsync<ValidationException>(() => _cqrsBus.ExecuteAsync(command));
    }
}


public record ValidatedCommand(string Name) : ICommand;

public class ValidatedCommandValidator : AbstractValidator<ValidatedCommand>
{
    public ValidatedCommandValidator()
    {
        RuleFor(c => c.Name).NotNull().NotEmpty().MaximumLength(5);
    }
}

public class ValidatedCommandHandler : IRequestHandler<ValidatedCommand>
{
    public Task Handle(ValidatedCommand request, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}