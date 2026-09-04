namespace DotNetMonoRepoTemplate.Database.Entities;

public sealed class ConnectorCredential : AuditableEntity
{
    public required string SourceSystemId { get; set; }
    public SourceSystem? SourceSystem { get; set; }
    public required string AccountId { get; set; }
    public required string KeyVaultSecretName { get; set; }
    public required string CredentialType { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastRotatedAt { get; set; }
}
