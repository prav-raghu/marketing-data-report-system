using FluentValidation;

namespace ScheduleApi.Configuration;

public sealed class ScheduleApiOptionsValidator : AbstractValidator<ScheduleApiOptions>
{
    public ScheduleApiOptionsValidator()
    {
        RuleFor(x => x.ScheduleApiKey).NotEmpty().MinimumLength(32);
        RuleFor(x => x.RedisUrl).NotEmpty();
        RuleFor(x => x.RefreshTokenExpiry).NotEmpty();
        RuleFor(x => x.AuthTokenExpiry).NotEmpty();
        RuleFor(x => x.CorsOrigin).NotEmpty();
        RuleFor(x => x.RateLimitWindow).NotEmpty();
        RuleFor(x => x.RateLimitMax).GreaterThan(0);
        RuleFor(x => x.Port).GreaterThan(0);
        RuleFor(x => x.NodeEnv).Must(value => value is "development" or "production")
            .WithMessage("NODE_ENV must be 'development' or 'production'");
    }
}
