using FluentValidation;

namespace CustomerApi.Configuration;

public sealed class CustomerApiOptionsValidator : AbstractValidator<CustomerApiOptions>
{
    public CustomerApiOptionsValidator()
    {
        RuleFor(x => x.JwtSecret).NotEmpty().MinimumLength(32);
        RuleFor(x => x.JwtRefreshSecret).NotEmpty().MinimumLength(32);
        RuleFor(x => x.RedisUrl).NotEmpty();
        RuleFor(x => x.RefreshTokenExpiry).NotEmpty();
        RuleFor(x => x.AuthTokenExpiry).NotEmpty();
        RuleFor(x => x.CorsOrigin).NotEmpty();
        RuleFor(x => x.RateLimitWindow).NotEmpty();
        RuleFor(x => x.RateLimitMax).GreaterThan(0);
        RuleFor(x => x.Port).GreaterThan(0);
        RuleFor(x => x.NodeEnv).Must(value => value is "development" or "production");
        RuleFor(x => x.MailtrapApiKey).NotEmpty();
        RuleFor(x => x.MailtrapFrom).NotEmpty().EmailAddress();
        RuleFor(x => x.MailtrapFromName).NotEmpty();
        RuleFor(x => x.CustomerWebUrl).NotEmpty();
    }
}
