using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Tests.E2E;

public class Test1AmountPatternDebugTests
{
    private readonly ITestOutputHelper _output;

    public Test1AmountPatternDebugTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Should_Debug_Amount_Pattern_Matching_For_Test1_Text()
    {
        // This is the actual text extracted from test1.pdf by Azure Document Intelligence
        var test1Text = @"BABS
KARLSKRONA STATION
VALHALLAVAGEN 1
SE-371 41 KARLSKRONA
0455-569 00
Org.nr 222000-1321
Swedbank Babs
Butiksnr: 8362907
Term-ID: 46206807
Kassörnr: 1
2016-12-30
13:29
neglitout
KÖP
665,00
SEK
TOTALT:
***** 665,00
MAESTRO
************ 9683
Personlig kod
Cal 7 000 SWE 063 088801
REF: 462068072564
AID: A0000000043060
TVR: 0000001000
TSI: E800
SPARA KVITTOT
KUNDENS EXEMPA
[19
NAME: AMAN GUPTA
BANK NAME: SWED BANK
ACCOUNT NO ..
964333429-6
CLEARING NO:
8169-5
Scanned by CamScanner";

        _output.WriteLine("🔍 DEBUGGING AMOUNT PATTERN MATCHING FOR TEST1.PDF");
        _output.WriteLine("=======================================================");
        _output.WriteLine($"Text length: {test1Text.Length} characters");
        _output.WriteLine("");
        _output.WriteLine("📝 FULL TEXT:");
        _output.WriteLine(test1Text);
        _output.WriteLine("");

        // Test our enhanced amount patterns
        var amountPatterns = new[] {
            @"(?i)(?:belopp|amount)[\s:]*(\d+[,\.]\d{2})\s*(?:kr|sek|€|$)?", // "Belopp: 7193.00 kr"
            @"\b\d+[,\.]\d{2}\s*(kr|sek|€|$)\b", // Amount with currency
            @"\b\d{1,3}(?:[,\s]\d{3})*[,\.]\d{2}\b", // Formatted numbers
            @"(?i)(summa|total|belopp|amount).*?\b\d+[,\.]\d{2}\b",
            @"(?i)\b\d+[,\.]\d{2}\s*(kronor|kr)\b",
            @"(?i)(?:totalt|total|summa)[\s:]*(\d+[,\.]\d{2})", // Total amounts
            @"\b\d{4}[,\.]\d{2}\b", // 4-digit amounts like 7193.00
            @"(\d{1,4}[,\.]\d{2})\s*(?:kr|sek)", // Swedish Krona (including 4-digit amounts)
            @"\b(\d{4}[,\.]\d{2})\b", // Standalone 4-digit amounts
        };

        _output.WriteLine("🔍 TESTING AMOUNT PATTERNS:");
        _output.WriteLine("============================");

        var foundAmounts = new List<string>();

        for (int i = 0; i < amountPatterns.Length; i++)
        {
            var pattern = amountPatterns[i];
            _output.WriteLine($"Pattern {i + 1}: {pattern}");
            
            var matches = Regex.Matches(test1Text, pattern, RegexOptions.IgnoreCase);
            
            if (matches.Count > 0)
            {
                _output.WriteLine($"  ✅ MATCHES FOUND: {matches.Count}");
                foreach (Match match in matches)
                {
                    _output.WriteLine($"    - Full match: '{match.Value}'");
                    if (match.Groups.Count > 1)
                    {
                        _output.WriteLine($"    - Group 1: '{match.Groups[1].Value}'");
                    }
                    foundAmounts.Add(match.Value);
                }
            }
            else
            {
                _output.WriteLine("  ❌ NO MATCHES");
            }
            _output.WriteLine("");
        }

        _output.WriteLine("📊 SUMMARY:");
        _output.WriteLine($"Total amounts found: {foundAmounts.Count}");
        foreach (var amount in foundAmounts.Distinct())
        {
            _output.WriteLine($"  - {amount}");
        }

        // We should find at least the "665,00" amounts
        foundAmounts.Should().NotBeEmpty("We should find at least some amounts in the text");
    }
}
