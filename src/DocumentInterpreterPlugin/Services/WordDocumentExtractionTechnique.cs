using DocumentInterpreter.Interfaces;
using DocumentInterpreter.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Spreadsheet;
using OfficeOpenXml;
using RtfPipe;

namespace DocumentInterpreter.Services
{
    /// <summary>
    /// Office document extraction technique supporting Word, Excel, Text, and RTF formats
    /// </summary>
    public class OfficeDocumentExtractionTechnique : IDocumentExtractionTechnique
    {
        private readonly ILogger<OfficeDocumentExtractionTechnique> _logger;
        private readonly ISwedishNumberParser _numberParser;
        private readonly IDocumentTypeDetector _typeDetector;

        public string TechniqueName => "OfficeDocument";
        public string[] SupportedFormats => new[] { ".docx", ".doc", ".xlsx", ".xls", ".txt", ".rtf" };
        public int Priority => 85; // High priority for office documents
        public bool IsAvailable => true; // Always available

        public OfficeDocumentExtractionTechnique(
            ILogger<OfficeDocumentExtractionTechnique> logger,
            ISwedishNumberParser numberParser,
            IDocumentTypeDetector typeDetector)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _numberParser = numberParser ?? throw new ArgumentNullException(nameof(numberParser));
            _typeDetector = typeDetector ?? throw new ArgumentNullException(nameof(typeDetector));

            // Set EPPlus license context
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public bool CanHandle(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
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
            // Word documents are typically fast to process
            return Math.Max(500, fileSizeBytes / 1000); // ~1ms per KB
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
                _logger.LogInformation("🔍 Starting office document extraction for: {FileName}", fileName);

                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                string extractedText;

                switch (extension)
                {
                    case ".docx":
                        extractedText = await ExtractFromDocxAsync(stream, cancellationToken);
                        break;
                    case ".doc":
                        result.ErrorMessage = "Legacy .doc format not supported. Please convert to .docx format.";
                        _logger.LogWarning("Legacy .doc format not supported for: {FileName}", fileName);
                        return result;
                    case ".xlsx":
                        extractedText = await ExtractFromXlsxAsync(stream, cancellationToken);
                        break;
                    case ".xls":
                        result.ErrorMessage = "Legacy .xls format not supported. Please convert to .xlsx format.";
                        _logger.LogWarning("Legacy .xls format not supported for: {FileName}", fileName);
                        return result;
                    case ".txt":
                        extractedText = await ExtractFromTxtAsync(stream, cancellationToken);
                        break;
                    case ".rtf":
                        extractedText = await ExtractFromRtfAsync(stream, cancellationToken);
                        break;
                    default:
                        result.ErrorMessage = $"Unsupported file format: {extension}";
                        return result;
                }

                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    result.ErrorMessage = "No text extracted from Word document";
                    _logger.LogWarning("No text extracted from Word document: {FileName}", fileName);
                    return result;
                }

                result.RawText = extractedText;
                _logger.LogInformation("✅ Office document extracted {CharCount} characters", extractedText.Length);

                // Detect document type
                var (documentType, typeConfidence) = _typeDetector.DetectDocumentType(extractedText);
                result.ExtractedData.DocumentType = documentType;
                result.ExtractedData.DocumentTypeConfidence = typeConfidence;

                // Parse amounts using Swedish number parser
                var amounts = _numberParser.ExtractSwedishAmounts(extractedText);
                result.ExtractedData.Amounts.AddRange(amounts);

                // Extract fields using pattern matching
                var fields = ExtractFields(extractedText);
                result.ExtractedData.Fields.AddRange(fields);

                // Calculate confidence based on extracted data quality
                result.ConfidenceScore = CalculateConfidence(result.ExtractedData, extractedText);
                result.IsSuccessful = result.ConfidenceScore > 0.3;

