using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public class InvoiceCharges
{
    public decimal? fees { get; set; }
    public decimal? shipping { get; set; }
    public decimal? vat { get; set; }

    // Optional: expose individual VAT buckets for transparency
    public decimal? vat6 { get; set; }
    public decimal? vat12 { get; set; }
    public decimal? vat25 { get; set; }
}

public static class InvoiceChargesParserService
{
    // --- Labels ---
    private static readonly Regex LabelExp =
        new Regex(@"(?i)\b(?:Exp(?:editions)?\.?\s*Avg(?:ift)?)\b", RegexOptions.Compiled);

    private static readonly Regex LabelFrakt =
        new Regex(@"(?i)\bfrakt(?:avgift|kostnad)?\b", RegexOptions.Compiled);

    // Matches: "Moms 6%", "MOMS 12 %", "moms 25"
    private static readonly Regex LabelMomsRate =
        new Regex(@"(?i)\bmoms\s*(6|12|25)\s*%?", RegexOptions.Compiled);

    // Generic VAT label when rates are not broken out
    private static readonly Regex LabelMomsGeneric =
        new Regex(@"(?i)\bmoms\b(?!\s*\d)", RegexOptions.Compiled);

    // --- Amounts ---
    // Allows +/- sign, thousand separators (space/nbsp/thin space/dot), and comma or dot as decimal separator
    private static readonly Regex AmountRegex =
        new Regex(@"[+-]?\d{1,3}(?:[ \u00A0\u202F\.]\d{3})*(?:[.,]\d{2})|\b[+-]?\d+(?:[.,]\d{2})\b",
                  RegexOptions.Compiled);

    /// <summary>
    /// Extracts Expeditionsavgift, Frakt, and Moms (VAT) from an invoice-like free text.
    /// Moms is computed as the sum of 6/12/25% buckets if present; otherwise falls back to a generic "Moms" label.
    /// Returns an InvoiceCharges object with nulls if nothing could be found confidently.
    /// </summary>
    public static InvoiceCharges ExtractCharges(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new InvoiceCharges();

        var text = Normalize(raw);

        var charges = new InvoiceCharges
        {
            fees = ExtractSingleByLabel(text, LabelExp),
            shipping             = ExtractSingleByLabel(text, LabelFrakt),
        };

        // VAT: prefer explicit rate buckets (6/12/25) and sum them
        var momsByRate = ExtractMomsByRate(text);
        if (momsByRate.Count > 0)
        {
            charges.vat6  = momsByRate.TryGetValue(6,  out var m6)  ? m6  : (decimal?)null;
            charges.vat12 = momsByRate.TryGetValue(12, out var m12) ? m12 : (decimal?)null;
            charges.vat25 = momsByRate.TryGetValue(25, out var m25) ? m25 : (decimal?)null;
            charges.vat = new[] { charges.vat6, charges.vat12, charges.vat25 }
                 .Sum(v => v ?? 0m);
        }
        else
        {
            // Fallback: a single "Moms" column/label
            charges.vat = ExtractSingleByLabel(text, LabelMomsGeneric);
        }

        return charges;
    }

    // ---- Helpers ----

    private static string Normalize(string raw)
    {
        var t = raw.Replace("\r\n", "\n").Replace('\r', '\n');
        // Strip control chars but keep newline and tab
        t = new string(t.Where(c => c == '\n' || c == '\t' || c >= ' ').ToArray());
        return t;
    }

    /// <summary>
    /// Extract an amount for a single label using a cascade:
    /// 1) Same line (prefer amounts to the right of the label).
    /// 2) Table layout: label in header line, amount in a subsequent "value" line aligned by column.
    /// 3) Heuristic window: first amount appearing within N chars after the label.
    /// </summary>
    private static decimal? ExtractSingleByLabel(string text, Regex label)
    {
        // 1) Inline on the same line
        var inline = TryInline(text, label);
        if (inline.HasValue) return inline;

        // 2) Table-like alignment
        var tab = TryTableAlignedSingle(text, label);
        if (tab.HasValue) return tab;

        // 3) Heuristic window after the label
        var near = TryNearAfter(text, label, maxChars: 140);
        if (near.HasValue) return near;

        return null;
    }

