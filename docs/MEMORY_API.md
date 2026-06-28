# Memory System API Documentation

## Overview

This document describes the API interfaces for the AccountingAgent memory system.

## IProductionMemoryService

The main interface for memory management operations.

### Methods

#### GetOrCreateUserAsync
```csharp
Task<User> GetOrCreateUserAsync(string deviceFingerprint)
```
Gets an existing user or creates a new one based on device fingerprint.

**Parameters:**
- `deviceFingerprint`: Unique device identifier

**Returns:** User entity

#### StoreMemoryAsync
```csharp
Task StoreMemoryAsync(Memory memory)
```
Stores a memory with vector embedding for semantic search.

**Parameters:**
- `memory`: Memory object to store

#### RetrieveMemoriesAsync
```csharp
Task<List<Memory>> RetrieveMemoriesAsync(
    string query, 
    MemoryType? memoryType = null, 
    double threshold = 0.7, 
    int maxResults = 10, 
    string? userId = null)
```
Retrieves memories using semantic search.

**Parameters:**
- `query`: Search query text
- `memoryType`: Optional memory type filter
- `threshold`: Similarity threshold (0.0-1.0)
- `maxResults`: Maximum number of results
- `userId`: User ID for access control

**Returns:** List of matching memories

#### HasConsentAsync
```csharp
Task<bool> HasConsentAsync(string userId, string consentKind)
```
Checks if user has granted specific consent.

**Parameters:**
- `userId`: User identifier
- `consentKind`: Type of consent (e.g., "memory_storage", "online_search")

**Returns:** True if consent granted

#### UpdateConsentAsync
```csharp
Task UpdateConsentAsync(string userId, string consentKind, bool granted, string? deviceFingerprint = null)
```
Updates user consent for specific operations.

**Parameters:**
- `userId`: User identifier
- `consentKind`: Type of consent
- `granted`: Whether consent is granted
- `deviceFingerprint`: Optional device fingerprint

#### GenerateSystemPromptAsync
```csharp
Task<string> GenerateSystemPromptAsync(string userId, string? context = null)
```
Generates system prompt with relevant memory context.

**Parameters:**
- `userId`: User identifier
- `context`: Optional context for memory retrieval

**Returns:** System prompt with memory context

#### GetUsersWithConsentAsync
```csharp
Task<List<User>> GetUsersWithConsentAsync(string consentKind)
```
Gets all users who have granted specific consent.

**Parameters:**
- `consentKind`: Type of consent

**Returns:** List of users with consent

## IIntelligentMemoryExtractor

Interface for extracting memories from conversations.

### Methods

#### ExtractMemoriesAsync
```csharp
Task<List<Memory>> ExtractMemoriesAsync(
    List<ChatMessage> messages, 
    string userId, 
    string? deviceFingerprint = null)
```
Extracts memories from a conversation.

**Parameters:**
- `messages`: List of chat messages
- `userId`: User identifier
- `deviceFingerprint`: Optional device fingerprint

**Returns:** List of extracted memories

#### ExtractFromText
```csharp
List<Memory> ExtractFromText(
    string text, 
    string userId, 
    string source, 
    string? deviceFingerprint = null)
```
Extracts memories from text content.

**Parameters:**
- `text`: Text to analyze
- `userId`: User identifier
- `source`: Source of the text
- `deviceFingerprint`: Optional device fingerprint

**Returns:** List of extracted memories

## IOnlineSearchService

Interface for managing online search with consent.

### Methods

#### CanSearchTodayAsync
```csharp
Task<bool> CanSearchTodayAsync(string userId)
```
Checks if user can perform online search today.

**Parameters:**
- `userId`: User identifier

**Returns:** True if user can search

#### SearchForUserAsync
```csharp
Task<string?> SearchForUserAsync(string userId, string searchQuery, string? deviceFingerprint = null)
```
Performs online search with consent and limits.

**Parameters:**
- `userId`: User identifier
- `searchQuery`: Search query
- `deviceFingerprint`: Optional device fingerprint

**Returns:** Search results or null if not allowed

