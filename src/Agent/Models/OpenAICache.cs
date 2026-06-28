using System.ComponentModel.DataAnnotations;

namespace Agent.Models;

/// <summary>
/// Entity for caching OpenAI API responses to reduce costs and improve performance
/// </summary>
public class OpenAICache
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// SHA256 hash of the request (messages + settings) for fast lookup
    /// </summary>
    [Required]
    public string RequestHash { get; set; } = string.Empty;
    
    /// <summary>
    /// The cached response content
    /// </summary>
    [Required]
    public string ResponseContent { get; set; } = string.Empty;
    
    /// <summary>
    /// JSON serialized request data for debugging
    /// </summary>
    public string? RequestData { get; set; }
    
    /// <summary>
    /// JSON serialized response data for debugging
    /// </summary>
    public string? ResponseData { get; set; }
    
    /// <summary>
    /// When this cache entry was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this cache entry was last accessed
    /// </summary>
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Number of times this cache entry has been used
    /// </summary>
    public int HitCount { get; set; } = 0;
    
    /// <summary>
    /// Optional expiration time for this cache entry
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
}