    private static decimal? TryInline(string text, Regex label)
    {
        foreach (var line in text.Split('\n'))
        {
            if (!label.IsMatch(line)) continue;

            var lab = label.Match(line);
            var nums = AmountRegex.Matches(line).Cast<Match>()
                                  // Prefer numbers to the right of the label; then nearest by index
                                  .OrderBy(m => m.Index < lab.Index ? 1 : 0)
                                  .ThenBy(m => Math.Abs(m.Index - lab.Index))
                                  .ToList();

            foreach (var m in nums)
                if (TryParseAmount(m.Value, out var val)) return val;
        }
        return null;
    }

    /// <summary>
    /// Find a header line containing the label and align to the closest amount
    /// on the next non-empty "value" line (looks up to 3 lines ahead).
    /// </summary>
    private static decimal? TryTableAlignedSingle(string text, Regex label)
    {
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var header = lines[i];
            if (!label.IsMatch(header)) continue;

            var headers = ExtractHeaders(header);

            // Find header index matching the label
            var headerIndex = headers.FindIndex(h =>
                label.IsMatch(h)
            );

            if (headerIndex >= 0)
            {
                // Find the next line with at least one amount (up to 3 lines ahead)
                for (int j = 1; j <= 3 && i + j < lines.Length; j++)
                {
                    var valLine = lines[i + j];
                    var nums = AmountRegex.Matches(valLine).Cast<Match>().ToList();
                    if (nums.Count == 0) continue;

                    // Pick the amount whose index is the same as the header's index (column alignment)
                    if (nums.Count > headerIndex) {
                        var value = nums[headerIndex];
                        if (TryParseAmount(value.Value, out var val))
                            return val;
                        break; // Stop after the first value line with amounts
                    }
                }
            }
        }

