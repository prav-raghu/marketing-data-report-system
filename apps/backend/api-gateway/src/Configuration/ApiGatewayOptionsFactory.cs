namespace ApiGateway.Configuration;

public static class ApiGatewayOptionsFactory
{
    public static ApiGatewayOptions Load(IConfiguration configuration)
    {
        var options = new ApiGatewayOptions
        {
            NodeEnv = configuration["NODE_ENV"] ?? "development",
            Port = int.TryParse(configuration["PORT"], out var port) ? port : 0,
            CorsOrigin = configuration["CORS_ORIGIN"] ?? string.Empty,
            CustomerApiUrl = configuration["CUSTOMER_API_URL"] ?? string.Empty,
            AdminApiUrl = configuration["ADMIN_API_URL"] ?? string.Empty,
            SchedulerApiUrl = configuration["SCHEDULER_API_URL"] ?? string.Empty,
            RateLimitMax = int.TryParse(configuration["RATE_LIMIT_MAX"], out var max) ? max : 200,
            RateLimitTimeWindow = configuration["RATE_LIMIT_TIME_WINDOW"] ?? "1 minute",
            GraphQlEnabled = string.Equals(configuration["GRAPHQL_ENABLED"], "true", StringComparison.OrdinalIgnoreCase),
            GraphQlPath = configuration["GRAPHQL_PATH"] ?? "/graphql",
            GraphQlIntrospection = string.Equals(configuration["GRAPHQL_INTROSPECTION"], "true", StringComparison.OrdinalIgnoreCase),
        };

        var result = new ApiGatewayOptionsValidator().Validate(options);
        if (!result.IsValid)
        {
            throw new InvalidOperationException(
                $"Invalid environment variables: {string.Join("; ", result.Errors.Select(e => e.ErrorMessage))}");
        }

        return options;
    }
}
