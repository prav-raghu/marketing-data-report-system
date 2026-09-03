namespace DotNetMonoRepoTemplate.Sms;

public interface ISmsService
{
    public Task<bool> SendSmsAsync(string recipient, string message, CancellationToken cancellationToken = default);
}
