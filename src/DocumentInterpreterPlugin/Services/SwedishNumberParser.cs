using DocumentInterpreter.Interfaces;
using DocumentInterpreter.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace DocumentInterpreter.Services
{
    /// <summary>
    /// Parser for Swedish number formats with space thousands separator and comma decimal
    /// </summary>
    public class SwedishNumberParser : ISwedishNumberParser
    {
        private readonly ILogger<SwedishNumberParser> _logger;

        // Regex patterns for Swedish number formats
        private static readonly Regex SwedishNumberRegex = new Regex(
            @"\b(\d{1,3}(?:[\s\u00A0\u202F]\d{3})*(?:[,]\d{1,2})?|\d+(?:[,]\d{1,2}))\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex CurrencyRegex = new Regex(
            @"\b(?:SEK|kr|kronor|EUR|€|USD|\$|DKK|NOK)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex AmountContextRegex = new Regex(
            @"(?i)(?:totalt?|total|summa|belopp|amount|att\s*betala|slutsumma|netto|brutto|moms|vat|frakt|shipping|avgift|fee|rabatt|discount)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public SwedishNumberParser(ILogger<SwedishNumberParser> logger)
        {
            _logger = logger;
        }

        public decimal? ParseSwedishNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            try
            {
                var normalized = NormalizeSwedishNumber(text);
                if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse Swedish number: {Text}", text);
            }

            return null;
        }

        public ExtractedAmount[] ExtractSwedishAmounts(string text)
        {
            var amounts = new List<ExtractedAmount>();
            
            if (string.IsNullOrWhiteSpace(text))
                return amounts.ToArray();

            var lines = text.Split('\n');
            
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex];
                var matches = SwedishNumberRegex.Matches(line);

                foreach (Match match in matches)
                {
                    var amountText = match.Value;
                    var parsedAmount = ParseSwedishNumber(amountText);

                    if (parsedAmount.HasValue && parsedAmount.Value > 0)
                    {
                        var amount = new ExtractedAmount
                        {
                            Value = parsedAmount.Value,
                            RawText = amountText,
                            Position = new DocumentPosition
                            {
                                Line = lineIndex + 1,
                                Character = match.Index
                            },
                            Context = GetContext(lines, lineIndex, match.Index, 50),
                            Currency = DetectCurrency(GetContext(lines, lineIndex, match.Index, 100)),
                            Type = DetectAmountType(GetContext(lines, lineIndex, match.Index, 100)),
                            Confidence = CalculateAmountConfidence(amountText, GetContext(lines, lineIndex, match.Index, 100))
                        };

                        amounts.Add(amount);
                    }
                }
            }

            return amounts.ToArray();
        }

        public string NormalizeSwedishNumber(string swedishNumber)
        {
            if (string.IsNullOrWhiteSpace(swedishNumber))
                return "0";

            // Remove all whitespace characters used as thousands separators
            var normalized = swedishNumber
                .Replace(" ", "")
                .Replace("\u00A0", "") // Non-breaking space
                .Replace("\u202F", "") // Narrow no-break space
                .Replace(".", ""); // Remove dots used as thousands separators

            // Replace comma with dot for decimal separator
            normalized = normalized.Replace(",", ".");

            // Remove any non-digit, non-dot characters
            normalized = Regex.Replace(normalized, @"[^\d\.]", "");

            return string.IsNullOrEmpty(normalized) ? "0" : normalized;
        }

        public string DetectCurrency(string context)
        {
            if (string.IsNullOrWhiteSpace(context))
                return "SEK";

            var match = CurrencyRegex.Match(context);
            if (match.Success)
            {
                var currency = match.Value.ToUpperInvariant();
                return currency switch
                {
                    "KR" or "KRONOR" => "SEK",
                    "€" => "EUR",
                    "$" => "USD",
                    _ => currency
                };
            }

            // Default to SEK for Swedish documents
            return "SEK";
        }

        private string GetContext(string[] lines, int lineIndex, int charIndex, int contextLength)
        {
            var context = "";
            
            // Get context from current line
            var line = lines[lineIndex];
            var start = Math.Max(0, charIndex - contextLength / 2);
            var end = Math.Min(line.Length, charIndex + contextLength / 2);
            context = line.Substring(start, end - start);

            // Add context from previous and next lines if needed
            if (context.Length < contextLength)
            {
                if (lineIndex > 0)
                {
                    var prevLine = lines[lineIndex - 1];
                    context = prevLine + " " + context;
                }
                
                if (lineIndex < lines.Length - 1)
                {
                    var nextLine = lines[lineIndex + 1];
                    context = context + " " + nextLine;
                }
            }

            return context.Trim();
        }

        private AmountType DetectAmountType(string context)
        {
            if (string.IsNullOrWhiteSpace(context))
                return AmountType.Unknown;

            var lowerContext = context.ToLowerInvariant();

            // Check for specific VAT rates
            if (lowerContext.Contains("moms 6") || lowerContext.Contains("vat 6"))
                return AmountType.VAT6;
            if (lowerContext.Contains("moms 12") || lowerContext.Contains("vat 12"))
                return AmountType.VAT12;
            if (lowerContext.Contains("moms 25") || lowerContext.Contains("vat 25"))
                return AmountType.VAT25;

            // Check for general VAT
            if (lowerContext.Contains("moms") || lowerContext.Contains("vat"))
                return AmountType.VAT;

            // Check for total amounts
            if (lowerContext.Contains("totalt") || lowerContext.Contains("total") || 
                lowerContext.Contains("summa") || lowerContext.Contains("att betala") ||
                lowerContext.Contains("slutsumma"))
                return AmountType.Total;

            // Check for net amounts
            if (lowerContext.Contains("netto") || lowerContext.Contains("net") ||
                lowerContext.Contains("exkl") || lowerContext.Contains("excluding"))
                return AmountType.Net;

            // Check for shipping
            if (lowerContext.Contains("frakt") || lowerContext.Contains("shipping") ||
                lowerContext.Contains("leverans"))
                return AmountType.Shipping;

            // Check for fees
            if (lowerContext.Contains("avgift") || lowerContext.Contains("fee") ||
                lowerContext.Contains("expeditionsavgift"))
                return AmountType.Fee;

            return AmountType.Unknown;
        }

        private double CalculateAmountConfidence(string amountText, string context)
        {
            double confidence = 0.5; // Base confidence

            // Higher confidence for properly formatted Swedish numbers
            if (amountText.Contains(" ") && amountText.Contains(","))
                confidence += 0.2;
            else if (amountText.Contains(","))
                confidence += 0.1;

            // Higher confidence if context contains amount-related keywords
            if (AmountContextRegex.IsMatch(context))
                confidence += 0.2;

            // Higher confidence if currency is mentioned nearby
            if (CurrencyRegex.IsMatch(context))
                confidence += 0.1;

            // Lower confidence for very small amounts (likely not totals)
            var parsedAmount = ParseSwedishNumber(amountText);
            if (parsedAmount.HasValue)
            {
                if (parsedAmount.Value < 10)
                    confidence -= 0.2;
                else if (parsedAmount.Value > 1000)
                    confidence += 0.1;
            }

            return Math.Max(0.0, Math.Min(1.0, confidence));
        }
    }
}
