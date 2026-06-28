using DocumentInterpreter.Interfaces;
using DocumentInterpreter.Models;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentInterpreter.Services
{
    /// <summary>
    /// PDF text extraction using iText7 library for direct text extraction from text-based PDFs
    /// </summary>
    public class IText7ExtractionTechnique : IDocumentExtractionTechnique
    {
        private readonly ILogger<IText7ExtractionTechnique> _logger;
        private readonly ISwedishNumberParser _numberParser;
        private readonly IDocumentTypeDetector _typeDetector;

        public string TechniqueName => "iText7";
        public string[] SupportedFormats => new[] { ".pdf" };
        public int Priority => 90; // High priority for text-based PDFs
        public bool IsAvailable => true; // Always available

        public IText7ExtractionTechnique(
            ILogger<IText7ExtractionTechnique> logger,
            ISwedishNumberParser numberParser,
            IDocumentTypeDetector typeDetector)
        {
            _logger = logger;
            _numberParser = numberParser;
            _typeDetector = typeDetector;
        }

        public async Task<ExtractionResult> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new ExtractionResult
            {
                TechniqueName = TechniqueName,
                IsSuccessful = false
            };

            try
            {
                _logger.LogDebug("Starting iText7 extraction for file: {FilePath}", filePath);

                if (!File.Exists(filePath))
                {
                    result.ErrorMessage = $"File not found: {filePath}";
                    return result;
                }

                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                return await ExtractAsync(fileStream, Path.GetFileName(filePath), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "iText7 extraction failed for file: {FilePath}", filePath);
                result.ErrorMessage = ex.Message;
                return result;
            }
            finally
            {
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            }
        }

        public async Task<ExtractionResult> ExtractAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new ExtractionResult
            {
                TechniqueName = TechniqueName,
                IsSuccessful = false
            };

            try
            {
                _logger.LogDebug("Starting iText7 extraction for stream: {FileName}", fileName);

                if (!CanHandle(fileName))
                {
                    result.ErrorMessage = $"Unsupported file format: {fileName}";
                    return result;
                }

                // Reset stream position
                if (stream.CanSeek)
                    stream.Position = 0;

                var extractedText = await ExtractTextFromPdfAsync(stream, cancellationToken);
                
                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    result.ErrorMessage = "No text could be extracted from PDF";
                    return result;
                }

                result.RawText = extractedText;
                result.IsSuccessful = true;

                // Calculate confidence based on text quality
                result.ConfidenceScore = CalculateTextQuality(extractedText);

                // Extract structured data
                await ExtractStructuredDataAsync(result, extractedText);

                _logger.LogDebug("iText7 extraction completed. Text length: {Length}, Confidence: {Confidence}", 
                    extractedText.Length, result.ConfidenceScore);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "iText7 extraction failed for stream: {FileName}", fileName);
                result.ErrorMessage = ex.Message;
                return result;
            }
            finally
            {
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            }
        }

        public bool CanHandle(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return Array.Exists(SupportedFormats, format => format.Equals(extension, StringComparison.OrdinalIgnoreCase));
        }

        public long EstimateProcessingTime(long fileSizeBytes)
        {
            // Estimate ~1ms per KB for text extraction
            return Math.Max(100, fileSizeBytes / 1024);
        }

        private async Task<string> ExtractTextFromPdfAsync(Stream stream, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var pdfReader = new PdfReader(stream);
                    using var pdfDocument = new PdfDocument(pdfReader);
                    
                    var extractedText = "";
                    var pageCount = pdfDocument.GetNumberOfPages();
                    
                    _logger.LogDebug("PDF has {PageCount} pages", pageCount);

                    for (int pageNum = 1; pageNum <= pageCount; pageNum++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        var page = pdfDocument.GetPage(pageNum);
                        var strategy = new SimpleTextExtractionStrategy();
                        var pageText = PdfTextExtractor.GetTextFromPage(page, strategy);
                        
                        if (!string.IsNullOrWhiteSpace(pageText))
                        {
                            extractedText += $"=== PAGE {pageNum} ===\n{pageText}\n\n";
                        }
                    }

                    return extractedText;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to extract text from PDF using iText7");
                    throw;
                }
            }, cancellationToken);
        }

        private double CalculateTextQuality(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0.0;

            double quality = 0.5; // Base quality

            // Higher quality for longer text
            if (text.Length > 1000)
                quality += 0.2;
            else if (text.Length > 500)
                quality += 0.1;

            // Higher quality if text contains structured content
            if (text.Contains("faktura") || text.Contains("invoice"))
                quality += 0.1;
            
            if (text.Contains("moms") || text.Contains("vat"))
                quality += 0.1;

            // Check for Swedish number patterns
            var amounts = _numberParser.ExtractSwedishAmounts(text);
            if (amounts.Length > 0)
                quality += 0.1;

            // Lower quality if text seems garbled (too many special characters)
            var specialCharRatio = text.Count(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)) / (double)text.Length;
            if (specialCharRatio > 0.3)
                quality -= 0.2;

            return Math.Max(0.0, Math.Min(1.0, quality));
        }

        private async Task ExtractStructuredDataAsync(ExtractionResult result, string text)
        {
            await Task.Run(() =>
            {
                try
                {
                    // Extract amounts
                    var amounts = _numberParser.ExtractSwedishAmounts(text);
                    result.ExtractedData.Amounts.AddRange(amounts);

                    // Detect document type
                    var (docType, confidence) = _typeDetector.DetectDocumentType(text);
                    result.ExtractedData.DocumentType = docType;
                    result.ExtractedData.DocumentTypeConfidence = confidence;

                    // Extract dates (basic implementation)
                    ExtractDates(result.ExtractedData, text);

                    // Extract key fields (basic implementation)
                    ExtractFields(result.ExtractedData, text);

                    _logger.LogDebug("Extracted {AmountCount} amounts, document type: {DocType} (confidence: {Confidence})",
                        amounts.Length, docType, confidence);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to extract structured data from text");
                }
            });
        }

        private void ExtractDates(ExtractedData data, string text)
        {
            // Basic date extraction - can be enhanced
            var datePatterns = new[]
            {
                @"\b(\d{4}-\d{2}-\d{2})\b", // YYYY-MM-DD
                @"\b(\d{2}/\d{2}/\d{4})\b", // DD/MM/YYYY
                @"\b(\d{2}\.\d{2}\.\d{4})\b" // DD.MM.YYYY
            };

            foreach (var pattern in datePatterns)
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(text, pattern);
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (DateTime.TryParse(match.Value, out var date))
                    {
                        data.Dates.Add(new ExtractedDate
                        {
                            Value = date,
                            Type = DateType.Unknown,
                            Confidence = 0.7,
                            RawText = match.Value
                        });
                    }
                }
            }
        }

        private void ExtractFields(ExtractedData data, string text)
        {
            // Basic field extraction - can be enhanced
            var fieldPatterns = new[]
            {
                (@"(?i)fakturanummer[\s:]*(\w+)", "InvoiceNumber"),
                (@"(?i)kundnummer[\s:]*(\w+)", "CustomerNumber"),
                (@"(?i)ordernummer[\s:]*(\w+)", "OrderNumber")
            };

            foreach (var (pattern, fieldName) in fieldPatterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(text, pattern);
                if (match.Success && match.Groups[1].Success)
                {
                    data.Fields.Add(new ExtractedField
                    {
                        Name = fieldName,
                        Value = match.Groups[1].Value,
                        Confidence = 0.8
                    });
                }
            }
        }
    }
}
