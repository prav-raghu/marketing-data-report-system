namespace DotNetMonoRepoTemplate.Sms;

public interface ISmsService
{
    Task<bool> SendSmsAsync(string to, string message, CancellationToken cancellationToken = default);
}
