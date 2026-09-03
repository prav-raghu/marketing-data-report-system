namespace CustomerApi.Configuration;

public static class CustomerApiOptionsFactory
{
    public static CustomerApiOptions Load(IConfiguration configuration)
    {
        var options = new CustomerApiOptions
        {
            JwtSecret = configuration["JWT_SECRET"] ?? string.Empty,
            JwtRefreshSecret = configuration["JWT_REFRESH_SECRET"] ?? string.Empty,
            RedisUrl = configuration["REDIS_URL"] ?? string.Empty,
            RefreshTokenExpiry = configuration["REFRESH_TOKEN_EXPIRY"] ?? string.Empty,
            AuthTokenExpiry = configuration["AUTH_TOKEN_EXPIRY"] ?? string.Empty,
            CorsOrigin = configuration["CORS_ORIGIN"] ?? string.Empty,
            RateLimitWindow = configuration["RATE_LIMIT_WINDOW"] ?? string.Empty,
            RateLimitMax = int.TryParse(configuration["RATE_LIMIT_MAX"], out var max) ? max : 0,
            Port = int.TryParse(configuration["PORT"], out var port) ? port : 0,
            NodeEnv = configuration["NODE_ENV"] ?? "development",
            MailtrapApiKey = configuration["MAILTRAP_API_KEY"] ?? string.Empty,
            MailtrapFrom = configuration["MAILTRAP_FROM"] ?? string.Empty,
            MailtrapFromName = configuration["MAILTRAP_FROM_NAME"] ?? string.Empty,
            CustomerWebUrl = configuration["CUSTOMER_WEB_URL"] ?? string.Empty,
            AccountBanThreshold = int.TryParse(configuration["ACCOUNT_BAN_THRESHOLD"], out var threshold) ? threshold : null,
        };

        var result = new CustomerApiOptionsValidator().Validate(options);
        if (!result.IsValid)
        {
            throw new InvalidOperationException(
                $"Invalid environment variables: {string.Join("; ", result.Errors.Select(e => e.ErrorMessage))}");
        }

        return options;
    }
}
