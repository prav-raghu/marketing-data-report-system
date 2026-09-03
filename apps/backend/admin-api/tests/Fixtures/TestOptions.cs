using AdminApi.Configuration;

namespace AdminApi.Tests.Fixtures;

public static class TestOptions
{
    public static AdminApiOptions AdminApi() => new()
    {
        JwtSecret = "test-jwt-secret-key-for-unit-tests-only-0123456789",
        JwtRefreshSecret = "test-jwt-refresh-secret-key-for-unit-tests-only-9876543210",
        MfaChallengeSecret = "test-mfa-challenge-secret-key-for-unit-tests-only-1122334455",
        TwoFactorEncryptionKey = "0123456789abcdef0123456789abcdef",
        RedisUrl = "redis://localhost:6379",
        RefreshTokenExpiry = "30d",
        AuthTokenExpiry = "1h",
        CorsOrigin = "http://localhost:4004",
        RateLimitWindow = "1m",
        RateLimitMax = 100,
        Port = 4001,
        NodeEnv = "test",
        MailtrapApiKey = "test-mailtrap-api-key",
        MailtrapFrom = "no-reply@test.com",
        MailtrapFromName = "Test Admin",
        AdminWebUrl = "http://localhost:4004",
        PasswordResetExpirationMinutes = 30,
        AccountBanThreshold = null,
    };
}
