using Azure;
using Azure.AI.Vision.ImageAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using SkiaSharp;
using PDFtoImage;

namespace DocumentInterpreterPlugin.Services;

public interface IAzureComputerVisionService
{
    bool IsEnabled { get; }
    Task<string> ExtractTextFromImageAsync(byte[] imageBytes, string? language = null);
    Task<string> ExtractTextFromPdfAsync(byte[] pdfBytes, string? language = null);
}

public class AzureComputerVisionService : IAzureComputerVisionService
{
    private readonly ImageAnalysisClient? _client;
    private readonly ILogger<AzureComputerVisionService> _logger;
    private readonly string _defaultLanguage;
    private readonly bool _isEnabled;

    public bool IsEnabled => _isEnabled;

    public AzureComputerVisionService(IConfiguration configuration, ILogger<AzureComputerVisionService> logger)
    {
        _logger = logger;

        var endpoint = configuration["Azure:ComputerVision:Endpoint"];
        var apiKey = configuration["Azure:ComputerVision:ApiKey"];
        _defaultLanguage = configuration["Azure:ComputerVision:Language"] ?? "sv,en";
        _isEnabled = configuration.GetValue<bool>("Azure:ComputerVision:EnableAdvancedOcr", false);

        _logger.LogInformation("🔧 AZURE COMPUTER VISION INITIALIZATION");
        _logger.LogInformation("Endpoint: {Endpoint}", endpoint ?? "NULL");
        _logger.LogInformation("ApiKey: {ApiKey}", string.IsNullOrEmpty(apiKey) ? "NULL" : "***PROVIDED***");
        _logger.LogInformation("Language: {Language}", _defaultLanguage);
        _logger.LogInformation("EnableAdvancedOcr: {Enabled}", _isEnabled);

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("❌ Azure Computer Vision not configured. OCR will use fallback implementation.");
            _isEnabled = false;
            return;
        }

