using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Agent.Services.Interfaces;
using Agent.Models;

namespace Agent.BackgroundServices;

/// <summary>
/// Background service that performs automatic daily online searches for users with consent
/// Runs once per day and searches for relevant information based on user profiles
/// </summary>
public class DailyOnlineSearchService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailyOnlineSearchService> _logger;
    private readonly TimeSpan _dailyInterval = TimeSpan.FromHours(24);
    private readonly TimeSpan _searchTime = TimeSpan.FromHours(10); // 10 AM

    public DailyOnlineSearchService(
        IServiceProvider serviceProvider,
        ILogger<DailyOnlineSearchService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔍 Daily Online Search Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nextSearchTime = CalculateNextSearchTime();
                var delay = nextSearchTime - DateTimeOffset.Now;

                if (delay > TimeSpan.Zero)
                {
                    _logger.LogDebug("🔍 Next daily search scheduled for {NextSearchTime} (in {Delay})",
                        nextSearchTime, delay);
                    
                    await Task.Delay(delay, stoppingToken);
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    await PerformDailySearchesAsync();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in Daily Online Search Service: {ErrorMessage}", ex.Message);
                
                // Wait a bit before retrying to avoid tight error loops
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }

        _logger.LogInformation("🔍 Daily Online Search Service stopped");
    }

    private DateTimeOffset CalculateNextSearchTime()
    {
        var now = DateTimeOffset.Now;
        var today = now.Date;
        var searchTimeToday = today.Add(_searchTime);

        // If it's already past search time today, schedule for tomorrow
        if (now >= searchTimeToday)
        {
            return searchTimeToday.AddDays(1);
        }

        return searchTimeToday;
    }

    private async Task PerformDailySearchesAsync()
    {
        try
        {
            _logger.LogInformation("🔍 Starting daily online searches...");

            using var scope = _serviceProvider.CreateScope();
            var memoryService = scope.ServiceProvider.GetRequiredService<IProductionMemoryService>();
            var onlineSearchService = scope.ServiceProvider.GetRequiredService<IOnlineSearchService>();

            // Get all users who have given consent for online search
            var usersWithConsent = await memoryService.GetUsersWithConsentAsync("online_search");
            
            if (!usersWithConsent.Any())
            {
                _logger.LogInformation("🔍 No users with online search consent found");
                return;
            }

            _logger.LogInformation("🔍 Found {Count} users with online search consent", usersWithConsent.Count);

            var searchCount = 0;
            var errorCount = 0;

            foreach (var user in usersWithConsent)
            {
                try
                {
                    // Check if user can search today (respects daily limits)
                    if (!await onlineSearchService.CanSearchTodayAsync(user.UserId))
                    {
                        _logger.LogDebug("User {UserId} cannot search today (limit reached)", user.UserId);
                        continue;
                    }

                    // Generate search query based on user's profile and recent memories
                    var searchQuery = await GenerateSearchQueryForUserAsync(memoryService, user.UserId);
                    
                    if (string.IsNullOrEmpty(searchQuery))
                    {
                        _logger.LogDebug("No search query generated for user {UserId}", user.UserId);
                        continue;
                    }

                    // Perform the search
                    var searchResults = await onlineSearchService.SearchForUserAsync(user.UserId, searchQuery);
                    
                    if (!string.IsNullOrEmpty(searchResults))
                    {
                        searchCount++;
                        _logger.LogInformation("✅ Performed daily search for user {UserId} with query: {Query}", 
                            user.UserId, searchQuery);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Search returned no results for user {UserId}", user.UserId);
                    }

                    // Add a small delay between searches to be respectful to the API
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger.LogError(ex, "❌ Error performing daily search for user {UserId}: {ErrorMessage}", 
                        user.UserId, ex.Message);
                }
            }

            _logger.LogInformation("🔍 Daily searches completed: {SearchCount} successful, {ErrorCount} errors", 
                searchCount, errorCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during daily online searches: {ErrorMessage}", ex.Message);
        }
    }



    private async Task<string?> GenerateSearchQueryForUserAsync(IProductionMemoryService memoryService, string userId)
    {
        try
        {
            // Get user's recent memories to understand their interests
            var recentMemories = await memoryService.RetrieveMemoriesAsync(
                "user interests facts", 
                MemoryType.Facts, 
                0.5, 
                10, 
                userId);

            if (!recentMemories.Any())
            {
                // If no specific memories, use a general search query
                return "latest technology news artificial intelligence";
            }

            // Extract key topics from user's memories
            var topics = new List<string>();
            foreach (var memory in recentMemories.Take(5))
            {
                var content = memory.Content?.ToLowerInvariant() ?? "";
                
                // Extract potential search topics (this is a simplified approach)
                if (content.Contains("teknologi") || content.Contains("technology"))
                    topics.Add("technology news");
                if (content.Contains("ai") || content.Contains("artificiell intelligens"))
                    topics.Add("artificial intelligence");
                if (content.Contains("programmering") || content.Contains("programming"))
                    topics.Add("programming trends");
                if (content.Contains("företag") || content.Contains("business"))
                    topics.Add("business news");
                if (content.Contains("hälsa") || content.Contains("health"))
                    topics.Add("health news");
            }

            // If we found specific topics, use them
            if (topics.Any())
            {
                var searchQuery = string.Join(" ", topics.Distinct().Take(3));
                _logger.LogDebug("Generated search query for user {UserId}: {Query}", userId, searchQuery);
                return searchQuery;
            }

            // Fallback to general search
            return "latest news technology";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating search query for user {UserId}", userId);
            return "latest technology news";
        }
    }
}
