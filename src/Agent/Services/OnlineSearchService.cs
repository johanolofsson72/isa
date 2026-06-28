using Microsoft.SemanticKernel;
using Agent.Services.Interfaces;
using Agent.Models;

namespace Agent.Services;

/// <summary>
/// Service for managing online search with user consent and daily limits
/// </summary>
public class OnlineSearchService : IOnlineSearchService
{
    private readonly IProductionMemoryService _memoryService;
    private readonly Kernel _kernel;
    private readonly ILogger<OnlineSearchService> _logger;

    private const string ONLINE_SEARCH_CONSENT = "online_search";
    private const string DAILY_SEARCH_LIMIT = "daily_search_limit";
    private const int MAX_DAILY_SEARCHES = 1; // One search per user per day

    public OnlineSearchService(
        IProductionMemoryService memoryService,
        Kernel kernel,
        ILogger<OnlineSearchService> logger)
    {
        _memoryService = memoryService;
        _kernel = kernel;
        _logger = logger;
    }

    /// <summary>
    /// Check if user can perform online search today
    /// </summary>
    public async Task<bool> CanSearchTodayAsync(string userId)
    {
        try
        {
            // Check if user has given consent
            var hasConsent = await _memoryService.HasConsentAsync(userId, ONLINE_SEARCH_CONSENT);
            if (!hasConsent)
            {
                _logger.LogDebug("User {UserId} has not given consent for online search", userId);
                return false;
            }

            // Check daily limit
            var runtimeState = await _memoryService.GetOrCreateUserRuntimeStateAsync(userId);
            var today = DateTime.UtcNow.Date;

            // Reset counter if it's a new day
            if (runtimeState.LastOnlineSearchDate?.Date != today)
            {
                runtimeState.OnlineSearchCount = 0;
                runtimeState.LastOnlineSearchDate = today;
                await _memoryService.UpdateUserRuntimeStateAsync(runtimeState);
            }

            var canSearch = runtimeState.OnlineSearchCount < MAX_DAILY_SEARCHES;
            _logger.LogDebug("User {UserId} can search today: {CanSearch} (count: {Count}/{Max})", 
                userId, canSearch, runtimeState.OnlineSearchCount, MAX_DAILY_SEARCHES);

            return canSearch;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if user {UserId} can search today", userId);
            return false;
        }
    }

    /// <summary>
    /// Perform online search for user with consent and limits
    /// </summary>
    public async Task<string?> SearchForUserAsync(string userId, string searchQuery, string? deviceFingerprint = null)
    {
        try
        {
            // Check if user can search today
            if (!await CanSearchTodayAsync(userId))
            {
                _logger.LogInformation("User {UserId} cannot search today (no consent or limit reached)", userId);
                return null;
            }

            // Get GoogleSearch plugin
            var googleSearchPlugin = _kernel.Plugins.FirstOrDefault(p => p.Name == "GoogleSearch");
            if (googleSearchPlugin == null)
            {
                _logger.LogWarning("GoogleSearch plugin not found");
                return null;
            }

            // Perform search
            _logger.LogInformation("Performing online search for user {UserId}: {Query}", userId, searchQuery);

            var searchFunction = googleSearchPlugin["SearchWebAsync"];
            var result = await _kernel.InvokeAsync(searchFunction, new KernelArguments
            {
                ["query"] = searchQuery,
                ["maxResults"] = 3
            });

            var searchResults = result.ToString();

            // Update search count
            var runtimeState = await _memoryService.GetOrCreateUserRuntimeStateAsync(userId);
            runtimeState.OnlineSearchCount++;
            runtimeState.LastOnlineSearchDate = DateTime.UtcNow.Date;
            runtimeState.OnlineSearchIsStarted = true;
            await _memoryService.UpdateUserRuntimeStateAsync(runtimeState);

            // Store search results as memory with confirmation pending
            if (!string.IsNullOrEmpty(searchResults))
            {
                var memory = new MemoryEntry
                {
                    Content = $"Online search results for '{searchQuery}': {searchResults}",
                    Type = MemoryType.Facts,
                    Scope = MemoryScope.UserPrivate,
                    Confidentiality = Confidentiality.Internal,
                    Source = "online_search",
                    Provenance = Provenance.ExternalSearch,
                    Confidence = 0.7, // Lower confidence for external search
                    IsAuthoritative = false,
                    DeviceFingerprint = deviceFingerprint,
                    OwnerUserId = userId,
                    SubjectUserId = userId,
                    Confirmation = ConfirmationState.Pending, // Requires user confirmation
                    ExpiresAt = DateTime.UtcNow.AddDays(30) // Expire in 30 days
                };

                await _memoryService.StoreMemoryAsync(memory);
                _logger.LogInformation("Stored online search results as pending memory for user {UserId}", userId);
            }

            return searchResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform online search for user {UserId}", userId);
            return null;
        }
    }

