using FluentValidation;

namespace ApiGateway.Configuration;

public sealed class ApiGatewayOptionsValidator : AbstractValidator<ApiGatewayOptions>
{
    public ApiGatewayOptionsValidator()
    {
        RuleFor(x => x.NodeEnv).Must(value => value is "development" or "test" or "production");
        RuleFor(x => x.Port).GreaterThan(0);
        RuleFor(x => x.CorsOrigin).NotEmpty();
        RuleFor(x => x.CustomerApiUrl).NotEmpty();
        RuleFor(x => x.AdminApiUrl).NotEmpty();
        RuleFor(x => x.SchedulerApiUrl).NotEmpty();
        RuleFor(x => x.RateLimitMax).GreaterThan(0);
        RuleFor(x => x.RateLimitTimeWindow).NotEmpty();
    }
}