        try
        {
            _client = new ImageAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
            _logger.LogInformation("✅ Azure Computer Vision service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to initialize Azure Computer Vision service");
            _isEnabled = false;
        }
    }

    public async Task<string> ExtractTextFromImageAsync(byte[] imageBytes, string? language = null)
    {
        if (!_isEnabled || _client == null)
        {
            _logger.LogWarning("Azure Computer Vision not available, returning empty text");
            return string.Empty;
        }

        try
        {
            _logger.LogInformation("Starting Azure Computer Vision OCR for image ({Size} bytes)", imageBytes.Length);

            using var imageStream = new MemoryStream(imageBytes);
            var imageData = BinaryData.FromStream(imageStream);

            _logger.LogDebug("Calling Azure Computer Vision AnalyzeAsync with Read feature");

            // Use the correct API for Azure Computer Vision
            var result = await _client!.AnalyzeAsync(imageData, VisualFeatures.Read);

            _logger.LogDebug("Azure Computer Vision API call completed");

            if (result?.Value?.Read?.Blocks == null)
            {
                _logger.LogWarning("No text blocks found in image - result.Value.Read.Blocks is null");
                _logger.LogDebug("Result details: HasValue={HasValue}, Read={Read}",
                    result?.HasValue, result?.Value?.Read != null);
                return string.Empty;
            }

            var extractedText = new StringBuilder();
            var blockCount = 0;
            var lineCount = 0;

            foreach (var block in result.Value.Read.Blocks)
            {
                blockCount++;
                foreach (var line in block.Lines)
                {
                    lineCount++;
                    extractedText.AppendLine(line.Text);
                    _logger.LogDebug("Extracted line {LineNum}: '{Text}'", lineCount, line.Text);
                }
            }

            var text = extractedText.ToString().Trim();
            _logger.LogInformation("Azure Computer Vision extracted {Length} characters from {BlockCount} blocks and {LineCount} lines",
                text.Length, blockCount, lineCount);

            if (!string.IsNullOrEmpty(text))
            {
                _logger.LogDebug("Extracted text preview: '{Preview}'",
                    text.Length > 100 ? text.Substring(0, 100) + "..." : text);
            }

            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Azure Computer Vision OCR");
            return string.Empty;
        }
    }

    public async Task<string> ExtractTextFromPdfAsync(byte[] pdfBytes, string? language = null)
    {
        if (!_isEnabled)
        {
            _logger.LogWarning("Azure Computer Vision not available for PDF processing");
            return string.Empty;
        }

        try
        {
            _logger.LogInformation("Converting PDF to images for Azure Computer Vision OCR ({Size} bytes)", pdfBytes.Length);

            // Convert PDF to images first
            var imageBytes = await ConvertPdfToImagesAsync(pdfBytes);

            if (!imageBytes.Any())
            {
                _logger.LogWarning("No images generated from PDF");
                return string.Empty;
            }

            _logger.LogInformation("Successfully converted PDF to {ImageCount} images", imageBytes.Count);

            // Process each image and combine results
            var allText = new StringBuilder();

            for (int i = 0; i < imageBytes.Count; i++)
            {
                _logger.LogInformation("Processing image {ImageIndex}/{TotalImages} ({Size} bytes)",
                    i + 1, imageBytes.Count, imageBytes[i].Length);

                var imageText = await ExtractTextFromImageAsync(imageBytes[i], language);

                if (!string.IsNullOrWhiteSpace(imageText))
                {
                    if (allText.Length > 0)
                        allText.AppendLine(); // Add line break between pages

                    allText.Append(imageText);
                    _logger.LogInformation("Extracted {CharCount} characters from image {ImageIndex}",
                        imageText.Length, i + 1);
                }
            }

            var finalText = allText.ToString();
            _logger.LogInformation("Combined text from all images: {TotalCharCount} characters", finalText.Length);

            return finalText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Azure Computer Vision PDF processing");
            return string.Empty;
        }
    }

    private Task<List<byte[]>> ConvertPdfToImagesAsync(byte[] pdfBytes)
    {
        var images = new List<byte[]>();

        try
        {
            _logger.LogDebug("Starting PDF to image conversion");

            // First, try to extract text directly from PDF
            var directText = ExtractTextDirectlyFromPdf(pdfBytes);
            if (!string.IsNullOrEmpty(directText) && directText.Length > 100)
            {
                _logger.LogInformation("PDF contains extractable text ({Length} chars), skipping image conversion", directText.Length);
                // Return empty list to indicate we should use direct text extraction
                return Task.FromResult(images);
            }

            _logger.LogInformation("PDF has minimal extractable text, converting to images for OCR");

            // For now, we'll implement a simple approach using iText7 to render pages
            // This is a basic implementation - in production you might want to use a more robust solution
            using var pdfDocument = new PdfDocument(new PdfReader(new MemoryStream(pdfBytes)));

            var pageCount = pdfDocument.GetNumberOfPages();
            _logger.LogDebug("PDF has {PageCount} pages", pageCount);

            // Convert PDF pages to images using PDFtoImage library
            try
            {
                _logger.LogInformation("Starting PDF to image conversion using PDFtoImage library");
#pragma warning disable CA1416 // Validate platform compatibility
                var pdfImages = Conversion.ToImages(pdfBytes);
#pragma warning restore CA1416 // Validate platform compatibility
                var pageNum = 1;

                _logger.LogInformation("PDF converted to {PageCount} images", pdfImages.Count());

                foreach (var skImage in pdfImages.Take(10)) // Limit to 10 pages for cost control
                {
                    try
                    {
                        using var data = skImage.Encode(SKEncodedImageFormat.Png, 100);
                        images.Add(data.ToArray());
                        _logger.LogInformation("Converted PDF page {PageNum} to PNG image ({Size} bytes)", pageNum, data.Size);
                        pageNum++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to encode PDF page {PageNum} to PNG", pageNum);
                    }
                    finally
                    {
                        skImage?.Dispose();
                    }
                }

                _logger.LogInformation("Successfully converted {ImageCount} PDF pages to images", images.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert PDF to images using PDFtoImage library");
                return Task.FromResult(images);
            }

            _logger.LogInformation("Converted {PageCount} PDF pages to images", images.Count);
            return Task.FromResult(images);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert PDF to images");
            return Task.FromResult(images);
        }
    }

    private string ExtractTextDirectlyFromPdf(byte[] pdfBytes)
    {
        try
        {
            using var pdfDocument = new PdfDocument(new PdfReader(new MemoryStream(pdfBytes)));
            var text = new StringBuilder();

            for (int pageNum = 1; pageNum <= pdfDocument.GetNumberOfPages(); pageNum++)
            {
                var page = pdfDocument.GetPage(pageNum);
                var strategy = new SimpleTextExtractionStrategy();
                var pageText = PdfTextExtractor.GetTextFromPage(page, strategy);
                text.AppendLine(pageText);
            }

            return text.ToString().Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract text directly from PDF");
            return string.Empty;
        }
    }


}
