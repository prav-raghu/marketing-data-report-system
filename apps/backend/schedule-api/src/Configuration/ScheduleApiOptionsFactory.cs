namespace ScheduleApi.Configuration;

public static class ScheduleApiOptionsFactory
{
    public static ScheduleApiOptions Load(IConfiguration configuration)
    {
        var options = new ScheduleApiOptions
        {
            ScheduleApiKey = configuration["SCHEDULE_API_KEY"] ?? string.Empty,
            JwtSecret = configuration["JWT_SECRET"],
            RedisUrl = configuration["REDIS_URL"] ?? string.Empty,
            RefreshTokenExpiry = configuration["REFRESH_TOKEN_EXPIRY"] ?? string.Empty,
            AuthTokenExpiry = configuration["AUTH_TOKEN_EXPIRY"] ?? string.Empty,
            CorsOrigin = configuration["CORS_ORIGIN"] ?? string.Empty,
            RateLimitWindow = configuration["RATE_LIMIT_WINDOW"] ?? string.Empty,
            RateLimitMax = int.TryParse(configuration["RATE_LIMIT_MAX"], out var rateLimitMax) ? rateLimitMax : 0,
            Port = int.TryParse(configuration["PORT"], out var port) ? port : 0,
            NodeEnv = configuration["NODE_ENV"] ?? "development",
            AccountBanThreshold = int.TryParse(configuration["ACCOUNT_BAN_THRESHOLD"], out var threshold) ? threshold : null,
        };

        var result = new ScheduleApiOptionsValidator().Validate(options);
        if (!result.IsValid)
        {
            throw new InvalidOperationException(
                $"Invalid environment variables: {string.Join("; ", result.Errors.Select(e => e.ErrorMessage))}");
        }

        return options;
    }
}
