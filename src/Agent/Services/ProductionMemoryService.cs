using Agent.Configuration;
using Agent.Data;
using Agent.Models;
using Agent.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using System.Text.Json;

namespace Agent.Services;

/// <summary>
/// Production-ready memory service with identity management and embedding-based retrieval
/// </summary>
public class ProductionMemoryService : IProductionMemoryService
{
    private readonly MemoryDbContext _context;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingService;
    private readonly ILogger<ProductionMemoryService> _logger;
    private readonly LiteLLMOptions _llmOptions;

    #region Configuration Constants

    // Similarity Thresholds (industry-standard from research)
    private const double IDENTITY_THRESHOLD = 0.90;     // Identity/preferences: cosine ≥ 0.90
    private const double FACTS_THRESHOLD = 0.85;        // Facts: ≥ 0.85
    private const double CONTEXT_THRESHOLD = 0.80;      // Episodic context: ≥ 0.80
    private const double DUPLICATE_THRESHOLD = 0.95;    // Very high similarity indicates duplicate

    // Time-to-Live (TTL) Configuration
    private const int TTL_DAYS_IDENTITY = 365;          // Keep identity info for 1 year
    private const int TTL_DAYS_FACTS = 180;             // Keep facts for 6 months
    private const int TTL_DAYS_CONTEXT = 30;            // Keep context for 1 month

    // Memory Decay and Cleanup - Type-specific decay rates
    private const double IDENTITY_DECAY_RATE = 0.999;   // Very slow decay for identity (0.1% daily)
    private const double FACTS_DECAY_RATE = 0.998;      // Slow decay for facts (0.2% daily)
    private const double CONTEXT_DECAY_RATE = 0.995;    // Moderate decay for context (0.5% daily)
    private const double MIN_SCORE_THRESHOLD = 0.05;    // Lower threshold to respect TTL

    #endregion

