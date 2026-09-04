using DotNetMonoRepoTemplate.Types;

namespace DotNetMonoRepoTemplate.Database.Entities;

public sealed class SchemaContract : AuditableEntity
{
    public required string SourceSystemId { get; set; }
    public SourceSystem? SourceSystem { get; set; }
    public required string SourceEntity { get; set; }
    public required string Version { get; set; }
    public PayloadFormat Format { get; set; } = PayloadFormat.Json;
    public required string Definition { get; set; }
    public required string DefinitionHash { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}
