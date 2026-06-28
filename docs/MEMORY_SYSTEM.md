# Memory System Documentation

## Overview

The AccountingAgent memory system provides intelligent, persistent memory capabilities that allow the AI assistant to remember information across conversations while respecting user privacy and consent.

## Key Features

- **Intelligent Memory Extraction**: Automatically identifies and extracts relevant information from conversations
- **Privacy-First Design**: Requires explicit user consent for storing sensitive information
- **Multi-User Support**: Isolates memories between different users with strict access control
- **Online Search Integration**: Performs daily searches with user consent to stay updated
- **Semantic Search**: Uses vector embeddings for intelligent memory retrieval
- **Background Maintenance**: Automatic cleanup and optimization of stored memories

## Architecture

### Core Components

1. **ProductionMemoryService** (`IProductionMemoryService`)
   - Central memory management service
   - Handles storage, retrieval, and access control
   - Manages user consent and privacy settings

2. **IntelligentMemoryExtractor** (`IIntelligentMemoryExtractor`)
   - Extracts relevant information from conversations
   - Categorizes memories by type (Facts, Identity, Secrets, etc.)
   - Determines privacy levels and consent requirements

3. **OnlineSearchService** (`IOnlineSearchService`)
   - Manages online search functionality with consent
   - Enforces daily search limits
   - Integrates with GoogleSearchPlugin for web searches

4. **Background Services**
   - `MemoryMaintenanceService`: Cleanup and optimization
   - `DailyOnlineSearchService`: Automatic daily searches

### Database Schema

#### Core Tables

- **Users**: User identification and device fingerprinting
- **Memories**: Stored memory content with embeddings
- **Consents**: User consent tracking for different operations
- **UserRuntimeStates**: Session and runtime information

#### Memory Types

- `Facts`: Factual information about users or topics
- `Identity`: Personal identification information
- `Secrets`: Sensitive information requiring special handling
- `Episodic`: Contextual information about conversations
- `Preferences`: User preferences and settings

## Usage Examples

### Basic Memory Storage

```csharp
// Extract and store memories from conversation
var memories = await memoryExtractor.ExtractMemoriesAsync(messages, userId, deviceFingerprint);
foreach (var memory in memories)
{
    await memoryService.StoreMemoryAsync(memory);
}
```

### Memory Retrieval

```csharp
// Retrieve relevant memories for context
var relevantMemories = await memoryService.RetrieveMemoriesAsync(
    query: "user preferences", 
    memoryType: MemoryType.Preferences,
    threshold: 0.7,
    maxResults: 10,
    userId: userId
);
```

### Consent Management

```csharp
// Check if user has given consent for memory storage
var hasConsent = await memoryService.HasConsentAsync(userId, "memory_storage");

// Request consent for online search
var consentMessage = await onlineSearchService.RequestOnlineSearchConsentAsync(userId);
```

## Privacy and Security

### Access Control

- **User Isolation**: Memories are strictly isolated between users
- **Device Fingerprinting**: Users are identified by device fingerprints
- **Consent-Based Storage**: Sensitive information requires explicit consent

### Data Protection

- **Encryption**: Sensitive data is encrypted at rest
- **Retention Policies**: Automatic cleanup of old or irrelevant memories
- **Audit Logging**: All memory operations are logged for transparency

### Privacy Principles

1. **Explicit Consent**: Users must consent to memory storage
2. **Data Minimization**: Only relevant information is stored
3. **User Control**: Users can view and delete their memories
4. **Transparency**: Clear communication about what is remembered

## Configuration

### Environment Variables

```bash
# Database connection
ConnectionStrings__DefaultConnection="Server=..."

# OpenAI API for embeddings
OpenAI__ApiKey="sk-..."

# Google Search API
GoogleSearch__ApiKey="..."
GoogleSearch__SearchEngineId="..."
```

### Service Registration

```csharp
// In Program.cs or ServiceRegistrationExtensions.cs
services.AddScoped<IProductionMemoryService, ProductionMemoryService>();
services.AddScoped<IIntelligentMemoryExtractor, IntelligentMemoryExtractor>();
services.AddScoped<IOnlineSearchService, OnlineSearchService>();
services.AddHostedService<MemoryMaintenanceService>();
services.AddHostedService<DailyOnlineSearchService>();
```

## Testing

### Unit Tests

The memory system includes comprehensive unit tests covering:

- Memory extraction and categorization
- Storage and retrieval operations
- Consent management
- Access control and privacy
- Online search functionality

Run tests with:
```bash
dotnet test tests/Agent.Tests/Agent.Tests.csproj --filter "FullyQualifiedName~Memory"
```

### E2E Tests

End-to-end tests validate:

- Multi-user memory isolation
- Memory retention across sessions
- Consent workflows
- Privacy protection

Run E2E tests with:
```bash
dotnet test tests/Agent.Tests/Agent.Tests.csproj --filter "MemorySystemE2ETests"
```

## Monitoring and Maintenance

### Background Services

1. **Memory Maintenance** (Daily at 2 AM)
   - Removes expired memories
   - Optimizes vector embeddings
   - Cleans up orphaned data

2. **Daily Online Search** (Daily at 10 AM)
   - Performs searches for consenting users
   - Respects daily search limits
   - Updates user knowledge base

### Metrics and Logging

- Memory storage and retrieval performance
- Consent grant/deny rates
- Search usage statistics
- Error rates and system health

## Troubleshooting

### Common Issues

1. **Memory Not Being Stored**
   - Check user consent status
   - Verify memory extraction is working
   - Check database connectivity

2. **Poor Memory Retrieval**
   - Adjust similarity thresholds
   - Check embedding quality
   - Verify query formulation

3. **Online Search Not Working**
   - Verify API keys and configuration
   - Check user consent for online search
   - Ensure daily limits not exceeded

### Debug Commands

```bash
# Check memory service health
curl https://localhost:7042/health

# View user memories (development only)
curl https://localhost:7042/api/memories/user/{userId}

# Check consent status
curl https://localhost:7042/api/consent/{userId}/{consentKind}
```

## Future Enhancements

- **Federated Learning**: Share anonymized insights across users
- **Advanced Categorization**: More sophisticated memory classification
- **Temporal Reasoning**: Better understanding of time-based context
- **Multi-Modal Memories**: Support for images and documents
- **Collaborative Memories**: Shared memories for team contexts

## Contributing

When contributing to the memory system:

1. Follow privacy-first design principles
2. Add comprehensive tests for new features
3. Update documentation for API changes
4. Consider performance implications of new features
5. Ensure backward compatibility where possible

## License

This memory system is part of the AccountingAgent project and follows the same licensing terms.
