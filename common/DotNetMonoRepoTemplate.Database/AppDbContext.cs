using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using DotNetMonoRepoTemplate.Database.Entities;

namespace DotNetMonoRepoTemplate.Database;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserStatus> UserStatuses => Set<UserStatus>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
    public DbSet<SourceSystem> SourceSystems => Set<SourceSystem>();
    public DbSet<SourceConnector> SourceConnectors => Set<SourceConnector>();
    public DbSet<ConnectorCredential> ConnectorCredentials => Set<ConnectorCredential>();
    public DbSet<IngestionRun> IngestionRuns => Set<IngestionRun>();
    public DbSet<IngestionCheckpoint> IngestionCheckpoints => Set<IngestionCheckpoint>();
    public DbSet<SchemaContract> SchemaContracts => Set<SchemaContract>();
    public DbSet<QuarantineRecord> QuarantineRecords => Set<QuarantineRecord>();
    public DbSet<MetricDefinition> MetricDefinitions => Set<MetricDefinition>();
    public DbSet<ConversionActionMapping> ConversionActionMappings => Set<ConversionActionMapping>();
    public DbSet<BudgetPlan> BudgetPlans => Set<BudgetPlan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasIndex(u => new { u.IsActive, u.LastSeen });
            entity.HasIndex(u => new { u.UserStatusId, u.IsActive });
            entity.HasIndex(u => u.RoleId);
            entity.HasIndex(u => u.AuthHash);
            entity.HasIndex(u => u.CreatedAt);
            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
            entity
                .HasOne(u => u.Status)
                .WithMany(s => s.Users)
                .HasForeignKey(u => u.UserStatusId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne(u => u.Roles)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasIndex(r => r.Name).IsUnique();
        });

        modelBuilder.Entity<UserStatus>(entity =>
        {
            entity.ToTable("user_statuses");
            entity.HasIndex(s => s.Name).IsUnique();
        });

        modelBuilder.Entity<WebhookSubscription>(entity =>
        {
            entity.ToTable("webhook_subscriptions");
            entity.HasIndex(w => w.IsActive);
            entity.HasIndex(w => w.CreatedAt);
        });

        modelBuilder.Entity<WebhookDelivery>(entity =>
        {
            entity.ToTable("webhook_deliveries");
            entity.HasIndex(d => d.SubscriptionId);
            entity.HasIndex(d => d.Status);
            entity.HasIndex(d => d.EventType);
            entity.HasIndex(d => d.CreatedAt);
            entity.HasIndex(d => d.NextRetryAt);
            entity.Property(d => d.Payload)
                .HasColumnType("jsonb")
                .HasConversion(
                    payload => payload.RootElement.GetRawText(),
                    json => JsonDocument.Parse(json, default));
            entity
                .HasOne(d => d.Subscription)
                .WithMany(s => s.Deliveries)
                .HasForeignKey(d => d.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SourceSystem>(entity =>
        {
            entity.ToTable("source_systems");
            entity.HasIndex(x => x.Key).IsUnique();
            entity.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<SourceConnector>(entity =>
        {
            entity.ToTable("source_connectors");
            entity.HasIndex(x => new { x.SourceSystemId, x.SourceEntity, x.AccountId }).IsUnique();
            entity.HasIndex(x => x.Tier);
            entity.HasIndex(x => new { x.IsActive, x.LastRunAt });
            entity.Property(x => x.Tier).HasConversion<string>();
            entity.Property(x => x.TrailingNinetyDaySpendZar).HasPrecision(18, 4);
            entity
                .HasOne(x => x.SourceSystem)
                .WithMany(s => s.Connectors)
                .HasForeignKey(x => x.SourceSystemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ConnectorCredential>(entity =>
        {
            entity.ToTable("connector_credentials");
            entity.HasIndex(x => new { x.SourceSystemId, x.AccountId, x.CredentialType }).IsUnique();
            entity.HasIndex(x => x.ExpiresAt);
            entity
                .HasOne(x => x.SourceSystem)
                .WithMany()
                .HasForeignKey(x => x.SourceSystemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IngestionRun>(entity =>
        {
            entity.ToTable("ingestion_runs");
            entity.HasIndex(x => new { x.SourceConnectorId, x.Status });
            entity
                .HasIndex(x => x.SourceConnectorId)
                .HasFilter("status IN ('Pending', 'Running')")
                .IsUnique()
                .HasDatabaseName("ix_ingestion_runs_single_in_flight");
            entity.HasIndex(x => new { x.SourceConnectorId, x.WindowStart, x.WindowEnd });
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.Status).HasConversion<string>();
            entity.Property(x => x.Trigger).HasConversion<string>();
            entity
                .HasOne(x => x.SourceConnector)
                .WithMany(c => c.Runs)
                .HasForeignKey(x => x.SourceConnectorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IngestionCheckpoint>(entity =>
        {
            entity.ToTable("ingestion_checkpoints");
            entity.HasIndex(x => x.SourceConnectorId).IsUnique();
            entity
                .HasOne(x => x.SourceConnector)
                .WithMany()
                .HasForeignKey(x => x.SourceConnectorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SchemaContract>(entity =>
        {
            entity.ToTable("schema_contracts");
            entity.HasIndex(x => new { x.SourceSystemId, x.SourceEntity, x.Version }).IsUnique();
            entity.HasIndex(x => x.EffectiveFrom);
            entity.Property(x => x.Format).HasConversion<string>();
            entity
                .HasOne(x => x.SourceSystem)
                .WithMany(s => s.Contracts)
                .HasForeignKey(x => x.SourceSystemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<QuarantineRecord>(entity =>
        {
            entity.ToTable("quarantine_records");
            entity.HasIndex(x => x.IngestionRunId);
            entity.HasIndex(x => new { x.Resolution, x.CreatedAt });
            entity.HasIndex(x => x.ReasonCode);
            entity.Property(x => x.Resolution).HasConversion<string>();
            entity
                .HasOne(x => x.IngestionRun)
                .WithMany(r => r.QuarantineRecords)
                .HasForeignKey(x => x.IngestionRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MetricDefinition>(entity =>
        {
            entity.ToTable("metric_definitions");
            entity.HasIndex(x => x.CanonicalName).IsUnique();
            entity.Property(x => x.Additivity).HasConversion<string>();
        });

        modelBuilder.Entity<ConversionActionMapping>(entity =>
        {
            entity.ToTable("conversion_action_mappings");
            entity.HasIndex(x => new { x.SourceSystemId, x.AccountId, x.PlatformActionType }).IsUnique();
            entity
                .HasOne(x => x.SourceSystem)
                .WithMany()
                .HasForeignKey(x => x.SourceSystemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BudgetPlan>(entity =>
        {
            entity.ToTable("budget_plans");
            entity.HasIndex(x => new { x.SourceSystemId, x.CampaignId, x.PeriodStart }).IsUnique();
            entity.HasIndex(x => new { x.PeriodStart, x.PeriodEnd });
            entity.Property(x => x.PlannedSpendZar).HasPrecision(18, 4);
            entity
                .HasOne(x => x.SourceSystem)
                .WithMany()
                .HasForeignKey(x => x.SourceSystemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType).Property(nameof(AuditableEntity.IsActive)).HasDefaultValue(true);
            }
        }
    }

    public override int SaveChanges()
    {
        ApplyTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyTimestamps()
    {
        var utcNow = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is AuditableEntity auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    auditable.CreatedAt = utcNow;
                }
                if (entry.State is EntityState.Added or EntityState.Modified)
                {
                    auditable.UpdatedAt = utcNow;
                }
            }
            else if (entry.Entity is TimestampedEntity timestamped)
            {
                if (entry.State == EntityState.Added)
                {
                    timestamped.CreatedAt = utcNow;
                }
                if (entry.State is EntityState.Added or EntityState.Modified)
                {
                    timestamped.UpdatedAt = utcNow;
                }
            }
        }
    }
}
