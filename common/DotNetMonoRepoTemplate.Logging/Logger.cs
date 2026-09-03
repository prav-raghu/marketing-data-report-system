using Serilog.Events;

namespace DotNetMonoRepoTemplate.Logging;

public sealed class Logger
{
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization",
        "cookie",
        "password",
        "currentPassword",
        "newPassword",
        "confirmPassword",
        "token",
        "refreshToken",
        "accessToken",
        "secret",
        "apiKey",
        "api_key",
        "clientSecret",
        "privateKey",
        "pin",
        "otp",
        "twoFactorCode",
        "ssn",
        "nationalId",
        "creditCard",
        "cardNumber",
        "cvv",
        "cvc",
        "expiryDate",
    };

    private readonly Serilog.ILogger _logger;

    public Logger(string? context = null)
    {
        _logger = Serilog.Log.ForContext("SourceContext", context ?? "Application");
    }

    public void Info(string message, IReadOnlyDictionary<string, object?>? data = null) =>
        Write(LogEventLevel.Information, message, data);

    public void Warn(string message, IReadOnlyDictionary<string, object?>? data = null) =>
        Write(LogEventLevel.Warning, message, data);

    public void Debug(string message, IReadOnlyDictionary<string, object?>? data = null) =>
        Write(LogEventLevel.Debug, message, data);

    public void Error(string message) => _logger.Error(message);

    public void Error(string message, Exception error) => _logger.Error(error, message);

    public void Error(string message, IReadOnlyDictionary<string, object?> data) =>
        Write(LogEventLevel.Error, message, data);

    private void Write(LogEventLevel level, string message, IReadOnlyDictionary<string, object?>? data)
    {
        var logger = _logger;
        if (data is not null)
        {
            foreach (var (key, value) in Redact(data))
            {
                logger = logger.ForContext(key, value, destructureObjects: true);
            }
        }
        logger.Write(level, message);
    }

    private static Dictionary<string, object?> Redact(IReadOnlyDictionary<string, object?> data)
    {
        var result = new Dictionary<string, object?>(data.Count);
        foreach (var (key, value) in data)
        {
            result[key] = SensitiveKeys.Contains(key) ? "[REDACTED]" : value;
        }
        return result;
    }
}