        return null;
    }

    public static List<string> ExtractHeaders(string input)
    {
        var tokens = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();

        // Matchar 6%, 12%, 25% samt ev. decimal med punkt eller komma (t.ex. 5,5%)
        var PercentToken = new Regex(@"^\d+([.,]\d+)?%$", RegexOptions.Compiled);
        for (int i = 0; i < tokens.Length; i++)
        {
            string current = tokens[i];

            // Bygg ut rubriken med följande token(s) enligt reglerna
            while (i + 1 < tokens.Length)
            {
                string next = tokens[i + 1];

                bool shouldAttach =
                    current.EndsWith(".") ||                   // t.ex. "Omv." + "skaskyldighet"
                    char.IsLower(next[0]) ||                   // nästa ord börjar med liten bokstav → hänger ihop
                    (current.Equals("Moms", StringComparison.OrdinalIgnoreCase) && PercentToken.IsMatch(next)); // "Moms" + "6%"

                if (!shouldAttach) break;

                current += " " + next;
                i++;
            }

            result.Add(current);
        }

        return result;
    }

    private static decimal? TryNearAfter(string text, Regex label, int maxChars)
    {
        var m = label.Match(text);
        if (!m.Success) return null;

        int start = m.Index + m.Length;
        int len = Math.Min(maxChars, Math.Max(0, text.Length - start));
        var window = text.Substring(start, len);

        foreach (Match num in AmountRegex.Matches(window))
            if (TryParseAmount(num.Value, out var val)) return val;

        return null;
    }

    /// <summary>
    /// Extracts VAT amounts for explicit rates (6/12/25) by aligning header labels to values on the next line.
    /// If found, returns a dictionary like {6: amount, 12: amount, 25: amount}.
    /// Includes a fallback where each rate appears on its own line (e.g., "Moms 25% 507,80").
    /// Enhanced to handle cases where VAT rate and amount are on consecutive lines.
    /// </summary>
    private static Dictionary<int, decimal> ExtractMomsByRate(string text)
    {
        var result = new Dictionary<int, decimal>();
        var lines = text.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var header = lines[i];

            var rateMatches = LabelMomsRate.Matches(header).Cast<Match>().ToList();
            if (rateMatches.Count == 0) continue;

            var headers = ExtractHeaders(header);

            // Find the value line (up to 3 lines ahead) containing amounts
            List<Match>? nums = null;
            for (int j = 1; j <= 3 && i + j < lines.Length; j++)
            {
                var valueLine = lines[i + j];
                var mnums = AmountRegex.Matches(valueLine).Cast<Match>().ToList();
                if (mnums.Count > 0)
                {
                    nums = mnums;
                    break;
                }
            }
            if (nums == null) continue;

            // For each rate in the header, find the corresponding amount by column alignment
            foreach (var rm in rateMatches.OrderBy(m => m.Index))
            {
                int rate = int.Parse(rm.Groups[1].Value, CultureInfo.InvariantCulture);

                //Find header index based on rate.
                var headerIndex = headers.FindIndex(h =>
                    LabelMomsRate.IsMatch(h) && h.IndexOf(rate.ToString(), StringComparison.Ordinal) >= 0
                );

                if (headerIndex >= 0 && headerIndex < nums.Count && TryParseAmount(nums[headerIndex].Value, out var val))
                {
                    result[rate] = val;
                }
            }

            // Stop at the first block found (typical invoices have one summary block)
            if (result.Count > 0) return result;
        }

        // Fallback: each rate printed on its own line, e.g. "Moms 25% 507,80"
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var m = LabelMomsRate.Match(line);
            if (!m.Success) continue;

            var nums = AmountRegex.Matches(line).Cast<Match>()
                                  .Where(x => x.Index > m.Index) // amounts after the label
                                  .ToList();
            if (nums.Count == 0) continue;

            int rate = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            if (TryParseAmount(nums[0].Value, out var val))
                result[rate] = val;
        }

        // Enhanced fallback: VAT rate on one line, amount on the next line
        // This handles cases like:
        // "Moms 25%"
        // "829,60"
        for (int i = 0; i < lines.Length - 1; i++)
        {
            var line = lines[i].Trim();
            var nextLine = lines[i + 1].Trim();

            var m = LabelMomsRate.Match(line);
            if (!m.Success) continue;

            // Check if the current line contains ONLY the VAT rate (no amounts)
            var currentLineAmounts = AmountRegex.Matches(line).Cast<Match>().ToList();
            if (currentLineAmounts.Count > 0) continue; // Skip if amounts are already on this line

            // Look for amounts on the next line
            var nextLineAmounts = AmountRegex.Matches(nextLine).Cast<Match>().ToList();
            if (nextLineAmounts.Count == 0) continue;

            int rate = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);

            // Take the first amount from the next line
            if (TryParseAmount(nextLineAmounts[0].Value, out var val))
            {
                result[rate] = val;
            }
        }

        return result;
    }

    /// <summary>
    /// Robust amount parsing:
    /// - Removes spaces/nbsp/thin spaces as thousands separators.
    /// - If both comma and dot occur, treats the last occurrence as the decimal separator and the other as thousands.
    /// - Normalizes to invariant culture for parsing.
    /// - Strips trailing currency tokens like "kr" or "SEK".
    /// </summary>
    private static bool TryParseAmount(string s, out decimal value)
    {
        // Remove thousand separators (space/nbsp/thin space)
        var cleaned = s.Replace(" ", "")
                       .Replace("\u00A0", "")
                       .Replace("\u202F", "");

        int lastComma = cleaned.LastIndexOf(',');
        int lastDot = cleaned.LastIndexOf('.');

        if (lastComma >= 0 && lastDot >= 0)
        {
            bool commaIsDecimal = lastComma > lastDot;
            var decSep = commaIsDecimal ? ',' : '.';
            var thouSep = commaIsDecimal ? '.' : ',';
            cleaned = cleaned.Replace(thouSep.ToString(), "");
            cleaned = cleaned.Replace(decSep, '.');
        }
        else if (lastComma >= 0)
        {
            cleaned = cleaned.Replace(',', '.');
        }
        else
        {
            // If multiple dots exist, keep only the last one as decimal separator
            var parts = cleaned.Split('.');
            if (parts.Length > 2)
                cleaned = string.Concat(parts.Take(parts.Length - 1)) + "." + parts.Last();
        }

        // Strip currency tokens
        cleaned = Regex.Replace(cleaned, @"(?i)\s*(kr|SEK)$", "").Trim();

        return decimal.TryParse(cleaned,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out value);
    }
}