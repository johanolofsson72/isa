using Agent.Models;
using Agent.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Agent.Data;

/// <summary>
/// Entity Framework DbContext for memory system - separate database for memory data
/// </summary>
public class MemoryDbContext : DbContext
{
    public MemoryDbContext(DbContextOptions<MemoryDbContext> options)
        : base(options) { }

    // Parameterless constructor for Docker compatibility
    public MemoryDbContext() : base() { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Fallback configuration for Docker environment
            optionsBuilder.UseSqlite("Data Source=memory.db");
        }
    }

    // Memory system tables
    public DbSet<MemoryEntry> MemoryEntries { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Device> Devices { get; set; } = null!;
    public DbSet<DeviceUser> DeviceUsers { get; set; } = null!;
    public DbSet<Consent> Consents { get; set; } = null!;
    public DbSet<UserRuntimeState> UserRuntimeStates { get; set; } = null!;
    public DbSet<ConversationHistory> ConversationHistory { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasMaxLength(36).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.Locale).HasMaxLength(10);

            entity.HasIndex(e => e.DisplayName);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.LastSeenAt);
        });

        // Device configuration
        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(e => e.DeviceId);
            entity.Property(e => e.DeviceId).HasMaxLength(36).IsRequired();
            entity.Property(e => e.PrimaryUserId).HasMaxLength(36);

            entity.HasIndex(e => e.PrimaryUserId);
            entity.HasIndex(e => e.FirstSeen);
            entity.HasIndex(e => e.LastSeen);
        });

        // DeviceUser configuration
        modelBuilder.Entity<DeviceUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DeviceId).HasMaxLength(36).IsRequired();
            entity.Property(e => e.UserId).HasMaxLength(36).IsRequired();

            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Confidence);
            entity.HasIndex(e => e.LastSeen);
            entity.HasIndex(e => new { e.DeviceId, e.UserId }).IsUnique();

            // Foreign key relationships
            entity.HasOne(e => e.Device)
                .WithMany(d => d.DeviceUsers)
                .HasForeignKey(e => e.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany(u => u.DeviceUsers)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Consent configuration
        modelBuilder.Entity<Consent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(36).IsRequired();
            entity.Property(e => e.Kind).HasMaxLength(100).IsRequired();

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Kind);
            entity.HasIndex(e => new { e.UserId, e.Kind }).IsUnique();

            // Foreign key relationship
            entity.HasOne(e => e.User)
                .WithMany(u => u.Consents)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // UserRuntimeState configuration
        modelBuilder.Entity<UserRuntimeState>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(36).IsRequired();
            
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.LastNamePromptAt);
            entity.HasIndex(e => e.LastOnlineSearchAt);
            
            // Foreign key relationship
            entity.HasOne(e => e.User)
                .WithMany(u => u.RuntimeStates)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Memory Entry configuration
        modelBuilder.Entity<MemoryEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.Source).HasMaxLength(100);
            entity.Property(e => e.OwnerUserId).HasMaxLength(36);
            entity.Property(e => e.SubjectUserId).HasMaxLength(36);
            entity.Property(e => e.TenantId).HasMaxLength(36);
            entity.Property(e => e.DeviceFingerprint).HasMaxLength(36);
            entity.Property(e => e.EmbeddingProvider).HasMaxLength(100);
            entity.Property(e => e.EmbeddingModelId).HasMaxLength(100);
            entity.Property(e => e.EmbeddingVersion).HasMaxLength(50);
            entity.Property(e => e.RedactionMask).HasMaxLength(1000);
            entity.Property(e => e.ReplacesMemoryId).HasMaxLength(36);

            // Indexes for efficient querying
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.Scope);
            entity.HasIndex(e => e.Confidentiality);
            entity.HasIndex(e => e.OwnerUserId);
            entity.HasIndex(e => e.SubjectUserId);
            entity.HasIndex(e => e.DeviceFingerprint);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.ExpiresAt);
            entity.HasIndex(e => e.TombstonedAt);
            entity.HasIndex(e => new { e.OwnerUserId, e.Type });
            entity.HasIndex(e => new { e.Scope, e.Type });
            entity.HasIndex(e => new { e.Confidentiality, e.Type });
        });

        // ConversationHistory configuration
        modelBuilder.Entity<ConversationHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SessionId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(20).IsRequired();
            entity.Property(e => e.DeviceFingerprint).HasMaxLength(36);

            // Indexes for efficient querying
            entity.HasIndex(e => new { e.SessionId, e.MessageOrder });
            entity.HasIndex(e => e.DeviceFingerprint);
            entity.HasIndex(e => e.ExpiresAt);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}
