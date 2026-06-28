using System.ComponentModel.DataAnnotations;

namespace Agent.Data.Models;

public class IdempotencyEntry
{
    [Key]
    [MaxLength(64)]
    public string Key { get; set; } = string.Empty;

    public int StatusCode { get; set; }

    [MaxLength(128)]
    public string? ContentType { get; set; }

    public string ResponseBody { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);
}
