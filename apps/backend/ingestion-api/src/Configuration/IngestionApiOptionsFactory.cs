namespace IngestionApi.Configuration;

public static class IngestionApiOptionsFactory
{
    public static IngestionApiOptions Load(IConfiguration configuration)
    {
        var options = new IngestionApiOptions
        {
            IngestionApiKey = configuration["INGESTION_API_KEY"] ?? string.Empty,
            RedisUrl = configuration["REDIS_URL"] ?? string.Empty,
            CorsOrigin = configuration["CORS_ORIGIN"] ?? string.Empty,
            RateLimitWindow = configuration["RATE_LIMIT_WINDOW"] ?? string.Empty,
            RateLimitMax = int.TryParse(configuration["RATE_LIMIT_MAX"], out var rateLimitMax) ? rateLimitMax : 0,
            Port = int.TryParse(configuration["PORT"], out var port) ? port : 0,
            NodeEnv = configuration["NODE_ENV"] ?? "development",
            RawZoneConnectionString = configuration["RAW_ZONE_CONNECTION_STRING"] ?? string.Empty,
            RawZoneContainer = configuration["RAW_ZONE_CONTAINER"] ?? string.Empty,
            ReportingTimezone = configuration["REPORTING_TIMEZONE"] ?? "Africa/Johannesburg",
            ReportingCurrency = configuration["REPORTING_CURRENCY"] ?? "ZAR",
            MaxConcurrentExtractions = int.TryParse(configuration["MAX_CONCURRENT_EXTRACTIONS"], out var concurrency)
                ? concurrency
                : 20,
        };

        var result = new IngestionApiOptionsValidator().Validate(options);
        if (!result.IsValid)
        {
            throw new InvalidOperationException(
                $"Invalid environment variables: {string.Join("; ", result.Errors.Select(e => e.ErrorMessage))}");
        }

        return options;
    }
}
