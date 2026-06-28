using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

namespace DocumentInterpreterPlugin.Services;

public class AzureDocumentIntelligenceService
{
    private readonly ILogger<AzureDocumentIntelligenceService> _logger;
    private readonly DocumentAnalysisClient? _client;
    private readonly bool _isEnabled;
    private readonly string _defaultLanguage;

    public bool IsEnabled => _isEnabled;

    public AzureDocumentIntelligenceService(IConfiguration configuration, ILogger<AzureDocumentIntelligenceService> logger)
    {
        _logger = logger;
        
        var endpoint = configuration["Azure:DocumentIntelligence:Endpoint"];
        var apiKey = configuration["Azure:DocumentIntelligence:ApiKey"];
        _defaultLanguage = configuration["Azure:DocumentIntelligence:Language"] ?? "sv,en";
        _isEnabled = configuration.GetValue<bool>("Azure:DocumentIntelligence:EnableDocumentOcr", false);

        _logger.LogInformation("🔧 AZURE DOCUMENT INTELLIGENCE INITIALIZATION");
        _logger.LogInformation("Endpoint: {Endpoint}", endpoint ?? "NULL");
        _logger.LogInformation("ApiKey: {ApiKey}", string.IsNullOrEmpty(apiKey) ? "NULL" : "***PROVIDED***");
        _logger.LogInformation("Language: {Language}", _defaultLanguage);
        _logger.LogInformation("EnableDocumentOcr: {Enabled}", _isEnabled);

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("❌ Azure Document Intelligence not configured. Document OCR will use fallback implementation.");
            _isEnabled = false;
            return;
        }

        try
        {
            _client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
            _logger.LogInformation("✅ Azure Document Intelligence service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to initialize Azure Document Intelligence service");
            _isEnabled = false;
        }
    }

    public async Task<string> ExtractTextFromDocumentAsync(byte[] documentBytes, string contentType, string? language = null)
    {
        Console.WriteLine($"🔧 AZURE DEBUG: ExtractTextFromDocumentAsync called with {documentBytes?.Length ?? 0} bytes, contentType: {contentType}");
        _logger.LogInformation("🔧 DEBUG: ExtractTextFromDocumentAsync called with {Size} bytes, contentType: {ContentType}", documentBytes?.Length ?? 0, contentType);

        if (!_isEnabled || _client == null)
        {
            Console.WriteLine($"❌ AZURE DEBUG: Not available - Enabled: {_isEnabled}, Client: {_client != null}");
            _logger.LogWarning("❌ Azure Document Intelligence not available - Enabled: {Enabled}, Client: {ClientExists}", _isEnabled, _client != null);
            return string.Empty;
        }

        if (documentBytes == null || documentBytes.Length == 0)
        {
            Console.WriteLine("❌ AZURE DEBUG: Document bytes are null or empty");
            _logger.LogWarning("❌ Document bytes are null or empty");
            return string.Empty;
        }

        try
        {
            var targetLanguage = language ?? _defaultLanguage;
            Console.WriteLine($"✅ AZURE DEBUG: Starting Azure Document Intelligence extraction");
            Console.WriteLine($"📄 AZURE DEBUG: Document size: {documentBytes.Length} bytes, contentType: {contentType}");
            _logger.LogInformation("✅ USING AZURE DOCUMENT INTELLIGENCE for text extraction (contentType: {ContentType})", contentType);
            _logger.LogInformation("📄 Sending document to Azure Document Intelligence OCR ({Size} bytes)", documentBytes.Length);
            _logger.LogInformation("🔧 DEBUG: Azure client initialized: {ClientExists}", _client != null);
            _logger.LogInformation("🔧 DEBUG: Service enabled: {Enabled}", _isEnabled);
            _logger.LogInformation("🔧 DEBUG: Target language: {Language}", targetLanguage);

            using var stream = new MemoryStream(documentBytes);
            _logger.LogInformation("🔧 DEBUG: Created memory stream with {Size} bytes", stream.Length);

            _logger.LogInformation("🚀 Calling Azure Document Intelligence AnalyzeDocumentAsync with prebuilt-read model");

            // Use the prebuilt-read model for general text extraction
            var operation = await _client!.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-read", stream);
            _logger.LogInformation("✅ Azure Document Intelligence AnalyzeDocumentAsync completed");

            var result = operation.Value;
            _logger.LogInformation("✅ Got analysis result from Azure Document Intelligence");

            _logger.LogInformation("Azure Document Intelligence analysis completed successfully");
            _logger.LogInformation("Found {PageCount} pages", result.Pages.Count);

            if (result.Pages.Count == 0)
            {
                _logger.LogWarning("❌ Azure Document Intelligence found 0 pages in document");
            }

            var extractedText = new StringBuilder();

            // Extract text from all pages
            foreach (var page in result.Pages)
            {
                _logger.LogInformation("Processing page {PageNumber} with {LineCount} lines", page.PageNumber, page.Lines.Count);

                if (page.Lines.Count == 0)
                {
                    _logger.LogWarning("❌ Page {PageNumber} has no lines", page.PageNumber);
                }

                foreach (var line in page.Lines)
                {
                    _logger.LogDebug("🔍 Line content: '{Content}'", line.Content);
                    extractedText.AppendLine(line.Content);
                }
            }

            var finalText = extractedText.ToString();
            _logger.LogInformation("✅ Azure Document Intelligence text extraction completed. Extracted {CharCount} characters", finalText.Length);

            if (finalText.Length == 0)
            {
                _logger.LogWarning("❌ Azure Document Intelligence extracted 0 characters from document");
                return "No text extracted";
            }
            else if (finalText.Length < 50)
            {
                _logger.LogWarning("⚠️ Azure Document Intelligence extracted very little text: '{Text}'", finalText);
            }

            _logger.LogInformation("🎯 Azure Document Intelligence SUCCESS - returning {Length} characters", finalText.Length);
            return finalText;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ AZURE DEBUG: EXCEPTION - {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"❌ AZURE DEBUG: Stack trace: {ex.StackTrace}");
            _logger.LogError(ex, "❌ EXCEPTION during Azure Document Intelligence document processing");
            _logger.LogError("❌ Exception type: {ExceptionType}", ex.GetType().Name);
            _logger.LogError("❌ Exception message: {Message}", ex.Message);
            if (ex.InnerException != null)
            {
                Console.WriteLine($"❌ AZURE DEBUG: Inner exception: {ex.InnerException.Message}");
                _logger.LogError("❌ Inner exception: {InnerMessage}", ex.InnerException.Message);
            }
            _logger.LogError("❌ Stack trace: {StackTrace}", ex.StackTrace);
            return "No text extracted";
        }
    }

    public async Task<string> ExtractTextFromPdfAsync(byte[] pdfBytes, string? language = null)
    {
        return await ExtractTextFromDocumentAsync(pdfBytes, "application/pdf", language);
    }
}
