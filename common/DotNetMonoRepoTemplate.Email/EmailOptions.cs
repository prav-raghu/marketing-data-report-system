namespace DotNetMonoRepoTemplate.Email;

public sealed record EmailOptions
{
    public string? MailtrapApiKey { get; init; }
    public string? FromEmail { get; init; }
    public string? FromName { get; init; }
    public string? TestInboxId { get; init; }
    public string TemplatesDirectory { get; init; } = Path.Combine(AppContext.BaseDirectory, "Templates");
}
