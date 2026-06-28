using Microsoft.EntityFrameworkCore;
using Shared.Models;

namespace Agent.Data;

public class FilesDbContext : DbContext
{
    public FilesDbContext(DbContextOptions<FilesDbContext> options) : base(options) { }
    
    public DbSet<FileRecord> Files { get; set; }
    public DbSet<ProcessingResult> ProcessingResults { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<FileRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ContentType).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).IsRequired();
        });
        
        modelBuilder.Entity<ProcessingResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileId).IsRequired();
            entity.Property(e => e.ProcessedAt).IsRequired();
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Result).HasColumnType("TEXT");
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            
            // Foreign key relationship
            entity.HasOne<FileRecord>()
                .WithMany()
                .HasForeignKey(e => e.FileId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
