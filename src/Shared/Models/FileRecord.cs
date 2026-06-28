using System.ComponentModel.DataAnnotations;

namespace Shared.Models;

/// <summary>
/// Represents a file record in the files database
/// </summary>
public class FileRecord
{
    /// <summary>
    /// Unique identifier for the file record
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Original filename
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Full path to the file on disk
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// MIME content type of the file
    /// </summary>
    [MaxLength(100)]
    public string? ContentType { get; set; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// MD5 hash of the file content for deduplication
    /// </summary>
    [MaxLength(32)]
    public string? FileHash { get; set; }

    /// <summary>
    /// Source of the file (e.g., "Dropbox", "Upload", "Email")
    /// </summary>
    [MaxLength(50)]
    public string? Source { get; set; }

    /// <summary>
    /// Original source path or URL
    /// </summary>
    [MaxLength(1000)]
    public string? SourcePath { get; set; }

    /// <summary>
    /// When the file was created/uploaded
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the file record was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Additional metadata as JSON
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Whether the file has been processed
    /// </summary>
    public bool IsProcessed { get; set; }

    /// <summary>
    /// Whether the file is archived/deleted
    /// </summary>
    public bool IsArchived { get; set; }
}
