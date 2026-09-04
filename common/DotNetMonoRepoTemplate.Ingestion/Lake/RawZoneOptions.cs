namespace DotNetMonoRepoTemplate.Ingestion.Lake;

public sealed record RawZoneOptions
{
    public required string ConnectionString { get; init; }
    public required string ContainerName { get; init; }
}