#### RequestOnlineSearchConsentAsync
```csharp
Task<string> RequestOnlineSearchConsentAsync(string userId)
```
Requests consent for online search.

**Parameters:**
- `userId`: User identifier

**Returns:** Consent request message

#### ProcessConsentResponseAsync
```csharp
Task<string> ProcessConsentResponseAsync(string userId, string response)
```
Processes user consent response.

**Parameters:**
- `userId`: User identifier
- `response`: User's response

**Returns:** Response message

#### ConfirmSearchResultsAsync
```csharp
Task<string> ConfirmSearchResultsAsync(string userId, bool confirm)
```
Confirms or rejects search results.

**Parameters:**
- `userId`: User identifier
- `confirm`: True to confirm, false to reject

**Returns:** Confirmation message

#### GetSearchStatusAsync
```csharp
Task<string> GetSearchStatusAsync(string userId)
```
Gets search status for user.

**Parameters:**
- `userId`: User identifier

**Returns:** Search status message

## Data Models

### Memory
```csharp
public class Memory
{
    public Guid Id { get; set; }
    public string UserId { get; set; }
    public string Content { get; set; }
    public MemoryType Type { get; set; }
    public bool IsPrivate { get; set; }
    public bool RequiresConsent { get; set; }
    public string Source { get; set; }
    public string? DeviceFingerprint { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public float[]? Embedding { get; set; }
    public double? Relevance { get; set; }
}
```

### MemoryType Enum
```csharp
public enum MemoryType
{
    Facts,      // Factual information
    Identity,   // Personal identification
    Secrets,    // Sensitive information
    Episodic,   // Contextual information
    Preferences // User preferences
}
```

### User
```csharp
public class User
{
    public string UserId { get; set; }
    public string DeviceFingerprint { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastActiveAt { get; set; }
}
```

### Consent
```csharp
public class Consent
{
    public Guid Id { get; set; }
    public string UserId { get; set; }
    public string Kind { get; set; }
    public bool Granted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? DeviceFingerprint { get; set; }
}
```

## Error Handling

### Common Exceptions

- **UnauthorizedAccessException**: User lacks permission for operation
- **ArgumentException**: Invalid parameters provided
- **InvalidOperationException**: Operation not allowed in current state
- **TimeoutException**: Operation timed out

### Error Responses

All API methods handle errors gracefully and return appropriate error messages or empty results rather than throwing exceptions in normal operation.

## Rate Limiting

### Online Search Limits

- **Daily Limit**: 5 searches per user per day
- **Consent Required**: Users must explicitly consent to online search
- **Automatic Reset**: Limits reset daily at midnight UTC

### Memory Storage Limits

- **No Hard Limits**: Memory storage is not rate limited
- **Consent Required**: Sensitive information requires user consent
- **Automatic Cleanup**: Old memories are automatically cleaned up

## Security Considerations

### Access Control

- All memory operations are scoped to the requesting user
- Cross-user access is strictly prohibited
- Device fingerprinting provides additional security

### Data Protection

- Sensitive memories are encrypted at rest
- Embeddings do not contain raw sensitive data
- Consent is required for storing private information

### Audit Logging

- All memory operations are logged
- Consent changes are tracked
- Search activities are monitored

## Performance Considerations

### Vector Search

- Embeddings are cached for performance
- Similarity search is optimized with indexes
- Results are limited to prevent performance issues

### Background Processing

- Memory extraction is asynchronous when possible
- Cleanup operations run during off-peak hours
- Search operations are throttled to prevent API abuse

## Integration Examples

### Chat Integration
```csharp
// In chat processing
var memories = await memoryExtractor.ExtractMemoriesAsync(messages, userId, deviceFingerprint);
foreach (var memory in memories)
{
    await memoryService.StoreMemoryAsync(memory);
}

var systemPrompt = await memoryService.GenerateSystemPromptAsync(userId, context);
```

### Search Integration
```csharp
// In search handling
if (await onlineSearchService.CanSearchTodayAsync(userId))
{
    var results = await onlineSearchService.SearchForUserAsync(userId, query, deviceFingerprint);
    // Process results
}
```
