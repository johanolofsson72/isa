using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DocumentInterpreter.Tests
{
    /// <summary>
    /// Debug test for plugin initialization and orchestrator usage
    /// </summary>
    public class DebugPluginTest
    {
        [Fact]
        public async Task TestPluginInitializationAndOrchestrator()
        {
            // Arrange
            var configuration = new ConfigurationBuilder().Build();
            var logger = NullLogger<Plugin>.Instance;

            // Act - Create plugin (this should initialize orchestrator)
            var plugin = new Plugin(configuration, logger);

            // Test with demo.pdf
            var testFilePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "ekonomi", "demo.pdf");
            
            if (!File.Exists(testFilePath))
            {
                // Skip test if file doesn't exist
                Assert.True(true, $"Test file not found at: {testFilePath}");
                return;
            }

            Console.WriteLine($"=== PLUGIN DEBUG TEST ===");
            Console.WriteLine($"File: {testFilePath}");
            Console.WriteLine($"File exists: {File.Exists(testFilePath)}");
            Console.WriteLine($"File size: {new FileInfo(testFilePath).Length} bytes");

            // Act - Test document interpretation
            var result = await plugin.InterpretDocumentFromFileAsync(testFilePath, null, "DebugTest");

            // Assert and Debug
            Console.WriteLine($"\n=== PLUGIN RESULT ===");
            Console.WriteLine($"Result: {result}");
            
            // Parse JSON result
            var jsonResult = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(result);
            
            Console.WriteLine($"Status: {jsonResult.GetProperty("status").GetString()}");
            Console.WriteLine($"Document Type: {jsonResult.GetProperty("documentType").GetString()}");
            Console.WriteLine($"Confidence: {jsonResult.GetProperty("confidence").GetDouble()}");
            Console.WriteLine($"Raw Text Length: {jsonResult.GetProperty("rawTextLength").GetInt32()}");
            
            if (jsonResult.TryGetProperty("extractedData", out var extractedData))
            {
                if (extractedData.TryGetProperty("amount", out var amount))
                {
                    Console.WriteLine($"Amount: {amount.GetString()}");
                }
                if (extractedData.TryGetProperty("rawText", out var rawText))
                {
                    var text = rawText.GetString() ?? "";
                    Console.WriteLine($"Raw Text Preview: {text.Substring(0, Math.Min(200, text.Length))}...");
                }
            }
            
            // Verify we got some meaningful results
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            
            // The result should not be "Unreadable" with 0 confidence if multi-technique works
            var status = jsonResult.GetProperty("status").GetString();
            var confidence = jsonResult.GetProperty("confidence").GetDouble();
            var rawTextLength = jsonResult.GetProperty("rawTextLength").GetInt32();
            
            Console.WriteLine($"\n=== ASSERTIONS ===");
            Console.WriteLine($"Status should not be 'Unreadable': {status != "Unreadable"}");
            Console.WriteLine($"Confidence should be > 0: {confidence > 0}");
            Console.WriteLine($"Raw text length should be > 0: {rawTextLength > 0}");
            
            // If multi-technique extraction works, we should get text and reasonable confidence
            if (status == "Unreadable" && confidence == 0 && rawTextLength == 0)
            {
                Assert.Fail("Multi-technique extraction appears to not be working - got Unreadable status with 0 confidence and no text");
            }
        }
    }
}
