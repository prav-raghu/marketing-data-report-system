namespace DotNetMonoRepoTemplate.Storage;

public sealed record AzureBlobOptions
{
    public required string ConnectionString { get; init; }
    public string? DefaultContainer { get; init; }
}
