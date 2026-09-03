using FluentValidation;

namespace WorkflowApi.Configuration;

public sealed class WorkflowApiOptionsValidator : AbstractValidator<WorkflowApiOptions>
{
    public WorkflowApiOptionsValidator()
    {
        RuleFor(x => x.DatabaseUrl).NotEmpty();
        RuleFor(x => x.Port).GreaterThan(0);
        RuleFor(x => x.NodeEnv).Must(value => value is "development" or "production");
        RuleFor(x => x.CorsOrigin).NotEmpty();
    }
}
