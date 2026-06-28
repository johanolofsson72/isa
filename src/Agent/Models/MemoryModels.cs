using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Agent.Models;

/// <summary>
/// Types of memory entries with specific similarity thresholds
/// </summary>
public enum MemoryType
{
    Identity,        // Names, personal info (threshold: 0.90)
    Facts,          // Mathematical facts, definitions (threshold: 0.85)
    EpisodicContext // Preferences, context (threshold: 0.80)
}

/// <summary>
/// Scope of memory entry - determines access control
/// </summary>
public enum MemoryScope
{
    Public,         // Accessible to all users
    UserPrivate,    // Only accessible to owner
    TeamShared     // Shared within team/organization
}

/// <summary>
/// Confirmation state for memory entries
/// </summary>
public enum ConfirmationState
{
    Pending,        // Awaiting user confirmation
    Confirmed,      // User confirmed as correct
    Rejected        // User rejected as incorrect
}

/// <summary>
/// Source of memory information
/// </summary>
public enum Provenance
{
    DirectUser,     // User explicitly provided
    Plugin,         // From plugin execution
    ExternalSearch, // From online search
    Inferred       // AI inferred from context
}

/// <summary>
/// Confidentiality level for memory entries
/// </summary>
public enum Confidentiality
{
    Public,         // Accessible to all users
    Internal,       // Internal to organization
    Private         // Only accessible to owner
}

/// <summary>
/// Core memory entry with embeddings and metadata
/// </summary>
public class MemoryEntry
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    public MemoryType Type { get; set; }
    public MemoryScope Scope { get; set; } = MemoryScope.UserPrivate;
    
    public double Score { get; set; } = 1.0;
    public double Confidence { get; set; } = 1.0;
    public bool IsAuthoritative { get; set; } = false;
    
    // Provenance and confirmation
    public string Source { get; set; } = "agent";
    public Provenance Provenance { get; set; } = Provenance.DirectUser;
    public ConfirmationState Confirmation { get; set; } = ConfirmationState.Pending;
    public Confidentiality Confidentiality { get; set; } = Confidentiality.Internal;

    // Ownership and access control
    public string? OwnerUserId { get; set; }
    public string? SubjectUserId { get; set; }
    public string? TenantId { get; set; }
    public string? DeviceFingerprint { get; set; }

    // Embedding metadata
    public string EmbeddingProvider { get; set; } = "litellm";
    public string EmbeddingModelId { get; set; } = "embed";
    public int EmbeddingDim { get; set; } = 768;
    public string? EmbeddingVersion { get; set; }
    public bool L2Normed { get; set; } = true;
    
    // Embedding vector as JSON array
    public string? EmbeddingVector { get; set; }
    
    // Security and redaction
    public string? RedactionMask { get; set; }
    
    // Timestamps and lifecycle
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? TombstonedAt { get; set; }
    public string? ReplacesMemoryId { get; set; }
}

/// <summary>
/// User profile information
/// </summary>
public class User
{
    [Key]
    public string UserId { get; set; } = Guid.NewGuid().ToString();
    
    public string? DisplayName { get; set; }
    public string? Locale { get; set; } = "sv-SE";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAt { get; set; }
    
    // Navigation properties
    public ICollection<DeviceUser> DeviceUsers { get; set; } = new List<DeviceUser>();
    public ICollection<Consent> Consents { get; set; } = new List<Consent>();
    public ICollection<UserRuntimeState> RuntimeStates { get; set; } = new List<UserRuntimeState>();
}

/// <summary>
/// Device information for cookie-based identification
/// </summary>
public class Device
{
    [Key]
    public string DeviceId { get; set; } = Guid.NewGuid().ToString();
    
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public string? PrimaryUserId { get; set; }
    
    // Navigation properties
    public ICollection<DeviceUser> DeviceUsers { get; set; } = new List<DeviceUser>();
}

/// <summary>
/// Many-to-many relationship between devices and users
/// </summary>
public class DeviceUser
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    public string DeviceId { get; set; } = string.Empty;
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    public double Confidence { get; set; } = 1.0;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public Device Device { get; set; } = null!;
    public User User { get; set; } = null!;
}

/// <summary>
/// User consent for various operations
/// </summary>
public class Consent
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    public string Kind { get; set; } = string.Empty; // e.g., "OnlineSearch", "DataCollection"
    
    public bool Granted { get; set; } = false;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation property
    public User User { get; set; } = null!;
}

/// <summary>
/// Runtime state for user interactions
/// </summary>
public class UserRuntimeState
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    public int NamePromptCount { get; set; } = 0;
    public DateTime? LastNamePromptAt { get; set; }
    public DateTime? LastOnlineSearchAt { get; set; }
    public DateTime? LastOnlineSearchDate { get; set; }
    public int OnlineSearchCount { get; set; } = 0;
    public bool OnlineSearchIsStarted { get; set; } = false;
    public int ProfileVersion { get; set; } = 1;
    
    // Navigation property
    public User User { get; set; } = null!;
}

/// <summary>
/// Persistent storage for individual conversation messages
/// </summary>
public class ConversationHistory
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(100)]
    public string SessionId { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Role { get; set; } = string.Empty; // "User", "Assistant", "System"

    [MaxLength(36)]
    public string? DeviceFingerprint { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(365);

    public int MessageOrder { get; set; } // For maintaining message sequence

    // Optional metadata
    public long? ProcessingTimeMs { get; set; }
    public bool HasError { get; set; } = false;
    public string? ErrorMessage { get; set; }
}