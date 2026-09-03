namespace DotNetMonoRepoTemplate.Email;

public interface IEmailService
{
    Task<bool> SendMailAsync(
        string to,
        string subject,
        string template,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken = default);
}
