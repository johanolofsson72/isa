using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DocumentInterpreter.Interfaces;
using DocumentInterpreter.Services;
using DocumentInterpreter.Models;
using Xunit;

namespace DocumentInterpreter.Tests
{
    /// <summary>
    /// Debug test for multi-technique extraction
    /// </summary>
    public class DebugMultiTechniqueTest
    {
        private readonly ILogger<DocumentExtractionOrchestrator> _orchestratorLogger;
        private readonly ILogger<ExtractionResultFusion> _fusionLogger;
        private readonly ILogger<SwedishNumberParser> _numberParserLogger;
        private readonly ILogger<DocumentTypeDetector> _typeDetectorLogger;
        private readonly ILogger<IText7ExtractionTechnique> _itext7Logger;

        public DebugMultiTechniqueTest()
        {
            _orchestratorLogger = NullLogger<DocumentExtractionOrchestrator>.Instance;
            _fusionLogger = NullLogger<ExtractionResultFusion>.Instance;
            _numberParserLogger = NullLogger<SwedishNumberParser>.Instance;
            _typeDetectorLogger = NullLogger<DocumentTypeDetector>.Instance;
            _itext7Logger = NullLogger<IText7ExtractionTechnique>.Instance;
        }

        [Fact]
        public async Task TestMultiTechniqueExtractionDebug()
        {
            // Arrange
            var resultFusion = new ExtractionResultFusion(_fusionLogger);
            var orchestrator = new DocumentExtractionOrchestrator(_orchestratorLogger, resultFusion);
            var numberParser = new SwedishNumberParser(_numberParserLogger);
            var typeDetector = new DocumentTypeDetector(_typeDetectorLogger);
            var itext7Technique = new IText7ExtractionTechnique(_itext7Logger, numberParser, typeDetector);

            orchestrator.RegisterTechnique(itext7Technique);

            // Test with demo.pdf
            var testFilePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "ekonomi", "demo.pdf");
            
            if (!File.Exists(testFilePath))
            {
                // Skip test if file doesn't exist
                Assert.True(true, $"Test file not found at: {testFilePath}");
                return;
            }

            // Act
            var result = await orchestrator.ExtractAsync(testFilePath);

            // Assert and Debug
            Assert.NotNull(result);
            Assert.NotEmpty(result.TechniqueResults);
            
            Console.WriteLine($"=== MULTI-TECHNIQUE EXTRACTION DEBUG ===");
            Console.WriteLine($"File: {testFilePath}");
            Console.WriteLine($"Overall Confidence: {result.OverallConfidence:F2}");
            Console.WriteLine($"Successful Techniques: {result.QualityMetrics.SuccessfulTechniques}");
            Console.WriteLine($"Failed Techniques: {result.QualityMetrics.FailedTechniques}");
            
            foreach (var techniqueResult in result.TechniqueResults)
            {
                Console.WriteLine($"\n--- {techniqueResult.TechniqueName} ---");
                Console.WriteLine($"Success: {techniqueResult.IsSuccessful}");
                Console.WriteLine($"Confidence: {techniqueResult.ConfidenceScore:F2}");
                Console.WriteLine($"Processing Time: {techniqueResult.ProcessingTimeMs}ms");
                Console.WriteLine($"Raw Text Length: {techniqueResult.RawText?.Length ?? 0}");
                
                if (techniqueResult.IsSuccessful)
                {
                    Console.WriteLine($"Raw Text Preview: {techniqueResult.RawText?.Substring(0, Math.Min(200, techniqueResult.RawText?.Length ?? 0))}...");
                    Console.WriteLine($"Amounts Found: {techniqueResult.ExtractedData.Amounts.Count}");
                    
                    foreach (var amount in techniqueResult.ExtractedData.Amounts)
                    {
                        Console.WriteLine($"  {amount.Type}: {amount.Value} {amount.Currency} (confidence: {amount.Confidence:F2})");
                    }
                }
                else
                {
                    Console.WriteLine($"Error: {techniqueResult.ErrorMessage}");
                }
            }
            
            Console.WriteLine($"\n=== CONSOLIDATED RESULTS ===");
            Console.WriteLine($"Total Amounts: {result.ConsolidatedData.Amounts.Count}");
            foreach (var amount in result.ConsolidatedData.Amounts)
            {
                Console.WriteLine($"  {amount.Type}: {amount.Value} {amount.Currency}");
            }
            
            // Verify we got some results
            Assert.True(result.QualityMetrics.SuccessfulTechniques > 0, "At least one technique should succeed");
            
            // Check if we got raw text
            var successfulWithText = result.TechniqueResults
                .Where(r => r.IsSuccessful && !string.IsNullOrWhiteSpace(r.RawText))
                .ToList();
            
            Assert.True(successfulWithText.Any(), "At least one technique should extract text");
        }
    }
}
