using DocumentInterpreter.Interfaces;
using DocumentInterpreter.Models;
using DocumentInterpreterPlugin.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace DocumentInterpreter.Services
{
    /// <summary>
    /// Azure Computer Vision extraction technique for OCR-based document analysis
    /// </summary>
    public class AzureComputerVisionExtractionTechnique : IDocumentExtractionTechnique
    {
        private readonly ILogger<AzureComputerVisionExtractionTechnique> _logger;
        private readonly ISwedishNumberParser _numberParser;
        private readonly IDocumentTypeDetector _typeDetector;
        private readonly AzureComputerVisionService _azureVisionService;

        public string TechniqueName => "AzureComputerVision";
        public string[] SupportedFormats => new[] { ".pdf", ".png", ".jpg", ".jpeg", ".tiff", ".bmp", ".gif" };
        public int Priority => 80; // High priority - good for scanned documents and images
        public bool IsAvailable => _azureVisionService.IsEnabled;

        public AzureComputerVisionExtractionTechnique(
            ILogger<AzureComputerVisionExtractionTechnique> logger,
            ISwedishNumberParser numberParser,
            IDocumentTypeDetector typeDetector,
            AzureComputerVisionService azureVisionService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _numberParser = numberParser ?? throw new ArgumentNullException(nameof(numberParser));
            _typeDetector = typeDetector ?? throw new ArgumentNullException(nameof(typeDetector));
            _azureVisionService = azureVisionService ?? throw new ArgumentNullException(nameof(azureVisionService));
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
            // Azure Computer Vision typically takes 2-8 seconds depending on document complexity
            // Estimate based on file size: ~500ms per MB + base overhead
            var basetime = 2000; // 2 seconds base time
            var sizeBasedTime = (fileSizeBytes / (1024 * 1024)) * 500; // 500ms per MB
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
                    result.ErrorMessage = "Azure Computer Vision service is not available or not configured";
                    _logger.LogWarning("Azure Computer Vision extraction skipped - service not available");
                    return result;
                }

                _logger.LogInformation("🔍 Starting Azure Computer Vision extraction for: {FileName}", fileName);

                // Convert stream to byte array
                byte[] documentBytes;
                using (var memoryStream = new MemoryStream())
                {
                    stream.Position = 0;
                    await stream.CopyToAsync(memoryStream, cancellationToken);
                    documentBytes = memoryStream.ToArray();
                }

                // Extract text using Azure Computer Vision
                string extractedText;
                var extension = Path.GetExtension(fileName).ToLowerInvariant();

                if (extension == ".pdf")
                {
                    // For PDF files, use the PDF-specific method that converts to images
                    extractedText = await _azureVisionService.ExtractTextFromPdfAsync(documentBytes);
                }
                else
                {
                    // For image files, use the direct image method
                    extractedText = await _azureVisionService.ExtractTextFromImageAsync(documentBytes);
                }

                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    result.ErrorMessage = "Azure Computer Vision returned empty text";
                    _logger.LogWarning("Azure Computer Vision returned empty text for: {FileName}", fileName);
                    return result;
                }

                result.RawText = extractedText;
                _logger.LogInformation("✅ Azure Computer Vision extracted {CharCount} characters", extractedText.Length);

                // Detect document type
                var (documentType, typeConfidence) = _typeDetector.DetectDocumentType(extractedText);
                result.ExtractedData.DocumentType = documentType;
                result.ExtractedData.DocumentTypeConfidence = typeConfidence;

                // Parse amounts using Swedish number parser
                var amounts = _numberParser.ExtractSwedishAmounts(extractedText);
                result.ExtractedData.Amounts.AddRange(amounts);

                // Parse dates
                var dates = ExtractDates(extractedText);
                result.ExtractedData.Dates.AddRange(dates);

                // Extract fields using pattern matching
                var fields = ExtractFields(extractedText);
                result.ExtractedData.Fields.AddRange(fields);

                // Calculate confidence based on extracted data quality
                result.ConfidenceScore = CalculateConfidence(result.ExtractedData, extractedText);
                result.IsSuccessful = result.ConfidenceScore > 0.3; // Lower threshold for OCR technique

                _logger.LogInformation("🎯 Azure Computer Vision extraction completed. Success: {Success}, Confidence: {Confidence:F2}, Amounts: {AmountCount}, Dates: {DateCount}, Fields: {FieldCount}",
                    result.IsSuccessful, result.ConfidenceScore, result.ExtractedData.Amounts.Count, result.ExtractedData.Dates.Count, result.ExtractedData.Fields.Count);

                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex, "❌ Azure Computer Vision extraction failed for: {FileName}", fileName);
                return result;
            }
            finally
            {
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            }
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

                _logger.LogDebug("Extracted {FieldCount} fields using Azure Computer Vision patterns", fields.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract fields using pattern matching");
            }

            return fields;
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
