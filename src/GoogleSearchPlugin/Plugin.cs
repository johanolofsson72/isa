using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace GoogleSearchPlugin;

public class Plugin
{
    private readonly string _apiKey;
    private readonly string _cx;
    private readonly ILogger<Plugin> _logger;

    public Plugin(IConfiguration configuration, ILogger<Plugin>? logger = null)
    {
        _apiKey = configuration["GoogleSearch:ApiKey"] ?? throw new InvalidOperationException("GoogleSearch:ApiKey not configured");
        _cx = configuration["GoogleSearch:Cx"] ?? throw new InvalidOperationException("GoogleSearch:Cx not configured");
        _logger = logger ?? NullLogger<Plugin>.Instance;
        
        _logger.LogInformation("GoogleSearchPlugin initialized with API key and search engine ID");
    }

    [KernelFunction]
    [Description("Söker efter information om en person på webben med hjälp av Google Custom Search API.")]
    public async Task<string> SearchPersonAsync(
        [Description("Personens namn")] string name,
        [Description("Personens plats/location")] string? location = null,
        [Description("Personens företag/company")] string? company = null)
    {
        try
        {
            _logger.LogInformation($"Searching for person: {name}, Location: {location}, Company: {company}");

            // Build search query
            var searchTerms = new List<string> { name };
            if (!string.IsNullOrEmpty(location))
                searchTerms.Add(location);
            if (!string.IsNullOrEmpty(company))
                searchTerms.Add(company);

            var query = string.Join(" ", searchTerms);
            var url = $"https://www.googleapis.com/customsearch/v1?key={_apiKey}&cx={_cx}&q={Uri.EscapeDataString(query)}&num=5";

            _logger.LogInformation($"Searching with query: {query}");

            using var client = new HttpClient();
            var response = await client.GetStringAsync(url);
            var json = JsonDocument.Parse(response);

            var result = new StringBuilder();
            result.AppendLine($"🔍 Sökresultat för: {name}");
            if (!string.IsNullOrEmpty(location))
                result.AppendLine($"📍 Plats: {location}");
            if (!string.IsNullOrEmpty(company))
                result.AppendLine($"🏢 Företag: {company}");
            result.AppendLine();

            if (json.RootElement.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    var title = item.GetProperty("title").GetString() ?? "";
                    var snippet = item.GetProperty("snippet").GetString() ?? "";
                    var link = item.GetProperty("link").GetString() ?? "";

                    result.AppendLine($"📌 {title}");
                    result.AppendLine($"{snippet}");
                    result.AppendLine($"🔗 {link}");
                    result.AppendLine();
                }
            }
            else
            {
                result.AppendLine("🔍 Hittade ingen ytterligare information på webben.");
            }

            var resultString = result.ToString();
            _logger.LogInformation($"Search completed. Found {items.EnumerateArray().Count()} results");
            return resultString;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in SearchPersonAsync: {ex.Message}");
            return $"❌ Fel vid webbsökning: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Söker efter information om en person med all tillgänglig data och testar olika kombinationer för att hitta mer information.")]
    public async Task<string> SearchPersonWithAllDataAsync(
        [Description("Personens namn")] string name,
        [Description("All tillgänglig data om personen (adress, företag, telefon, e-post, etc.)")] string personData)
    {
        try
        {
            _logger.LogInformation($"Searching for person with all data: {name}");
            _logger.LogInformation($"Available data: {personData}");

            var result = new StringBuilder();
            result.AppendLine($"🔍 Avancerad sökning för: {name}");
            result.AppendLine($"📋 Tillgänglig data: {personData}");
            result.AppendLine();

            // Extract key information from personData
            var searchQueries = new List<string>();
            
            // Basic name search
            searchQueries.Add(name);
            
            // Extract and use different combinations
            var extractedInfo = ExtractPersonInfo(personData);
            
            // Add combinations with location
            if (!string.IsNullOrEmpty(extractedInfo.Location))
            {
                searchQueries.Add($"{name} {extractedInfo.Location}");
                searchQueries.Add($"{name} {extractedInfo.Location} {extractedInfo.Company}");
            }
            
            // Add combinations with company
            if (!string.IsNullOrEmpty(extractedInfo.Company))
            {
                searchQueries.Add($"{name} {extractedInfo.Company}");
                searchQueries.Add($"{name} {extractedInfo.Company} {extractedInfo.Location}");
            }
            
            // Add combinations with email domain
            if (!string.IsNullOrEmpty(extractedInfo.EmailDomain))
            {
                searchQueries.Add($"{name} {extractedInfo.EmailDomain}");
                searchQueries.Add($"{name} {extractedInfo.EmailDomain} {extractedInfo.Location}");
            }
            
            // Add combinations with phone area code
            if (!string.IsNullOrEmpty(extractedInfo.PhoneAreaCode))
            {
                searchQueries.Add($"{name} {extractedInfo.PhoneAreaCode}");
            }

            // Add combinations with personnummer (very specific identifier)
            if (!string.IsNullOrEmpty(extractedInfo.Personnummer))
            {
                searchQueries.Add($"{name} {extractedInfo.Personnummer}");
                searchQueries.Add($"{extractedInfo.Personnummer}");
            }

            // Add specific company searches if available
            if (!string.IsNullOrEmpty(extractedInfo.Company))
            {
                searchQueries.Add($"{extractedInfo.Company} {name}");
                searchQueries.Add($"{extractedInfo.Company} {extractedInfo.Location}");
            }

            // Remove duplicates and limit to most relevant
            searchQueries = searchQueries.Distinct().Take(5).ToList();

            var allResults = new List<string>();
            var seenUrls = new HashSet<string>();

            foreach (var query in searchQueries)
            {
                try
                {
                    var url = $"https://www.googleapis.com/customsearch/v1?key={_apiKey}&cx={_cx}&q={Uri.EscapeDataString(query)}&num=3";
                    
                    _logger.LogInformation($"Searching with query: {query}");

                    using var client = new HttpClient();
                    var response = await client.GetStringAsync(url);
                    var json = JsonDocument.Parse(response);

                    if (json.RootElement.TryGetProperty("items", out var items))
                    {
                        foreach (var item in items.EnumerateArray())
                        {
                            var title = item.GetProperty("title").GetString() ?? "";
                            var snippet = item.GetProperty("snippet").GetString() ?? "";
                            var link = item.GetProperty("link").GetString() ?? "";

                            // Avoid duplicate URLs
                            if (!seenUrls.Contains(link))
                            {
                                seenUrls.Add(link);
                                
                                // Filter results to only include relevant ones
                                if (IsRelevantResult(name, extractedInfo, title, snippet))
                                {
                                    allResults.Add($"🔍 Sökning: \"{query}\"\n📌 {title}\n{snippet}\n🔗 {link}\n");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error searching with query '{query}': {ex.Message}");
                }
            }

            if (allResults.Any())
            {
                result.AppendLine("📊 Relevanta resultat från webbsökning:");
                result.AppendLine();
                foreach (var searchResult in allResults.Take(5)) // Limit to 5 best results
                {
                    result.AppendLine(searchResult);
                }
            }
            else
            {
                result.AppendLine("🔍 Hittade ingen ytterligare relevant information på webben.");
            }

            var resultString = result.ToString();
            _logger.LogInformation($"Advanced search completed. Found {allResults.Count} relevant results");
            return resultString;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in SearchPersonWithAllDataAsync: {ex.Message}");
            return $"❌ Fel vid avancerad webbsökning: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Söker efter allmän information på webben med hjälp av Google Custom Search API.")]
    public async Task<string> SearchWebAsync(
        [Description("Söktermen")] string query,
        [Description("Antal resultat att returnera (1-10)")] int maxResults = 5)
    {
        try
        {
            _logger.LogInformation($"Searching web for: {query}");

            var url = $"https://www.googleapis.com/customsearch/v1?key={_apiKey}&cx={_cx}&q={Uri.EscapeDataString(query)}&num={Math.Min(maxResults, 10)}";

            using var client = new HttpClient();
            var response = await client.GetStringAsync(url);
            var json = JsonDocument.Parse(response);

            var result = new StringBuilder();
            result.AppendLine($"🔍 Sökresultat för: {query}");
            result.AppendLine();

            if (json.RootElement.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    var title = item.GetProperty("title").GetString() ?? "";
                    var snippet = item.GetProperty("snippet").GetString() ?? "";
                    var link = item.GetProperty("link").GetString() ?? "";

                    result.AppendLine($"📌 {title}");
                    result.AppendLine($"{snippet}");
                    result.AppendLine($"🔗 {link}");
                    result.AppendLine();
                }
            }
            else
            {
                result.AppendLine("🔍 Hittade ingen information på webben.");
            }

            var resultString = result.ToString();
            _logger.LogInformation($"Search completed. Found {items.EnumerateArray().Count()} results");
            return resultString;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in SearchWebAsync: {ex.Message}");
            return $"❌ Fel vid webbsökning: {ex.Message}";
        }
    }

    private PersonInfo ExtractPersonInfo(string personData)
    {
        var info = new PersonInfo();
        
        // Extract location (look for common patterns)
        var locationPatterns = new[] { "Bor i", "Adress:", "Plats:" };
        foreach (var pattern in locationPatterns)
        {
            var index = personData.IndexOf(pattern);
            if (index >= 0)
            {
                var start = index + pattern.Length;
                var end = personData.IndexOf('\n', start);
                if (end < 0) end = personData.Length;
                info.Location = personData.Substring(start, end - start).Trim();
                break;
            }
        }

        // Extract company (look for common patterns)
        var companyPatterns = new[] { "Arbetar på", "Företag:", "Company:" };
        foreach (var pattern in companyPatterns)
        {
            var index = personData.IndexOf(pattern);
            if (index >= 0)
            {
                var start = index + pattern.Length;
                var end = personData.IndexOf('\n', start);
                if (end < 0) end = personData.Length;
                info.Company = personData.Substring(start, end - start).Trim();
                break;
            }
        }

        // Extract personnummer (Swedish personal identity number)
        var personnummerMatch = System.Text.RegularExpressions.Regex.Match(personData, @"(\d{4}-\d{2}-\d{2}-\d{4})");
        if (personnummerMatch.Success)
        {
            info.Personnummer = personnummerMatch.Groups[1].Value;
        }

        // Extract email domain
        var emailMatch = System.Text.RegularExpressions.Regex.Match(personData, @"@([a-zA-Z0-9.-]+)");
        if (emailMatch.Success)
        {
            info.EmailDomain = emailMatch.Groups[1].Value;
        }

        // Extract phone area code
        var phoneMatch = System.Text.RegularExpressions.Regex.Match(personData, @"\+46\s*(\d{2})");
        if (phoneMatch.Success)
        {
            info.PhoneAreaCode = phoneMatch.Groups[1].Value;
        }

        return info;
    }

    private bool IsRelevantResult(string personName, PersonInfo personInfo, string title, string snippet)
    {
        var combinedText = $"{title} {snippet}".ToLowerInvariant();
        var personNameLower = personName.ToLowerInvariant();
        
        // Must contain the person's name
        if (!combinedText.Contains(personNameLower))
        {
            return false;
        }

        // Check for location matches
        if (!string.IsNullOrEmpty(personInfo.Location))
        {
            var locationLower = personInfo.Location.ToLowerInvariant();
            if (combinedText.Contains(locationLower))
            {
                return true;
            }
        }

        // Check for company matches
        if (!string.IsNullOrEmpty(personInfo.Company))
        {
            var companyLower = personInfo.Company.ToLowerInvariant();
            if (combinedText.Contains(companyLower))
            {
                return true;
            }
        }

        // Check for email domain matches
        if (!string.IsNullOrEmpty(personInfo.EmailDomain))
        {
            var domainLower = personInfo.EmailDomain.ToLowerInvariant();
            if (combinedText.Contains(domainLower))
            {
                return true;
            }
        }

        // Check for phone area code matches
        if (!string.IsNullOrEmpty(personInfo.PhoneAreaCode))
        {
            var phoneCode = personInfo.PhoneAreaCode;
            if (combinedText.Contains(phoneCode))
            {
                return true;
            }
        }

        // Check for personnummer matches
        if (!string.IsNullOrEmpty(personInfo.Personnummer))
        {
            var personnummer = personInfo.Personnummer;
            if (combinedText.Contains(personnummer))
            {
                return true;
            }
        }

        // Additional relevance checks
        var relevantKeywords = new[] { "linkedin", "facebook", "twitter", "instagram", "blog", "artikel", "nyhet", "intervju", "profil" };
        if (relevantKeywords.Any(keyword => combinedText.Contains(keyword)))
        {
            return true;
        }

        // If it contains the name but no other specific matches, be more conservative
        // Only include if it seems like a professional or personal profile
        var professionalKeywords = new[] { "profil", "linkedin", "cv", "resume", "portfolio" };
        if (professionalKeywords.Any(keyword => combinedText.Contains(keyword)))
        {
            return true;
        }

        return false;
    }

    private class PersonInfo
    {
        public string? Location { get; set; }
        public string? Company { get; set; }
        public string? Personnummer { get; set; }
        public string? EmailDomain { get; set; }
        public string? PhoneAreaCode { get; set; }
    }
} 