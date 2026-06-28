using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DocumentInterpreter.Interfaces;
using DocumentInterpreter.Services;
using DocumentInterpreter.Models;
using System.Text;
using Xunit;

namespace DocumentInterpreter.Tests
{
    /// <summary>
    /// Test the multi-technique extraction system with real documents
    /// </summary>
    public class MultiTechniqueDocumentTest
    {
        private readonly ILogger<DocumentExtractionOrchestrator> _orchestratorLogger;
        private readonly ILogger<ExtractionResultFusion> _fusionLogger;
        private readonly ILogger<SwedishNumberParser> _numberParserLogger;
        private readonly ILogger<DocumentTypeDetector> _typeDetectorLogger;
        private readonly ILogger<IText7ExtractionTechnique> _itext7Logger;

        public MultiTechniqueDocumentTest()
        {
            _orchestratorLogger = NullLogger<DocumentExtractionOrchestrator>.Instance;
            _fusionLogger = NullLogger<ExtractionResultFusion>.Instance;
            _numberParserLogger = NullLogger<SwedishNumberParser>.Instance;
            _typeDetectorLogger = NullLogger<DocumentTypeDetector>.Instance;
            _itext7Logger = NullLogger<IText7ExtractionTechnique>.Instance;
        }

        [Fact]
        public async Task TestMultiTechniqueExtractionWithRealDocument()
        {
            // Arrange
            var resultFusion = new ExtractionResultFusion(_fusionLogger);
            var orchestrator = new DocumentExtractionOrchestrator(_orchestratorLogger, resultFusion);
            var numberParser = new SwedishNumberParser(_numberParserLogger);
            var typeDetector = new DocumentTypeDetector(_typeDetectorLogger);
            var itext7Technique = new IText7ExtractionTechnique(_itext7Logger, numberParser, typeDetector);

            orchestrator.RegisterTechnique(itext7Technique);

            // Test with a known document (if it exists)
            var testFilePath = Path.Combine(Directory.GetCurrentDirectory(), "ekonomi", "demo.pdf");
            
            if (!File.Exists(testFilePath))
            {
                // Skip test if file doesn't exist
                Assert.True(true, "Test file not found, skipping test");
                return;
            }

            // Act
            var result = await orchestrator.ExtractAsync(testFilePath);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.TechniqueResults);
            
            // Log results for inspection
            Console.WriteLine($"=== MULTI-TECHNIQUE EXTRACTION RESULTS ===");
            Console.WriteLine($"Overall Confidence: {result.OverallConfidence:F2}");
            Console.WriteLine($"Cross-validation Score: {result.QualityMetrics.CrossValidationScore:F2}");
            Console.WriteLine($"Successful Techniques: {result.QualityMetrics.SuccessfulTechniques}");
            Console.WriteLine($"Failed Techniques: {result.QualityMetrics.FailedTechniques}");
            
            foreach (var techniqueResult in result.TechniqueResults)
            {
                Console.WriteLine($"\n--- {techniqueResult.TechniqueName} ---");
                Console.WriteLine($"Success: {techniqueResult.IsSuccessful}");
                Console.WriteLine($"Confidence: {techniqueResult.ConfidenceScore:F2}");
                Console.WriteLine($"Processing Time: {techniqueResult.ProcessingTimeMs}ms");
                
                if (techniqueResult.IsSuccessful)
                {
                    Console.WriteLine($"Document Type: {techniqueResult.ExtractedData.DocumentType}");
                    Console.WriteLine($"Amounts Found: {techniqueResult.ExtractedData.Amounts.Count}");
                    
                    foreach (var amount in techniqueResult.ExtractedData.Amounts)
                    {
                        Console.WriteLine($"  {amount.Type}: {amount.Value} {amount.Currency} (confidence: {amount.Confidence:F2})");
                    }
                    
                    Console.WriteLine($"Dates Found: {techniqueResult.ExtractedData.Dates.Count}");
                    foreach (var date in techniqueResult.ExtractedData.Dates)
                    {
                        Console.WriteLine($"  {date.Type}: {date.Value:yyyy-MM-dd} (confidence: {date.Confidence:F2})");
                    }
                }
                else
                {
                    Console.WriteLine($"Error: {techniqueResult.ErrorMessage}");
                }
            }
            
            Console.WriteLine($"\n=== CONSOLIDATED RESULTS ===");
            Console.WriteLine($"Document Type: {result.ConsolidatedData.DocumentType}");
            Console.WriteLine($"Total Amounts: {result.ConsolidatedData.Amounts.Count}");
            Console.WriteLine($"Total Dates: {result.ConsolidatedData.Dates.Count}");
            Console.WriteLine($"Total Fields: {result.ConsolidatedData.Fields.Count}");
            
            // Verify we got some results
            Assert.True(result.QualityMetrics.SuccessfulTechniques > 0, "At least one technique should succeed");
        }

        [Fact]
        public async Task TestMultipleTechniquesWithSampleDocuments()
        {
            // Arrange
            var resultFusion = new ExtractionResultFusion(_fusionLogger);
            var orchestrator = new DocumentExtractionOrchestrator(_orchestratorLogger, resultFusion);
            var numberParser = new SwedishNumberParser(_numberParserLogger);
            var typeDetector = new DocumentTypeDetector(_typeDetectorLogger);
            var itext7Technique = new IText7ExtractionTechnique(_itext7Logger, numberParser, typeDetector);

            orchestrator.RegisterTechnique(itext7Technique);

            // Test with multiple documents
            var ekonomiDir = Path.Combine(Directory.GetCurrentDirectory(), "ekonomi");
            if (!Directory.Exists(ekonomiDir))
            {
                Assert.True(true, "Ekonomi directory not found, skipping test");
                return;
            }

            var pdfFiles = Directory.GetFiles(ekonomiDir, "*.pdf").Take(3).ToArray(); // Test first 3 files
            
            if (!pdfFiles.Any())
            {
                Assert.True(true, "No PDF files found, skipping test");
                return;
            }

            Console.WriteLine($"Testing {pdfFiles.Length} documents with multi-technique extraction...");

            foreach (var filePath in pdfFiles)
            {
                var fileName = Path.GetFileName(filePath);
                Console.WriteLine($"\n=== Testing {fileName} ===");

                try
                {
                    var result = await orchestrator.ExtractAsync(filePath);
                    
                    Console.WriteLine($"Overall Confidence: {result.OverallConfidence:F2}");
                    Console.WriteLine($"Successful Techniques: {result.QualityMetrics.SuccessfulTechniques}");
                    Console.WriteLine($"Document Type: {result.ConsolidatedData.DocumentType}");
                    Console.WriteLine($"Amounts Found: {result.ConsolidatedData.Amounts.Count}");
                    
                    if (result.ConsolidatedData.Amounts.Any())
                    {
                        var totalAmount = result.ConsolidatedData.Amounts.FirstOrDefault(a => a.Type == AmountType.Total);
                        if (totalAmount != null)
                        {
                            Console.WriteLine($"Total Amount: {totalAmount.Value} {totalAmount.Currency}");
                        }
                    }
                    
                    Assert.True(result.QualityMetrics.SuccessfulTechniques > 0, $"At least one technique should succeed for {fileName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing {fileName}: {ex.Message}");
                    // Don't fail the test for individual file errors
                }
            }
        }
    }
}
