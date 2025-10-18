using Domain.Common;
using Domain.Common.Abstract;
using Domain.Tasks;
using Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class ApplicationDbContext : IdentityDbContext<IdentityUser>
{
    private readonly string _currentUserId;
    private readonly bool _isAuthenticated;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUser currentUser) : base(options)
    {
        _currentUserId = currentUser.UserId ?? string.Empty;
        _isAuthenticated = currentUser.IsAuthenticated;
    }

    public DbSet<UserSetting> UserSettings => Set<UserSetting>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TaskTag> TaskTags => Set<TaskTag>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<UserSetting>(cfg =>
        {
            cfg.HasKey(x => new { x.UserId, x.Key });
            cfg.Property(x => x.UserId).IsRequired().HasMaxLength(256);
            cfg.Property(x => x.Key).IsRequired().HasMaxLength(128);
            cfg.Property(x => x.Value).IsRequired().HasMaxLength(2048);

            cfg.HasQueryFilter(s => s.UserId == _currentUserId);
        });

        builder.Entity<TaskItem>(cfg =>
        {
            cfg.HasKey(t => t.Id);

            cfg.Property(t => t.OwnerUserId)
                .IsRequired()
                .HasMaxLength(Validation.UserIdMaxLength);

            cfg.Property(t => t.Title)
                .IsRequired()
                .HasMaxLength(Validation.Task.TitleMaxLength);

            cfg.Property(t => t.Description)
                .HasMaxLength(Validation.Task.DescriptionMaxLength);

            cfg.Property(t => t.Priority).IsRequired();
            cfg.Property(t => t.Status).IsRequired();

            cfg.Property(t => t.CreatedAtUtc).IsRequired();
            cfg.Property(t => t.UpdatedAtUtc).IsRequired();
            cfg.Property(t => t.CompletedAtUtc);

            cfg.Property(t => t.RowVersion).IsRowVersion();

            cfg.HasIndex(t => new { t.OwnerUserId, t.Status });
            cfg.HasIndex(t => new { t.OwnerUserId, t.DueDate });
            cfg.HasIndex(t => new { t.OwnerUserId, t.CreatedAtUtc });
            cfg.HasIndex(t => new { t.OwnerUserId, t.Priority });

            // Global per-user filter
            cfg.HasQueryFilter(t => t.OwnerUserId == _currentUserId);
        });

        builder.Entity<Tag>(cfg =>
        {
            cfg.HasKey(t => t.Id);

            cfg.Property(t => t.OwnerUserId)
                .IsRequired()
                .HasMaxLength(Validation.UserIdMaxLength);

            cfg.Property(t => t.Name)
                .IsRequired()
                .HasMaxLength(Validation.Tag.NameMaxLength);

            cfg.Property(t => t.NormalizedName)
                .IsRequired()
                .HasMaxLength(Validation.Tag.NormalizedNameMaxLength);

            cfg.Property(t => t.CreatedAtUtc).IsRequired();
            cfg.Property(t => t.UpdatedAtUtc).IsRequired();

            cfg.HasIndex(t => new { t.OwnerUserId, t.NormalizedName }).IsUnique();

            cfg.HasQueryFilter(t => t.OwnerUserId == _currentUserId);
        });

        builder.Entity<TaskTag>(cfg =>
        {
            cfg.HasKey(tt => new { tt.TaskId, tt.TagId });

            cfg.HasOne(tt => tt.Task)
                .WithMany(t => t.TaskTags)
                .HasForeignKey(tt => tt.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            cfg.HasOne(tt => tt.Tag)
                .WithMany(t => t.TaskTags)
                .HasForeignKey(tt => tt.TagId)
                .OnDelete(DeleteBehavior.Cascade);

            cfg.HasIndex(tt => tt.TaskId);
            cfg.HasIndex(tt => tt.TagId);
        });
    }

    public override int SaveChanges()
    {
        StampAuditAndOwnership();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditAndOwnership();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampAuditAndOwnership()
    {
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Property(p => p.CreatedAtUtc).CurrentValue == default)
                    entry.Property(p => p.CreatedAtUtc).CurrentValue = utcNow;

                entry.Property(p => p.UpdatedAtUtc).CurrentValue = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(p => p.UpdatedAtUtc).CurrentValue = utcNow;
            }
        }

        if (!_isAuthenticated || string.IsNullOrEmpty(_currentUserId)) return;
        
        foreach (var e in ChangeTracker.Entries<TaskItem>().Where(e => e.State == EntityState.Added))
        {
            if (string.IsNullOrWhiteSpace(e.Entity.OwnerUserId))
                e.Entity.OwnerUserId = _currentUserId;
        }

        foreach (var e in ChangeTracker.Entries<Tag>().Where(e => e.State == EntityState.Added))
        {
            if (string.IsNullOrWhiteSpace(e.Entity.OwnerUserId))
                e.Entity.OwnerUserId = _currentUserId;
        }

        foreach (var e in ChangeTracker.Entries<UserSetting>().Where(e => e.State == EntityState.Added))
        {
            if (string.IsNullOrWhiteSpace(e.Entity.UserId))
                e.Entity.UserId = _currentUserId;
        }
    }
}
