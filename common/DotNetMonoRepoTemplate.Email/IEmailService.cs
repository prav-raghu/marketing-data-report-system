namespace DotNetMonoRepoTemplate.Email;

public interface IEmailService
{
    public Task<bool> SendMailAsync(
        string recipient,
        string subject,
        string templateName,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken = default);
}