    public ProductionMemoryService(
        MemoryDbContext context,
        IEmbeddingGenerator<string, Embedding<float>> embeddingService,
        IOptions<LiteLLMOptions> llmOptions,
        ILogger<ProductionMemoryService> logger)
    {
        _context = context;
        _embeddingService = embeddingService;
        _llmOptions = llmOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Re-embed a memory entry in place if its stored vector dimensionality or provider
    /// does not match the active LiteLLM configuration. Returns the up-to-date float[] or
    /// null if the entry has no source content to re-embed from.
    /// Caller is responsible for calling SaveChangesAsync once per batch.
    /// </summary>
    private async Task<float[]?> EnsureCurrentEmbeddingAsync(MemoryEntry memory, CancellationToken ct = default)
    {
        var isCurrent = memory.EmbeddingDim == _llmOptions.EmbeddingDimensions
                        && string.Equals(memory.EmbeddingProvider, "litellm", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrEmpty(memory.EmbeddingVector);

        if (isCurrent)
        {
            return JsonSerializer.Deserialize<float[]>(memory.EmbeddingVector!);
        }

        if (string.IsNullOrWhiteSpace(memory.Content))
        {
            return null;
        }

        var embeddings = await _embeddingService.GenerateAsync(new[] { memory.Content }, cancellationToken: ct);
        var vector = embeddings.First().Vector.ToArray();

        memory.EmbeddingVector = JsonSerializer.Serialize(vector);
        memory.EmbeddingProvider = "litellm";
        memory.EmbeddingModelId = _llmOptions.EmbedModel;
        memory.EmbeddingDim = _llmOptions.EmbeddingDimensions;

        _logger.LogInformation("Re-embedded stale memory {Id} ({OldDim}→{NewDim} dims)", memory.Id, memory.EmbeddingDim, _llmOptions.EmbeddingDimensions);
        return vector;
    }

    /// <summary>
    /// Generate system prompt with memory context in specified format
    /// </summary>
    public async Task<string> GenerateSystemPromptAsync(string? deviceFingerprint = null, string? baseSystemPrompt = null)
    {
        try
        {
            // Use provided base prompt or load from file
            string systemPrompt;
            if (!string.IsNullOrEmpty(baseSystemPrompt))
            {
                systemPrompt = baseSystemPrompt;
            }
            else
            {
                var systemPromptPath = Environment.GetEnvironmentVariable("ISA_SYSTEM_PROMPT_PATH") ?? "systemprompt.txt";
                systemPrompt = await File.ReadAllTextAsync(systemPromptPath);
            }

            if (string.IsNullOrEmpty(deviceFingerprint))
            {
                return systemPrompt; // Return base prompt if no device info
            }

            var user = await GetOrCreateUserAsync(deviceFingerprint);
            // Note: GetOrCreateUserAsync always returns a user, so no null check needed

            // Get recent memories for context
            var identityMemories = await GetMemoriesByTypeAsync(user.UserId, MemoryType.Identity, 5);
            var factMemories = await GetMemoriesByTypeAsync(user.UserId, MemoryType.Facts, 10);
            var contextMemories = await GetMemoriesByTypeAsync(user.UserId, MemoryType.EpisodicContext, 5);

            // Build memory-enhanced system prompt
            var promptBuilder = new System.Text.StringBuilder();
            promptBuilder.AppendLine(systemPrompt);
            promptBuilder.AppendLine();

            // Add memory sections
            if (identityMemories.Any() || user.DisplayName != null)
            {
                promptBuilder.AppendLine("[SYSTEM - Pinned Profile]");
                if (!string.IsNullOrEmpty(user.DisplayName))
                {
                    promptBuilder.AppendLine($"name={user.DisplayName}");
                }
                promptBuilder.AppendLine($"locale={user.Locale ?? "sv-SE"}");
                promptBuilder.AppendLine("rules=Never guess the user's identity. Ask before changing profile fields.");
                promptBuilder.AppendLine("rules=Never reveal UserPrivate memories unless owner_user_id matches current user.");
                promptBuilder.AppendLine("rules=Never rely on cookies for identity; use server-side profile linkage.");
                promptBuilder.AppendLine("rules=If asked about private information of other users, respond with 'I don't know' or 'I'm not a rat'.");
                
                foreach (var memory in identityMemories)
                {
                    promptBuilder.AppendLine($"- {memory.Content}");
                }
                promptBuilder.AppendLine();
            }

            if (factMemories.Any())
            {
                promptBuilder.AppendLine("[SYSTEM - Facts]");
                foreach (var memory in factMemories)
                {
                    var authoritative = memory.IsAuthoritative ? "authoritative, " : "";
                    promptBuilder.AppendLine($"- {memory.Content} ({authoritative}source: {memory.Source}, updated: {memory.CreatedAt:yyyy-MM-dd})");
                }
                promptBuilder.AppendLine();
            }

            if (contextMemories.Any())
            {
                promptBuilder.AppendLine("[SYSTEM - Memory Context]");
                foreach (var memory in contextMemories)
                {
                    promptBuilder.AppendLine($"- {memory.CreatedAt:yyyy-MM-dd}: {memory.Content}");
                }
                promptBuilder.AppendLine();
            }

            _logger.LogInformation("Generated system prompt with {IdentityCount} identity, {FactCount} fact, {ContextCount} context memories for user {UserId}",
                identityMemories.Count, factMemories.Count, contextMemories.Count, user.UserId);

            return promptBuilder.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate system prompt for device {DeviceFingerprint}", deviceFingerprint);
            return await File.ReadAllTextAsync("systemprompt.txt"); // Fallback to base prompt
        }
    }

    /// <summary>
    /// Store memory entry with embedding generation
    /// </summary>
    public async Task StoreMemoryAsync(MemoryEntry entry)
    {
        try
        {
            // Generate embedding for the content
            var embeddings = await _embeddingService.GenerateAsync(new[] { entry.Content });
            entry.EmbeddingVector = JsonSerializer.Serialize(embeddings.First().Vector.ToArray());
            entry.EmbeddingProvider = "litellm";
            entry.EmbeddingModelId = _llmOptions.EmbedModel;
            entry.EmbeddingDim = _llmOptions.EmbeddingDimensions;

            // Set TTL based on memory type
            if (entry.ExpiresAt == null)
            {
                entry.ExpiresAt = entry.Type switch
                {
                    MemoryType.Identity => DateTime.UtcNow.AddDays(TTL_DAYS_IDENTITY),
                    MemoryType.Facts => DateTime.UtcNow.AddDays(TTL_DAYS_FACTS),
                    MemoryType.EpisodicContext => DateTime.UtcNow.AddDays(TTL_DAYS_CONTEXT),
                    _ => DateTime.UtcNow.AddDays(TTL_DAYS_CONTEXT)
                };
            }

            // Check for duplicates using embedding similarity
            var existingMemories = await GetMemoriesByTypeAsync(entry.OwnerUserId, entry.Type, 50);
            var isDuplicate = await CheckForDuplicateAsync(entry, existingMemories);
            
            if (isDuplicate)
            {
                _logger.LogInformation("Skipping duplicate memory: {Content}", entry.Content[..Math.Min(50, entry.Content.Length)]);
                return;
            }

            // Add to database
            _context.Set<MemoryEntry>().Add(entry);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Stored {Type} memory: {Content} for user {UserId}", 
                entry.Type, entry.Content[..Math.Min(50, entry.Content.Length)], entry.OwnerUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store memory: {Content}", entry.Content[..Math.Min(50, entry.Content.Length)]);
            throw;
        }
    }

    /// <summary>
    /// Retrieve memories using similarity search with access control
    /// </summary>
    public async Task<List<MemoryEntry>> RetrieveMemoriesAsync(
        string query,
        MemoryType type,
        double threshold,
        int topK = 5,
        string? userId = null)
    {
        try
        {
            // SECURITY: Additional protection against cross-user information requests
            // Check if the query is asking for information about someone else by name
            var potentialNames = ExtractNamesFromQuery(query);
            if (potentialNames.Any())
            {
                _logger.LogWarning("SECURITY: Query contains potential names, applying strict privacy filtering: {Query}", 
                    query[..Math.Min(50, query.Length)]);
                
                // For queries mentioning names, only return PUBLIC memories or user's OWN memories
                var strictFiltering = true;
                return await RetrieveMemoriesWithStrictPrivacyAsync(query, type, threshold, topK, userId, strictFiltering);
            }

            // Generate query embedding
            var queryEmbeddings = await _embeddingService.GenerateAsync(new[] { query });
            var queryEmbedding = queryEmbeddings.First();

            // Get memories by type with access control
            var memories = await GetMemoriesByTypeAsync(userId, type, 100);

            // Calculate similarities and filter with privacy controls (re-embeds stale rows on the fly)
            var results = await CalculateSimilarityScoresAsync(queryEmbedding.Vector.ToArray(), memories, threshold, userId);

            // Sort by similarity and take top K
            var topResults = results
                .OrderByDescending(r => r.Similarity)
                .Take(topK)
                .Select(r => r.Memory)
                .ToList();

            _logger.LogInformation("Retrieved {Count} {Type} memories for query: {Query} (user: {UserId})",
                topResults.Count, type, query[..Math.Min(30, query.Length)], userId ?? "anonymous");

            return topResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve memories for query: {Query}", query[..Math.Min(30, query.Length)]);
            return new List<MemoryEntry>();
        }
    }

    /// <summary>
    /// SECURITY: Extract potential names from query to detect cross-user information requests
    /// </summary>
    private List<string> ExtractNamesFromQuery(string query)
    {
        var names = new List<string>();
        
        // Pattern to detect Swedish names (capitalized words that could be names)
        var namePatterns = new[]
        {
            @"(?:vad heter|vet du.*|var bor|var bodde)\s+([A-ZÅÄÖ][a-zåäö]+)",
            @"\b([A-ZÅÄÖ][a-zåäö]+(?:\s+[A-ZÅÄÖ][a-zåäö]+)*)\s+(?:i mellannamn|heter|bor|bodde)",
            @"information om\s+([A-ZÅÄÖ][a-zåäö]+)",
            @"([A-ZÅÄÖ][a-zåäö]+)'s\s+(?:mellannamn|namn|hemlighet)"
        };

        foreach (var pattern in namePatterns)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(query, pattern, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    var name = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(name) && name.Length > 1)
                    {
                        names.Add(name);
                    }
                }
            }
        }

