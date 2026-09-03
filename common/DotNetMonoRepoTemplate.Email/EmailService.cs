using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DotNetMonoRepoTemplate.Logging;

namespace DotNetMonoRepoTemplate.Email;

public sealed class EmailService : IEmailService
{
    private const string ProductionEndpoint = "https://send.api.mailtrap.io/api/send";
    private const string SandboxEndpointTemplate = "https://sandbox.api.mailtrap.io/api/send/{0}";
    private const string DefaultFromName = "Node Mono Repo Template";

    private readonly HttpClient _httpClient;
    private readonly EmailOptions _options;
    private readonly Logger _logger = new(nameof(EmailService));

    public EmailService(HttpClient httpClient, EmailOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<bool> SendMailAsync(
        string to,
        string subject,
        string template,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.MailtrapApiKey))
        {
            _logger.Warn("No email provider configured - set MAILTRAP_API_KEY");
            return false;
        }
        return await SendViaMailtrapAsync(to, subject, template, variables, cancellationToken);
    }

    private async Task<bool> SendViaMailtrapAsync(
        string to,
        string subject,
        string template,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_options.FromEmail))
            {
                _logger.Error("MAILTRAP_FROM is not set");
                return false;
            }

            var fromName = string.IsNullOrWhiteSpace(_options.FromName) ? DefaultFromName : _options.FromName;
            var html = await RenderTemplateAsync(template, variables, cancellationToken);
            var sandbox = !string.IsNullOrWhiteSpace(_options.TestInboxId);
            var endpoint = sandbox ? string.Format(SandboxEndpointTemplate, _options.TestInboxId) : ProductionEndpoint;

            var payload = new
            {
                from = new { email = _options.FromEmail, name = fromName },
                to = new[] { new { email = to } },
                subject,
                html,
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.MailtrapApiKey);
            request.Content = JsonContent.Create(payload);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.Error(
                    "Failed to send email via Mailtrap",
                    new Dictionary<string, object?> { ["status"] = (int)response.StatusCode, ["body"] = body });
                return false;
            }

            _logger.Info(
                "Email sent via Mailtrap",
                new Dictionary<string, object?> { ["to"] = to, ["subject"] = subject, ["sandbox"] = sandbox });
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to send email via Mailtrap", ex);
            return false;
        }
    }

    private async Task<string> RenderTemplateAsync(
        string template,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken)
    {
        var templateFile = Path.Combine(_options.TemplatesDirectory, $"{template}.html");
        string rawHtml;
        try
        {
            rawHtml = await File.ReadAllTextAsync(templateFile, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to read email template", ex);
            throw new InvalidOperationException($"Email template '{template}' not found", ex);
        }
        return ApplyTemplate(rawHtml, variables);
    }

    private static string ApplyTemplate(string html, IReadOnlyDictionary<string, object?> variables)
    {
        var result = html;
        foreach (var (key, value) in variables)
        {
            var placeholder = $"{{{{{key}}}}}";
            if (result.Contains(placeholder, StringComparison.Ordinal))
            {
                result = result.Replace(placeholder, SafeStringify(value), StringComparison.Ordinal);
            }
        }
        return result;
    }

    private static string SafeStringify(object? value) => value switch
    {
        null => string.Empty,
        string stringValue => stringValue,
        DateTime dateValue => dateValue.ToUniversalTime().ToString("O"),
        bool or int or long or double or decimal or float => value.ToString() ?? string.Empty,
        _ => JsonSerializer.Serialize(value),
    };
}
