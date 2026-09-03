namespace DotNetMonoRepoTemplate.Sms;

public sealed record SmsOptions
{
    public string? ClientId { get; init; }
    public string? ApiSecret { get; init; }
    public string? SenderId { get; init; }
    public bool Enabled { get; init; } = true;
}
