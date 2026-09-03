using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using DotNetMonoRepoTemplate.Logging;

namespace DotNetMonoRepoTemplate.Sms;

public sealed partial class SmsService : ISmsService
{
    private const string SendUrl = "https://rest.smsportal.com/v3/BulkMessages";

    private readonly HttpClient _httpClient;
    private readonly SmsOptions _options;
    private readonly Logger _logger = new(nameof(SmsService));

    public SmsService(HttpClient httpClient, SmsOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<bool> SendSmsAsync(string to, string message, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.Info("SMS disabled - skipping", new Dictionary<string, object?> { ["to"] = to });
            return false;
        }

        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ApiSecret))
        {
            _logger.Error("SMSPORTAL_CLIENT_ID or SMSPORTAL_API_SECRET is not set");
            return false;
        }

        var destination = ToE164(to);
        if (destination is null)
        {
            _logger.Error("Invalid phone number - cannot convert to E164", new Dictionary<string, object?> { ["to"] = to });
            return false;
        }

        try
        {
            IDictionary<string, object?> body = new Dictionary<string, object?>
            {
                ["messages"] = new object[] { new { content = message, destination } },
            };
            if (!string.IsNullOrWhiteSpace(_options.SenderId))
            {
                body["sendOptions"] = new { senderId = _options.SenderId };
            }

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ApiSecret}"));

            using var request = new HttpRequestMessage(HttpMethod.Post, SendUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            request.Content = JsonContent.Create(body);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadFromJsonAsync<SmsPortalResponse>(cancellationToken: cancellationToken)
                ?? new SmsPortalResponse();

            var faults = json.SendResponse?.ErrorReport?.Faults ?? Array.Empty<SmsPortalFault>();
            var accepted = json.SendResponse?.Messages ?? 0;
            var topLevelErrors = json.Errors ?? Array.Empty<string>();

            if (!response.IsSuccessStatusCode || topLevelErrors.Count > 0 || faults.Count > 0 || accepted < 1)
            {
                _logger.Error(
                    "SMSPortal rejected the request",
                    new Dictionary<string, object?>
                    {
                        ["status"] = (int)response.StatusCode,
                        ["errors"] = topLevelErrors,
                        ["faults"] = faults,
                        ["accepted"] = accepted,
                        ["to"] = to,
                    });
                return false;
            }

            _logger.Info("SMS sent successfully", new Dictionary<string, object?> { ["to"] = destination });
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("Unexpected error sending SMS via SMSPortal", ex);
            return false;
        }
    }

    private static string? ToE164(string phone)
    {
        var digits = NonDigitRegex().Replace(phone, string.Empty);
        if (digits.StartsWith("27", StringComparison.Ordinal) && digits.Length == 11)
        {
            return digits;
        }
        if (digits.StartsWith('0') && digits.Length == 10)
        {
            return $"27{digits[1..]}";
        }
        return null;
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigitRegex();
}
