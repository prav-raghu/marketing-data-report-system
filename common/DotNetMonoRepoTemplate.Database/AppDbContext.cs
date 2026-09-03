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
