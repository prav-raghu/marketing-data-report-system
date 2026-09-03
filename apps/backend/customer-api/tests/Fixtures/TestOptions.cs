using CustomerApi.Configuration;

namespace CustomerApi.Tests.Fixtures;

public static class TestOptions
{
    public static CustomerApiOptions CustomerApi(Action<CustomerApiOptionsBuilder>? configure = null)
    {
        var builder = new CustomerApiOptionsBuilder();
        configure?.Invoke(builder);
        return builder.Build();
    }
}

public sealed class CustomerApiOptionsBuilder
{
    public string JwtSecret { get; set; } = "test-jwt-secret-key-with-enough-entropy-for-hmacsha256";
    public string JwtRefreshSecret { get; set; } = "test-jwt-refresh-secret-key-with-enough-entropy-for-hmacsha256";
    public string RedisUrl { get; set; } = "redis://localhost:6379";
    public string RefreshTokenExpiry { get; set; } = "30d";
    public string AuthTokenExpiry { get; set; } = "1h";
    public string CorsOrigin { get; set; } = "http://localhost:3000";
    public string RateLimitWindow { get; set; } = "1m";
    public int RateLimitMax { get; set; } = 200;
    public int Port { get; set; } = 4002;
    public string NodeEnv { get; set; } = "test";
    public string MailtrapApiKey { get; set; } = "test-mailtrap-api-key";
    public string MailtrapFrom { get; set; } = "noreply@test.com";
    public string MailtrapFromName { get; set; } = "Test Sender";
    public string CustomerWebUrl { get; set; } = "http://localhost:3000";
    public int? AccountBanThreshold { get; set; }

    public CustomerApiOptions Build() =>
        new()
        {
            JwtSecret = JwtSecret,
            JwtRefreshSecret = JwtRefreshSecret,
            RedisUrl = RedisUrl,
            RefreshTokenExpiry = RefreshTokenExpiry,
            AuthTokenExpiry = AuthTokenExpiry,
            CorsOrigin = CorsOrigin,
            RateLimitWindow = RateLimitWindow,
            RateLimitMax = RateLimitMax,
            Port = Port,
            NodeEnv = NodeEnv,
            MailtrapApiKey = MailtrapApiKey,
            MailtrapFrom = MailtrapFrom,
            MailtrapFromName = MailtrapFromName,
            CustomerWebUrl = CustomerWebUrl,
            AccountBanThreshold = AccountBanThreshold,
        };
}
