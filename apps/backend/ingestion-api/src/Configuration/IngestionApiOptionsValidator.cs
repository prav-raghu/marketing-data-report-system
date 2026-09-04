using FluentValidation;

namespace IngestionApi.Configuration;

public sealed class IngestionApiOptionsValidator : AbstractValidator<IngestionApiOptions>
{
    public IngestionApiOptionsValidator()
    {
        RuleFor(x => x.IngestionApiKey).NotEmpty().MinimumLength(32);
        RuleFor(x => x.RedisUrl).NotEmpty();
        RuleFor(x => x.CorsOrigin).NotEmpty();
        RuleFor(x => x.RateLimitWindow).NotEmpty();
        RuleFor(x => x.RateLimitMax).GreaterThan(0);
        RuleFor(x => x.Port).GreaterThan(0);
        RuleFor(x => x.NodeEnv).Must(value => value is "development" or "production")
            .WithMessage("NODE_ENV must be 'development' or 'production'");
        RuleFor(x => x.RawZoneConnectionString).NotEmpty();
        RuleFor(x => x.RawZoneContainer).NotEmpty();
        RuleFor(x => x.ReportingTimezone).NotEmpty();
        RuleFor(x => x.ReportingCurrency).NotEmpty().Length(3);
        RuleFor(x => x.MaxConcurrentExtractions).InclusiveBetween(1, 200);
    }
}