        return names.Distinct().ToList();
    }

    /// <summary>
    /// SECURITY: Strict privacy filtering for queries that mention names
    /// </summary>
    private async Task<List<MemoryEntry>> RetrieveMemoriesWithStrictPrivacyAsync(
        string query,
        MemoryType type,
        double threshold,
        int topK,
        string? userId,
        bool strictFiltering)
    {
        try
        {
            // Generate query embedding
            var queryEmbeddings = await _embeddingService.GenerateAsync(new[] { query });
            var queryEmbedding = queryEmbeddings.First();

            // Get only PUBLIC memories when strict filtering is enabled
            var memories = await _context.Set<MemoryEntry>()
                .Where(m => m.Type == type && 
                           m.TombstonedAt == null &&
                           (m.Scope == MemoryScope.Public || 
                            m.Confidentiality == Confidentiality.Public ||
                            (userId != null && m.OwnerUserId == userId))) // Only user's own memories
                .OrderByDescending(m => m.Score)
                .Take(100)
                .ToListAsync();

            var results = new List<(MemoryEntry Memory, double Similarity)>();

            var hasReembeds = false;
            foreach (var memory in memories)
            {
                // STRICT: Never reveal private memories about other users
                if (memory.Confidentiality == Confidentiality.Private &&
                    memory.OwnerUserId != userId)
                {
                    _logger.LogWarning("SECURITY: Blocked private memory access in strict mode for user {UserId}", userId);
                    continue;
                }

                var beforeDim = memory.EmbeddingDim;
                var memoryEmbedding = await EnsureCurrentEmbeddingAsync(memory);
                if (memoryEmbedding == null) continue;
                if (memory.EmbeddingDim != beforeDim) hasReembeds = true;

                var similarity = CalculateCosineSimilarity(queryEmbedding.Vector.ToArray(), memoryEmbedding);

                if (similarity >= threshold)
                {
                    results.Add((memory, similarity));
                }
            }

            if (hasReembeds)
            {
                await _context.SaveChangesAsync();
            }

            var filteredResults = results
                .OrderByDescending(r => r.Similarity)
                .Take(topK)
                .Select(r => r.Memory)
                .ToList();

            _logger.LogWarning("SECURITY: Applied strict privacy filtering, returned {Count} memories for query: {Query}", 
                filteredResults.Count, query[..Math.Min(30, query.Length)]);

            return filteredResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve memories with strict privacy filtering");
            return new List<MemoryEntry>();
        }
    }

    /// <summary>
    /// Get or create user based on device fingerprint
    /// </summary>
    public async Task<User> GetOrCreateUserAsync(string deviceFingerprint)
    {
        _logger.LogInformation("🔍 DEBUG: GetOrCreateUserAsync called with device fingerprint: {DeviceFingerprint}", deviceFingerprint);

        // Get or create device with thread-safe upsert
        var device = await GetOrCreateDeviceAsync(deviceFingerprint);

        device.LastSeen = DateTime.UtcNow;

        // Get primary user for device - prioritize users with memories over just confidence
        var deviceUsers = await _context.Set<DeviceUser>()
            .Include(du => du.User)
            .Where(du => du.DeviceId == deviceFingerprint)
            .ToListAsync();

        DeviceUser? deviceUser = null;
        if (deviceUsers.Any())
        {
            // First, try to find a user that has memories (prioritize memories over confidence)
            foreach (var du in deviceUsers.OrderByDescending(du => du.LastSeen))
            {
                var hasMemories = await _context.Set<MemoryEntry>()
                    .AnyAsync(m => m.OwnerUserId == du.UserId);

                if (hasMemories)
                {
                    deviceUser = du;
                    _logger.LogInformation("DEVICE_USER: Selected user {UserId} with memories for device {DeviceId}",
                        du.UserId, deviceFingerprint);
                    break;
                }
            }

            // If no user has memories, fall back to most recently seen user (not highest confidence)
            if (deviceUser == null)
            {
                deviceUser = deviceUsers.OrderByDescending(du => du.LastSeen).First();
                _logger.LogInformation("DEVICE_USER: No users with memories found, selected most recent user {UserId} for device {DeviceId}",
                    deviceUser.UserId, deviceFingerprint);
            }
        }

        User user;
        if (deviceUser?.User != null)
        {
            user = deviceUser.User;
            user.LastSeenAt = DateTime.UtcNow;
        }
        else
        {
            // Create new user
            user = new User();
            _context.Set<User>().Add(user);
            
            // Link device to user
            var newDeviceUser = new DeviceUser
            {
                DeviceId = deviceFingerprint,
                UserId = user.UserId,
                Confidence = 1.0
            };
            _context.Set<DeviceUser>().Add(newDeviceUser);

            // Set as primary user for device
            device.PrimaryUserId = user.UserId;
        }

        await _context.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Find user by display name (for name-based identification)
    /// PRIVACY CONTROL: Only searches public memories and user display names
    /// </summary>
    public async Task<User?> FindUserByNameAsync(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return null;

        // First try exact match on DisplayName
        var user = await _context.Set<User>()
            .FirstOrDefaultAsync(u => u.DisplayName == displayName);

        if (user != null)
            return user;

        // CRITICAL FIX: Search ALL identity memories to find existing users
        // This is essential for user ID reuse when users return
        // We search all identity memories but only return the owner's information
        var identityMemories = await _context.Set<MemoryEntry>()
            .Where(m => m.Type == MemoryType.Identity &&
                       m.Content.Contains(displayName) &&
                       m.TombstonedAt == null)
            .ToListAsync();

        // Find the user who owns the memory containing this name
        // SECURITY NOTE: This is safe because we only return the user ID of the memory owner,
        // not the actual memory content. This enables proper user ID reuse without data leakage.
        foreach (var memory in identityMemories)
        {
            if (!string.IsNullOrEmpty(memory.OwnerUserId))
            {
                user = await _context.Set<User>()
                    .FirstOrDefaultAsync(u => u.UserId == memory.OwnerUserId);

                if (user != null)
                {
                    // Update the user's DisplayName if it's not set
                    if (string.IsNullOrEmpty(user.DisplayName))
                    {
                        user.DisplayName = displayName;
                        await _context.SaveChangesAsync();
                    }
                    return user;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Update user information
    /// </summary>
    public async Task UpdateUserAsync(User user)
    {
        _context.Set<User>().Update(user);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    public async Task<User?> GetUserByIdAsync(string userId)
    {
        return await _context.Set<User>()
            .FirstOrDefaultAsync(u => u.UserId == userId);
    }

    /// <summary>
    /// Get memory count for user (for debugging)
    /// </summary>
    public async Task<int> GetMemoryCountForUserAsync(string userId)
    {
        return await _context.Set<MemoryEntry>()
            .Where(m => m.OwnerUserId == userId && m.TombstonedAt == null)
            .CountAsync();
    }

    /// <summary>
    /// Link device to existing user (for name-based identification)
    /// </summary>
    public async Task LinkDeviceToUserAsync(string deviceFingerprint, string userId)
    {
        // Get or create device with thread-safe upsert
        var device = await GetOrCreateDeviceAsync(deviceFingerprint);

        device.LastSeen = DateTime.UtcNow;
        device.PrimaryUserId = userId;

        // Check if DeviceUser relationship already exists
        var existingDeviceUser = await _context.Set<DeviceUser>()
            .FirstOrDefaultAsync(du => du.DeviceId == deviceFingerprint && du.UserId == userId);

        if (existingDeviceUser == null)
        {
            // Create new DeviceUser relationship
            var deviceUser = new DeviceUser
            {
                DeviceId = deviceFingerprint,
                UserId = userId,
                Confidence = 0.8 // Lower confidence since identified by name, not device history
            };
            _context.Set<DeviceUser>().Add(deviceUser);
        }
        else
        {
            // Update existing relationship
            existingDeviceUser.LastSeen = DateTime.UtcNow;
            existingDeviceUser.Confidence = Math.Min(1.0, existingDeviceUser.Confidence + 0.1); // Increase confidence
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Consolidate device fingerprints when the same user is identified from multiple devices
    /// This replaces the current device fingerprint with an existing one that has more data
    /// </summary>
    public async Task<string?> ConsolidateDeviceFingerprintAsync(string currentDeviceFingerprint, string userId)
    {
        try
        {
            // Find all devices associated with this user
            var userDevices = await _context.Set<DeviceUser>()
                .Include(du => du.Device)
                .Where(du => du.UserId == userId)
                .OrderByDescending(du => du.Confidence)
                .ThenByDescending(du => du.LastSeen)
                .ToListAsync();

            if (userDevices.Count <= 1)
            {
                _logger.LogInformation("CONSOLIDATION: User {UserId} has only one device, no consolidation needed", userId);
                return null; // No consolidation needed
            }

            // Find the device with the most memories (best candidate for consolidation)
            DeviceUser? bestDevice = null;
            int maxMemories = 0;

            foreach (var deviceUser in userDevices)
            {
                var memoryCount = await _context.Set<MemoryEntry>()
                    .CountAsync(m => m.OwnerUserId == userId && m.TombstonedAt == null);

                if (memoryCount > maxMemories)
                {
                    maxMemories = memoryCount;
                    bestDevice = deviceUser;
                }
            }

            // If current device is already the best, no consolidation needed
            if (bestDevice?.DeviceId == currentDeviceFingerprint)
            {
                _logger.LogInformation("CONSOLIDATION: Current device {DeviceId} is already the best for user {UserId}",
                    currentDeviceFingerprint, userId);
                return null;
            }

            // If we found a better device, return its fingerprint for consolidation
            if (bestDevice != null && maxMemories > 0)
            {
                _logger.LogInformation("CONSOLIDATION: Recommending device fingerprint consolidation for user {UserId}: {CurrentDevice} → {BestDevice} (has {MemoryCount} memories)",
                    userId, currentDeviceFingerprint, bestDevice.DeviceId, maxMemories);
                return bestDevice.DeviceId;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to consolidate device fingerprint for user {UserId}", userId);
            return null;
        }
    }

    /// <summary>
    /// Check if user has given consent
    /// </summary>
    public async Task<bool> HasConsentAsync(string userId, string consentKind)
    {
        var consent = await _context.Set<Consent>()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Kind == consentKind);
        
        return consent?.Granted ?? false;
    }

    /// <summary>
    /// Update user consent
    /// </summary>
    public async Task UpdateConsentAsync(string userId, string consentKind, bool granted)
    {
        var consent = await _context.Set<Consent>()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Kind == consentKind);

        if (consent == null)
        {
            consent = new Consent
            {
                UserId = userId,
                Kind = consentKind,
                Granted = granted
            };
            _context.Set<Consent>().Add(consent);
        }
        else
        {
            consent.Granted = granted;
            consent.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Get or create user runtime state
    /// </summary>
    public async Task<UserRuntimeState> GetOrCreateUserRuntimeStateAsync(string userId)
    {
        var state = await _context.Set<UserRuntimeState>()
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (state == null)
        {
            state = new UserRuntimeState { UserId = userId };
            _context.Set<UserRuntimeState>().Add(state);
            await _context.SaveChangesAsync();
        }

        return state;
    }

    /// <summary>
    /// Update user runtime state
    /// </summary>
    public async Task UpdateUserRuntimeStateAsync(UserRuntimeState state)
    {
        _context.Set<UserRuntimeState>().Update(state);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Get all users who have given consent for a specific operation
    /// </summary>
    public async Task<List<User>> GetUsersWithConsentAsync(string consentKind)
    {
        try
        {
            var usersWithConsent = await _context.Set<Consent>()
                .Where(c => c.Kind == consentKind && c.Granted)
                .Include(c => c.User)
                .Select(c => c.User)
                .ToListAsync();

            _logger.LogDebug("Found {Count} users with consent for {ConsentKind}",
                usersWithConsent.Count, consentKind);

            return usersWithConsent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get users with consent for {ConsentKind}", consentKind);
            return new List<User>();
        }
    }

    /// <summary>
    /// Consolidate and cleanup expired memories
    /// </summary>
    public async Task ConsolidateAndCleanupAsync()
    {
        try
        {
            var now = DateTime.UtcNow;

            // Apply daily decay
            var memories = await _context.Set<MemoryEntry>()
                .Where(m => m.TombstonedAt == null)
                .ToListAsync();

            foreach (var memory in memories)
            {
                var daysOld = (now - memory.CreatedAt).TotalDays;

                // Apply type-specific decay rates
                var decayRate = memory.Type switch
                {
                    MemoryType.Identity => IDENTITY_DECAY_RATE,
                    MemoryType.Facts => FACTS_DECAY_RATE,
                    MemoryType.EpisodicContext => CONTEXT_DECAY_RATE,
                    _ => CONTEXT_DECAY_RATE
                };

                memory.Score *= Math.Pow(decayRate, daysOld);
            }

            // Remove memories below threshold or expired - but respect TTL first
            var toRemove = memories
                .Where(m => (m.ExpiresAt.HasValue && m.ExpiresAt.Value < now) ||
                           (m.Score < MIN_SCORE_THRESHOLD && (!m.ExpiresAt.HasValue || m.ExpiresAt.Value < now)))
                .ToList();

            foreach (var memory in toRemove)
            {
                memory.TombstonedAt = now;
                _logger.LogInformation("Tombstoned memory: {Content} (Type: {Type}, Score: {Score}, Expired: {Expired})",
                    memory.Content[..Math.Min(50, memory.Content.Length)],
                    memory.Type,
                    memory.Score,
                    memory.ExpiresAt.HasValue && memory.ExpiresAt.Value < now);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Applied decay to {Count} memories, tombstoned {RemovedCount} expired memories",
                memories.Count, toRemove.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to consolidate and cleanup memories");
        }
    }

    #region Private Helper Methods

    private async Task<User?> GetUserByDeviceAsync(string deviceFingerprint)
    {
        var deviceUser = await _context.Set<DeviceUser>()
            .Include(du => du.User)
            .Where(du => du.DeviceId == deviceFingerprint)
            .OrderByDescending(du => du.Confidence)
            .FirstOrDefaultAsync();

        return deviceUser?.User;
    }

    private async Task<List<MemoryEntry>> GetMemoriesByTypeAsync(string? userId, MemoryType type, int limit)
    {
        var query = _context.Set<MemoryEntry>()
            .Where(m => m.Type == type && m.TombstonedAt == null);

        if (!string.IsNullOrEmpty(userId))
        {
            // User can access their own private memories + public memories
            query = query.Where(m =>
                (m.OwnerUserId == userId) ||
                (m.Scope == MemoryScope.Public) ||
                (m.Confidentiality == Confidentiality.Public));
        }
        else
        {
            // Anonymous users can only access public memories
            query = query.Where(m =>
                m.Scope == MemoryScope.Public ||
                m.Confidentiality == Confidentiality.Public);
        }

        return await query
            .OrderByDescending(m => m.Score)
            .Take(limit)
            .ToListAsync();
    }

    private async Task<bool> CheckForDuplicateAsync(MemoryEntry newEntry, List<MemoryEntry> existingMemories)
    {
        if (string.IsNullOrEmpty(newEntry.EmbeddingVector)) return false;

        var newEmbedding = JsonSerializer.Deserialize<float[]>(newEntry.EmbeddingVector);
        if (newEmbedding == null) return false;

        var hasReembeds = false;
        foreach (var existing in existingMemories)
        {
            var beforeDim = existing.EmbeddingDim;
            var existingEmbedding = await EnsureCurrentEmbeddingAsync(existing);
            if (existingEmbedding == null) continue;
            if (existing.EmbeddingDim != beforeDim) hasReembeds = true;

            var similarity = CalculateCosineSimilarity(newEmbedding, existingEmbedding);
            if (similarity > DUPLICATE_THRESHOLD)
            {
                if (hasReembeds) await _context.SaveChangesAsync();
                return true;
            }
        }

        if (hasReembeds) await _context.SaveChangesAsync();
        return false;
    }

    private static double CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
            return 0.0;

        double dotProduct = 0.0;
        double magnitudeA = 0.0;
        double magnitudeB = 0.0;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            magnitudeA += vectorA[i] * vectorA[i];
            magnitudeB += vectorB[i] * vectorB[i];
        }

        magnitudeA = Math.Sqrt(magnitudeA);
        magnitudeB = Math.Sqrt(magnitudeB);

        if (magnitudeA == 0.0 || magnitudeB == 0.0)
            return 0.0;

        return dotProduct / (magnitudeA * magnitudeB);
    }

    /// <summary>
    /// Get or create device with proper race condition handling
    /// </summary>
    private async Task<Device> GetOrCreateDeviceAsync(string deviceFingerprint)
    {
        var device = await _context.Set<Device>()
            .FirstOrDefaultAsync(d => d.DeviceId == deviceFingerprint);

        if (device == null)
        {
            device = new Device { DeviceId = deviceFingerprint };
            _context.Set<Device>().Add(device);

            // Try to save device first to handle race conditions
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("UNIQUE constraint failed: Devices.DeviceId") == true)
            {
                // Another thread created the device, reload it
                _context.Entry(device).State = EntityState.Detached;
                device = await _context.Set<Device>()
                    .FirstOrDefaultAsync(d => d.DeviceId == deviceFingerprint);

                if (device == null)
                {
                    throw new InvalidOperationException($"Device {deviceFingerprint} should exist but was not found after constraint violation");
                }
            }
        }

        return device;
    }

    /// <summary>
    /// Calculate similarity scores for memories with privacy filtering. Re-embeds stale
    /// rows on the fly so vectors created by the previous embedding provider stay searchable.
    /// </summary>
    private async Task<List<(MemoryEntry Memory, double Similarity)>> CalculateSimilarityScoresAsync(
        float[] queryEmbedding,
        List<MemoryEntry> memories,
        double threshold,
        string? userId)
    {
        var results = new List<(MemoryEntry Memory, double Similarity)>();
        var hasReembeds = false;

        foreach (var memory in memories)
        {
            if (memory.Confidentiality == Confidentiality.Private &&
                memory.OwnerUserId != userId)
            {
                continue;
            }

            var beforeDim = memory.EmbeddingDim;
            var memoryEmbedding = await EnsureCurrentEmbeddingAsync(memory);
            if (memoryEmbedding == null) continue;
            if (memory.EmbeddingDim != beforeDim) hasReembeds = true;

            var similarity = CalculateCosineSimilarity(queryEmbedding, memoryEmbedding);
            if (similarity >= threshold)
            {
                results.Add((memory, similarity));
            }
        }

        if (hasReembeds)
        {
            await _context.SaveChangesAsync();
        }

        return results;
    }

    #endregion
}