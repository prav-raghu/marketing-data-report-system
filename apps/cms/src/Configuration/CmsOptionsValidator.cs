using FluentValidation;

namespace Cms.Configuration;

public sealed class CmsOptionsValidator : AbstractValidator<CmsOptions>
{
    public CmsOptionsValidator()
    {
        RuleFor(x => x.DatabaseUrl).NotEmpty();
        RuleFor(x => x.Port).GreaterThan(0);
        RuleFor(x => x.NodeEnv).Must(value => value is "development" or "production");
    }
}