                _logger.LogInformation("🎯 Office document extraction completed. Success: {Success}, Confidence: {Confidence:F2}, Amounts: {AmountCount}, Fields: {FieldCount}",
                    result.IsSuccessful, result.ConfidenceScore, result.ExtractedData.Amounts.Count, result.ExtractedData.Fields.Count);

                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex, "❌ Office document extraction failed for: {FileName}", fileName);
                return result;
            }
            finally
            {
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            }
        }

        private async Task<string> ExtractFromDocxAsync(Stream stream, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var document = WordprocessingDocument.Open(stream, false);
                    var body = document.MainDocumentPart?.Document?.Body;
                    
                    if (body == null)
                    {
                        _logger.LogWarning("Word document body is null");
                        return string.Empty;
                    }

                    var text = new StringBuilder();
                    
                    // Extract text from paragraphs
                    foreach (var paragraph in body.Elements<Paragraph>())
                    {
                        var paragraphText = paragraph.InnerText;
                        if (!string.IsNullOrWhiteSpace(paragraphText))
                        {
                            text.AppendLine(paragraphText);
                        }
                    }

                    // Extract text from tables
                    foreach (var table in body.Elements<DocumentFormat.OpenXml.Wordprocessing.Table>())
                    {
                        foreach (var row in table.Elements<TableRow>())
                        {
                            var rowText = new List<string>();
                            foreach (var cell in row.Elements<TableCell>())
                            {
                                var cellText = cell.InnerText?.Trim();
                                if (!string.IsNullOrWhiteSpace(cellText))
                                {
                                    rowText.Add(cellText);
                                }
                            }
                            if (rowText.Any())
                            {
                                text.AppendLine(string.Join("\t", rowText));
                            }
                        }
                    }

                    var result = text.ToString().Trim();
                    _logger.LogInformation("Extracted {CharCount} characters from Word document", result.Length);
                    
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to extract text from Word document");
                    throw;
                }
            }, cancellationToken);
        }

        private async Task<string> ExtractFromXlsxAsync(Stream stream, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var package = new ExcelPackage(stream);
                    var text = new StringBuilder();

                    foreach (var worksheet in package.Workbook.Worksheets)
                    {
                        _logger.LogDebug("Processing worksheet: {WorksheetName}", worksheet.Name);

                        if (worksheet.Dimension == null) continue;

                        text.AppendLine($"=== {worksheet.Name} ===");

                        for (int row = worksheet.Dimension.Start.Row; row <= worksheet.Dimension.End.Row; row++)
                        {
                            var rowData = new List<string>();

                            for (int col = worksheet.Dimension.Start.Column; col <= worksheet.Dimension.End.Column; col++)
                            {
                                var cellValue = worksheet.Cells[row, col].Text?.Trim();
                                if (!string.IsNullOrWhiteSpace(cellValue))
                                {
                                    rowData.Add(cellValue);
                                }
                            }

                            if (rowData.Any())
                            {
                                text.AppendLine(string.Join("\t", rowData));
                            }
                        }

                        text.AppendLine(); // Add blank line between worksheets
                    }

                    var result = text.ToString().Trim();
                    _logger.LogInformation("Extracted {CharCount} characters from Excel document", result.Length);

                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to extract text from Excel document");
                    throw;
                }
            }, cancellationToken);
        }

        private async Task<string> ExtractFromTxtAsync(Stream stream, CancellationToken cancellationToken)
        {
            try
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                var text = await reader.ReadToEndAsync();

                _logger.LogInformation("Extracted {CharCount} characters from text document", text.Length);
                return text;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract text from text document");
                throw;
            }
        }

        private async Task<string> ExtractFromRtfAsync(Stream stream, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    var rtfContent = reader.ReadToEnd();

                    // Clean and validate RTF content
                    rtfContent = rtfContent.Trim();

                    if (string.IsNullOrWhiteSpace(rtfContent))
                    {
                        _logger.LogWarning("RTF content is empty");
                        return string.Empty;
                    }

                    // Check if it's actually RTF format
                    if (!rtfContent.StartsWith("{\\rtf", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("File does not appear to be valid RTF format, treating as plain text");
                        return rtfContent;
                    }

                    // Ensure RTF content ends properly
                    if (!rtfContent.EndsWith("}"))
                    {
                        rtfContent = rtfContent.TrimEnd() + "}";
                    }

                    try
                    {
                        // Convert RTF to HTML using RtfPipe
                        var html = Rtf.ToHtml(rtfContent);

                        // Remove HTML tags to get plain text
                        var plainText = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
                        plainText = System.Net.WebUtility.HtmlDecode(plainText);
                        plainText = plainText.Trim();

                        _logger.LogInformation("Extracted {CharCount} characters from RTF document using RtfPipe", plainText.Length);
                        return plainText;
                    }
                    catch (Exception rtfEx)
                    {
                        _logger.LogWarning(rtfEx, "RtfPipe failed, attempting manual RTF parsing");

                        // Fallback: Manual RTF parsing
                        var manualText = ExtractTextFromRtfManually(rtfContent);
                        _logger.LogInformation("Extracted {CharCount} characters from RTF document using manual parsing", manualText.Length);
                        return manualText;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to extract text from RTF document");
                    throw;
                }
            }, cancellationToken);
        }

        private string ExtractTextFromRtfManually(string rtfContent)
        {
            try
            {
                var text = new StringBuilder();
                bool inControlWord = false;
                int groupLevel = 0;

                for (int i = 0; i < rtfContent.Length; i++)
                {
                    char c = rtfContent[i];

                    switch (c)
                    {
                        case '{':
                            groupLevel++;
                            break;

                        case '}':
                            groupLevel--;
                            break;

                        case '\\':
                            inControlWord = true;
                            // Skip control words
                            while (i + 1 < rtfContent.Length &&
                                   (char.IsLetter(rtfContent[i + 1]) || rtfContent[i + 1] == '\\'))
                            {
                                i++;
                            }
                            // Skip optional numeric parameter
                            while (i + 1 < rtfContent.Length &&
                                   (char.IsDigit(rtfContent[i + 1]) || rtfContent[i + 1] == '-'))
                            {
                                i++;
                            }
                            // Skip delimiter space
                            if (i + 1 < rtfContent.Length && rtfContent[i + 1] == ' ')
                            {
                                i++;
                            }
                            inControlWord = false;
                            break;

                        default:
                            if (!inControlWord && groupLevel <= 1)
                            {
                                // Only add text that's not in control words and at top level
                                if (char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c))
                                {
                                    text.Append(c);
                                }
                            }
                            break;
                    }
                }

                // Clean up the extracted text
                var result = text.ToString();
                result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " "); // Normalize whitespace
                result = result.Trim();

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Manual RTF parsing failed");
                return string.Empty;
            }
        }

        private List<ExtractedField> ExtractFields(string text)
        {
            var fields = new List<ExtractedField>();

            try
            {
                // Extract common document fields
                var patterns = new Dictionary<string, string[]>
                {
                    ["InvoiceNumber"] = new[] { @"(?:faktura|invoice)[\s#:]*(\d+)", @"(?:nr|number|no)[\s#:]*(\d+)" },
                    ["CustomerName"] = new[] { @"(?:till|to|kund|customer)[\s:]*([A-ZÅÄÖ][a-zåäö\s]+(?:[A-ZÅÄÖ][a-zåäö\s]*)*)" },
                    ["SupplierName"] = new[] { @"(?:från|from|leverantör|supplier)[\s:]*([A-ZÅÄÖ][a-zåäö\s]+(?:[A-ZÅÄÖ][a-zåäö\s]*)*)" }
                };

                foreach (var fieldType in patterns)
                {
                    foreach (var pattern in fieldType.Value)
                    {
                        var matches = System.Text.RegularExpressions.Regex.Matches(text, pattern, 
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        
                        foreach (System.Text.RegularExpressions.Match match in matches)
                        {
                            if (match.Groups.Count > 1)
                            {
                                var value = match.Groups[1].Value.Trim();
                                if (value.Length > 2 && value.Length < 100)
                                {
                                    fields.Add(new ExtractedField
                                    {
                                        Name = fieldType.Key,
                                        Value = value,
                                        Confidence = 0.8
                                    });
                                }
                            }
                        }
                    }
                }

                _logger.LogDebug("Extracted {FieldCount} fields from Word document", fields.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract fields from Word document");
            }

            return fields;
        }

        private double CalculateConfidence(ExtractedData data, string rawText)
        {
            double confidence = 0.0;

            // Base confidence from text quality
            if (!string.IsNullOrWhiteSpace(rawText))
            {
                confidence += 0.4; // Higher base score for structured Word documents
                
                // Bonus for text length
                if (rawText.Length > 100) confidence += 0.1;
                if (rawText.Length > 500) confidence += 0.1;
            }

            // Confidence from extracted amounts
            if (data.Amounts.Any())
            {
                confidence += 0.2;
                if (data.Amounts.Count > 1) confidence += 0.1;
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

            return Math.Min(confidence, 1.0);
        }
    }
}
