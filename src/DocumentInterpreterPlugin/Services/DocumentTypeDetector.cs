using DocumentInterpreter.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DocumentInterpreter.Services
{
    /// <summary>
    /// Detects document types and provides technique prioritization
    /// </summary>
    public class DocumentTypeDetector : IDocumentTypeDetector
    {
        private readonly ILogger<DocumentTypeDetector> _logger;

        // Document type patterns
        private static readonly Dictionary<string, (Regex[] Patterns, double BaseConfidence)> DocumentPatterns = new()
        {
            ["CustomerInvoice"] = (new[]
            {
                new Regex(@"(?i)\b(faktura|invoice)\b", RegexOptions.Compiled),
                new Regex(@"(?i)\b(fakturanummer|invoice\s*number)\b", RegexOptions.Compiled),
                new Regex(@"(?i)\b(moms|vat)\b", RegexOptions.Compiled),
                new Regex(@"(?i)\b(totalt|total|summa)\b", RegexOptions.Compiled),
                new Regex(@"(?i)\b(kundnummer|customer\s*number)\b", RegexOptions.Compiled)
            }, 0.8),

            ["SupplierInvoice"] = (new[]
            {
                new Regex(@"(?i)\b(leverantörsfaktura|supplier\s*invoice)\b", RegexOptions.Compiled),
                new Regex(@"(?i)\b(leverantör|supplier)\b", RegexOptions.Compiled),
                new Regex(@"(?i)\b(faktura|invoice)\b", RegexOptions.Compiled),
                new Regex(@"(?i)\b(moms|vat)\b", RegexOptions.Compiled),
                new Regex(@"(?i)\b(att\s*betala|amount\s*due)\b", RegexOptions.Compiled)
            }, 0.7),

            ["CashReceipt"] = (new[]
            {
                new Regex(@"(?i)\b(kvitto|receipt)\b", RegexOptions.Compiled),
                new Regex(@"(?i)\b(kassabon|cash\s*receipt)\b", RegexOptions.Compiled),
                new Regex(@"(?i)\b(butik|store|shop)\b", RegexOptions.Compiled),
                new Regex(@"(?i)\b(kort|card|kontant|cash)\b", RegexOptions.Compiled)
            }, 0.6),

            ["Contract"] = (new[]
            {
                new Regex(@"(?i)\b(kontrakt|contract|avtal|agreement)\b", RegexOptions.Compiled),
                new Regex(@"(?i)\b(villkor|terms|conditions)\b", RegexOptions.Compiled),
                new Regex(@"(?i)\b(undertecknad|signed|signature)\b", RegexOptions.Compiled),
                new Regex(@"(?i)\b(part|party|parter|parties)\b", RegexOptions.Compiled)
            }, 0.5),

            ["BankStatement"] = (new[]
            {
                new Regex(@"(?i)\b(kontoutdrag|bank\s*statement)\b", RegexOptions.Compiled),
                new Regex(@"(?i)\b(saldo|balance)\b", RegexOptions.Compiled),
                new Regex(@"(?i)\b(transaktion|transaction)\b", RegexOptions.Compiled),
                new Regex(@"(?i)\b(bank|swedbank|handelsbanken|seb|nordea)\b", RegexOptions.Compiled)
            }, 0.4)
        };

        // Technique priorities by document type
        private static readonly Dictionary<string, Dictionary<string, int>> TechniquePriorities = new()
        {
            ["CustomerInvoice"] = new()
            {
                ["AzureDocumentIntelligence"] = 100,
                ["iText7"] = 90,
                ["TesseractOCR"] = 70,
                ["AzureComputerVision"] = 60
            },
            ["SupplierInvoice"] = new()
            {
                ["AzureDocumentIntelligence"] = 100,
                ["iText7"] = 90,
                ["TesseractOCR"] = 70,
                ["AzureComputerVision"] = 60
            },
            ["CashReceipt"] = new()
            {
                ["TesseractOCR"] = 100,
                ["AzureComputerVision"] = 90,
                ["AzureDocumentIntelligence"] = 80,
                ["iText7"] = 60
            },
            ["Contract"] = new()
            {
                ["iText7"] = 100,
                ["AzureDocumentIntelligence"] = 90,
                ["TesseractOCR"] = 70,
                ["AzureComputerVision"] = 50
            },
            ["BankStatement"] = new()
            {
                ["iText7"] = 100,
                ["AzureDocumentIntelligence"] = 90,
                ["TesseractOCR"] = 60,
                ["AzureComputerVision"] = 50
            },
            ["Unknown"] = new()
            {
                ["AzureDocumentIntelligence"] = 90,
                ["iText7"] = 80,
                ["TesseractOCR"] = 70,
                ["AzureComputerVision"] = 60
            }
        };

        public DocumentTypeDetector(ILogger<DocumentTypeDetector> logger)
        {
            _logger = logger;
        }

        public (string DocumentType, double Confidence) DetectDocumentType(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return ("Unknown", 0.0);

            var scores = new Dictionary<string, double>();

            foreach (var (docType, (patterns, baseConfidence)) in DocumentPatterns)
            {
                var score = CalculateDocumentTypeScore(text, patterns, baseConfidence);
                scores[docType] = score;
                
                _logger.LogDebug("Document type {DocType} score: {Score}", docType, score);
            }

            var bestMatch = scores.OrderByDescending(kvp => kvp.Value).First();
            
            // Only return a specific type if confidence is above threshold
            if (bestMatch.Value < 0.3)
            {
                _logger.LogDebug("No document type detected with sufficient confidence. Best: {Type} ({Score})", 
                    bestMatch.Key, bestMatch.Value);
                return ("Unknown", 0.0);
            }

            _logger.LogDebug("Detected document type: {Type} with confidence: {Confidence}", 
                bestMatch.Key, bestMatch.Value);

            return (bestMatch.Key, bestMatch.Value);
        }

        public Dictionary<string, int> GetTechniquePriorities(string documentType)
        {
            if (TechniquePriorities.TryGetValue(documentType, out var priorities))
            {
                return new Dictionary<string, int>(priorities);
            }

            // Return default priorities for unknown document types
            return new Dictionary<string, int>(TechniquePriorities["Unknown"]);
        }

        public bool IsLikelyInvoice(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var invoicePatterns = new[]
            {
                new Regex(@"(?i)\b(faktura|invoice)\b", RegexOptions.Compiled),
                new Regex(@"(?i)\b(fakturanummer|invoice\s*number)\b", RegexOptions.Compiled),
                new Regex(@"(?i)\b(moms|vat)\b", RegexOptions.Compiled)
            };

            var matches = invoicePatterns.Count(pattern => pattern.IsMatch(text));
            return matches >= 2; // At least 2 invoice indicators
        }

        public bool IsLikelyReceipt(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var receiptPatterns = new[]
            {
                new Regex(@"(?i)\b(kvitto|receipt)\b", RegexOptions.Compiled),
                new Regex(@"(?i)\b(kassabon|cash\s*receipt)\b", RegexOptions.Compiled),
                new Regex(@"(?i)\b(butik|store)\b", RegexOptions.Compiled)
            };

            var matches = receiptPatterns.Count(pattern => pattern.IsMatch(text));
            return matches >= 1; // At least 1 receipt indicator
        }

        private double CalculateDocumentTypeScore(string text, Regex[] patterns, double baseConfidence)
        {
            var textLower = text.ToLowerInvariant();
            var matchCount = 0;
            var totalMatches = 0;

            foreach (var pattern in patterns)
            {
                var matches = pattern.Matches(text);
                if (matches.Count > 0)
                {
                    matchCount++;
                    totalMatches += matches.Count;
                }
            }

            if (matchCount == 0)
                return 0.0;

            // Calculate score based on pattern matches
            var patternScore = (double)matchCount / patterns.Length;
            var densityScore = Math.Min(1.0, totalMatches / 10.0); // Bonus for multiple matches
            
            var finalScore = baseConfidence * patternScore * (0.7 + 0.3 * densityScore);

            // Boost score for documents with structured content
            if (HasStructuredContent(text))
                finalScore *= 1.2;

            // Boost score for documents with amounts
            if (HasMonetaryAmounts(text))
                finalScore *= 1.1;

            return Math.Min(1.0, finalScore);
        }

        private bool HasStructuredContent(string text)
        {
            // Check for structured content indicators
            var structurePatterns = new[]
            {
                @"\d{4}-\d{2}-\d{2}", // Dates
                @"\d+[,\.]\d{2}", // Amounts
                @"^\s*\d+\.", // Numbered lists
                @":\s*\w+", // Key-value pairs
            };

            var matches = structurePatterns.Count(pattern => 
                Regex.IsMatch(text, pattern, RegexOptions.Multiline));

            return matches >= 2;
        }

        private bool HasMonetaryAmounts(string text)
        {
            var amountPatterns = new[]
            {
                @"\d+[,\.]\d{2}\s*(?:kr|sek|€|\$)",
                @"(?:kr|sek|€|\$)\s*\d+[,\.]\d{2}",
                @"\d{1,3}(?:\s\d{3})*[,\.]\d{2}"
            };

            return amountPatterns.Any(pattern => 
                Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase));
        }
    }
}