    /// <summary>
    /// Request consent for online search from user
    /// </summary>
    public async Task<string> RequestOnlineSearchConsentAsync(string userId)
    {
        try
        {
            var hasConsent = await _memoryService.HasConsentAsync(userId, ONLINE_SEARCH_CONSENT);
            if (hasConsent)
            {
                return "Du har redan gett samtycke för online-sökning. Jag kan söka efter information åt dig en gång per dag.";
            }

            return @"🔍 **Online-sökning med samtycke**

Jag kan söka efter information på internet för att hjälpa dig bättre. Detta innebär:

✅ **Vad jag gör:**
- Söker efter relevant information på webben
- Sparar sökresultat som minnen (kräver din bekräftelse)
- Begränsar till 1 sökning per dag per användare

🔒 **Integritet:**
- Inga personuppgifter skickas till söktjänster
- Sökresultat sparas lokalt och kräver din bekräftelse
- Du kan återkalla samtycke när som helst

Vill du ge samtycke för online-sökning? Svara 'ja' för att aktivera eller 'nej' för att avböja.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request online search consent for user {UserId}", userId);
            return "Ett fel uppstod vid hantering av samtycke för online-sökning.";
        }
    }

    /// <summary>
    /// Process user consent response
    /// </summary>
    public async Task<string> ProcessConsentResponseAsync(string userId, string response)
    {
        try
        {
            var normalizedResponse = response.Trim().ToLowerInvariant();
            bool granted = normalizedResponse == "ja" || normalizedResponse == "yes" || 
                          normalizedResponse == "ok" || normalizedResponse == "godkänn";

            await _memoryService.UpdateConsentAsync(userId, ONLINE_SEARCH_CONSENT, granted);

            if (granted)
            {
                _logger.LogInformation("User {UserId} granted consent for online search", userId);
                return "✅ Tack! Du har gett samtycke för online-sökning. Jag kan nu söka efter information åt dig en gång per dag.";
            }
            else
            {
                _logger.LogInformation("User {UserId} denied consent for online search", userId);
                return "❌ Okej, jag kommer inte att söka online åt dig. Du kan ändra detta senare genom att fråga om online-sökning igen.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process consent response for user {UserId}", userId);
            return "Ett fel uppstod vid hantering av ditt samtycke.";
        }
    }

    /// <summary>
    /// Confirm or reject search results
    /// </summary>
    public async Task<string> ConfirmSearchResultsAsync(string userId, bool confirm)
    {
        try
        {
            // Find pending search result memories
            var pendingMemories = await _memoryService.RetrieveMemoriesAsync(
                "online search results", 
                MemoryType.Facts, 
                0.5, 
                10, 
                userId);

            var pendingSearchMemories = pendingMemories
                .Where(m => m.Provenance == Provenance.ExternalSearch && 
                           m.Confirmation == ConfirmationState.Pending)
                .ToList();

            if (!pendingSearchMemories.Any())
            {
                return "Inga väntande sökresultat att bekräfta.";
            }

            foreach (var memory in pendingSearchMemories)
            {
                memory.Confirmation = confirm ? ConfirmationState.Confirmed : ConfirmationState.Rejected;
                if (!confirm)
                {
                    memory.TombstonedAt = DateTime.UtcNow; // Mark as deleted
                }
                await _memoryService.StoreMemoryAsync(memory);
            }

            var action = confirm ? "bekräftat" : "avvisat";
            _logger.LogInformation("User {UserId} {Action} {Count} search result memories", 
                userId, action, pendingSearchMemories.Count);

            return confirm 
                ? $"✅ Bekräftat {pendingSearchMemories.Count} sökresultat som korrekta."
                : $"❌ Avvisat {pendingSearchMemories.Count} sökresultat.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to confirm search results for user {UserId}", userId);
            return "Ett fel uppstod vid bekräftelse av sökresultat.";
        }
    }

    /// <summary>
    /// Get search status for user
    /// </summary>
    public async Task<string> GetSearchStatusAsync(string userId)
    {
        try
        {
            var hasConsent = await _memoryService.HasConsentAsync(userId, ONLINE_SEARCH_CONSENT);
            if (!hasConsent)
            {
                return "🔍 Online-sökning: Inaktiverad (inget samtycke)";
            }

            var runtimeState = await _memoryService.GetOrCreateUserRuntimeStateAsync(userId);
            var today = DateTime.UtcNow.Date;
            var searchesLeft = MAX_DAILY_SEARCHES - runtimeState.OnlineSearchCount;

            if (runtimeState.LastOnlineSearchDate?.Date != today)
            {
                searchesLeft = MAX_DAILY_SEARCHES;
            }

            return $"🔍 Online-sökning: Aktiverad ({searchesLeft}/{MAX_DAILY_SEARCHES} sökningar kvar idag)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get search status for user {UserId}", userId);
            return "🔍 Online-sökning: Status okänd (fel uppstod)";
        }
    }
}
