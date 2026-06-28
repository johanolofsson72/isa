using DocumentInterpreter.Interfaces;
using DocumentInterpreter.Models;
using DocumentInterpreterPlugin.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace DocumentInterpreter.Services
{
    /// <summary>
    /// Azure Document Intelligence extraction technique for AI-powered document analysis
    /// </summary>
    public class AzureDocumentIntelligenceExtractionTechnique : IDocumentExtractionTechnique
    {
        private readonly ILogger<AzureDocumentIntelligenceExtractionTechnique> _logger;
        private readonly ISwedishNumberParser _numberParser;
        private readonly IDocumentTypeDetector _typeDetector;
        private readonly AzureDocumentIntelligenceService _azureService;

        public string TechniqueName => "AzureDocumentIntelligence";
        public string[] SupportedFormats => new[] { ".pdf", ".png", ".jpg", ".jpeg", ".tiff", ".bmp", ".docx", ".doc" };
        public int Priority => 95; // Highest priority - most advanced AI technique
        public bool IsAvailable => _azureService.IsEnabled;

        public AzureDocumentIntelligenceExtractionTechnique(
            ILogger<AzureDocumentIntelligenceExtractionTechnique> logger,
            ISwedishNumberParser numberParser,
            IDocumentTypeDetector typeDetector,
            AzureDocumentIntelligenceService azureService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _numberParser = numberParser ?? throw new ArgumentNullException(nameof(numberParser));
            _typeDetector = typeDetector ?? throw new ArgumentNullException(nameof(typeDetector));
            _azureService = azureService ?? throw new ArgumentNullException(nameof(azureService));
        }

        public bool CanHandle(string fileName)
        {
            if (!IsAvailable)
                return false;

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return SupportedFormats.Contains(extension);
        }

        public async Task<ExtractionResult> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
        {
            using var fileStream = File.OpenRead(filePath);
            return await ExtractAsync(fileStream, Path.GetFileName(filePath), cancellationToken);
        }

        public long EstimateProcessingTime(long fileSizeBytes)
        {
            // Azure Document Intelligence typically takes 2-10 seconds depending on document complexity
            // Estimate based on file size: ~1 second per MB + base overhead
            var basetime = 2000; // 2 seconds base time
            var sizeBasedTime = (fileSizeBytes / (1024 * 1024)) * 1000; // 1 second per MB
            return basetime + sizeBasedTime;
        }

        public async Task<ExtractionResult> ExtractAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new ExtractionResult
            {
                TechniqueName = TechniqueName,
                IsSuccessful = false,
                ConfidenceScore = 0.0,
                ExtractedData = new ExtractedData(),
                ProcessingTimeMs = 0,
                ErrorMessage = null,
                RawText = string.Empty
            };

            try
            {
                if (!IsAvailable)
                {
                    result.ErrorMessage = "Azure Document Intelligence service is not available or not configured";
                    _logger.LogWarning("Azure Document Intelligence extraction skipped - service not available");
                    return result;
                }

                _logger.LogInformation("🤖 Starting Azure Document Intelligence extraction for: {FileName}", fileName);

                // Convert stream to byte array
                byte[] documentBytes;
                using (var memoryStream = new MemoryStream())
                {
                    stream.Position = 0;
                    await stream.CopyToAsync(memoryStream, cancellationToken);
                    documentBytes = memoryStream.ToArray();
                }

                // Extract text using Azure Document Intelligence
                var extractedText = await _azureService.ExtractTextFromDocumentAsync(
                    documentBytes, 
                    GetContentTypeFromFileName(fileName));

                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    result.ErrorMessage = "Azure Document Intelligence returned empty text";
                    _logger.LogWarning("Azure Document Intelligence returned empty text for: {FileName}", fileName);
                    return result;
                }

                result.RawText = extractedText;
                _logger.LogInformation("✅ Azure Document Intelligence extracted {CharCount} characters", extractedText.Length);

                // Detect document type
                var (documentType, typeConfidence) = _typeDetector.DetectDocumentType(extractedText);
                result.ExtractedData.DocumentType = documentType;
                result.ExtractedData.DocumentTypeConfidence = typeConfidence;

                // Parse amounts using Swedish number parser
                var amounts = _numberParser.ExtractSwedishAmounts(extractedText);
                result.ExtractedData.Amounts.AddRange(amounts);

                // Parse dates (simplified for now)
                var dates = ExtractDates(extractedText);
                result.ExtractedData.Dates.AddRange(dates);

                // Extract fields using pattern matching
                var fields = ExtractFields(extractedText);
                result.ExtractedData.Fields.AddRange(fields);

                // Calculate confidence based on extracted data quality
                result.ConfidenceScore = CalculateConfidence(result.ExtractedData, extractedText);
                result.IsSuccessful = result.ConfidenceScore > 0.3; // Lower threshold for AI technique

                _logger.LogInformation("🎯 Azure Document Intelligence extraction completed. Success: {Success}, Confidence: {Confidence:F2}, Amounts: {AmountCount}, Dates: {DateCount}, Fields: {FieldCount}",
                    result.IsSuccessful, result.ConfidenceScore, result.ExtractedData.Amounts.Count, result.ExtractedData.Dates.Count, result.ExtractedData.Fields.Count);

                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex, "❌ Azure Document Intelligence extraction failed for: {FileName}", fileName);
                return result;
            }
            finally
            {
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            }
        }

        private string GetContentTypeFromFileName(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".tiff" or ".tif" => "image/tiff",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream"
            };
        }

        private List<ExtractedField> ExtractFields(string text)
        {
            var fields = new List<ExtractedField>();

            try
            {
                // Extract invoice number patterns
                var invoicePatterns = new[]
                {
                    @"(?:faktura|invoice)[\s#:]*(\d+)",
                    @"(?:nr|number|no)[\s#:]*(\d+)",
                    @"(?:ref|reference)[\s#:]*([A-Z0-9-]+)"
                };

                foreach (var pattern in invoicePatterns)
                {
                    var matches = System.Text.RegularExpressions.Regex.Matches(text, pattern, 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    
                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        if (match.Groups.Count > 1)
                        {
                            fields.Add(new ExtractedField
                            {
                                Name = "InvoiceNumber",
                                Value = match.Groups[1].Value.Trim(),
                                Confidence = 0.8
                            });
                        }
                    }
                }

                // Extract customer/supplier names
                var namePatterns = new[]
                {
                    @"(?:till|to|kund|customer)[\s:]*([A-ZÅÄÖ][a-zåäö\s]+(?:[A-ZÅÄÖ][a-zåäö\s]*)*)",
                    @"(?:från|from|leverantör|supplier)[\s:]*([A-ZÅÄÖ][a-zåäö\s]+(?:[A-ZÅÄÖ][a-zåäö\s]*)*)"
                };

                foreach (var pattern in namePatterns)
                {
                    var matches = System.Text.RegularExpressions.Regex.Matches(text, pattern, 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    
                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        if (match.Groups.Count > 1)
                        {
                            var name = match.Groups[1].Value.Trim();
                            if (name.Length > 3 && name.Length < 100) // Reasonable name length
                            {
                                fields.Add(new ExtractedField
                                {
                                    Name = "CustomerName",
                                    Value = name,
                                    Confidence = 0.7
                                });
                            }
                        }
                    }
                }

                _logger.LogDebug("Extracted {FieldCount} fields using Azure Document Intelligence patterns", fields.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract fields using pattern matching");
            }

            return fields;
        }

        private List<ExtractedDate> ExtractDates(string text)
        {
            var dates = new List<ExtractedDate>();

            try
            {
                // Simple date extraction patterns for Swedish dates
                var datePatterns = new[]
                {
                    @"\b(\d{4}-\d{2}-\d{2})\b", // YYYY-MM-DD
                    @"\b(\d{2}/\d{2}/\d{4})\b", // DD/MM/YYYY
                    @"\b(\d{1,2}\s+\w+\s+\d{4})\b" // DD Month YYYY
                };

                foreach (var pattern in datePatterns)
                {
                    var matches = System.Text.RegularExpressions.Regex.Matches(text, pattern);

                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        if (DateTime.TryParse(match.Groups[1].Value, out var dateValue))
                        {
                            dates.Add(new ExtractedDate
                            {
                                Value = dateValue,
                                Type = DateType.Unknown,
                                Confidence = 0.7,
                                RawText = match.Groups[1].Value
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract dates");
            }

            return dates;
        }

        private double CalculateConfidence(ExtractedData data, string rawText)
        {
            double confidence = 0.0;

            // Base confidence from text quality
            if (!string.IsNullOrWhiteSpace(rawText))
            {
                confidence += 0.3; // Base score for having text
                
                // Bonus for text length (more text = higher confidence)
                if (rawText.Length > 100) confidence += 0.1;
                if (rawText.Length > 500) confidence += 0.1;
            }

            // Confidence from extracted amounts
            if (data.Amounts.Any())
            {
                confidence += 0.3;
                
                // Bonus for multiple amounts (total, VAT, net)
                if (data.Amounts.Count > 1) confidence += 0.1;
            }

            // Confidence from extracted dates
            if (data.Dates.Any())
            {
                confidence += 0.2;
            }

            // Confidence from extracted fields
            if (data.Fields.Any())
            {
                confidence += 0.2;
                
                // Bonus for invoice number
                if (data.Fields.Any(f => f.Name.Contains("Invoice", StringComparison.OrdinalIgnoreCase)))
                {
                    confidence += 0.1;
                }
            }

            // Cap at 1.0
            return Math.Min(confidence, 1.0);
        }
    }
}
