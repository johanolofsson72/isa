using DocumentInterpreter.Interfaces;
using DocumentInterpreter.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentInterpreter.Services
{
    /// <summary>
    /// Orchestrates multiple document extraction techniques in parallel
    /// </summary>
    public class DocumentExtractionOrchestrator : IDocumentExtractionOrchestrator
    {
        private readonly ILogger<DocumentExtractionOrchestrator> _logger;
        private readonly IExtractionResultFusion _resultFusion;
        private readonly List<IDocumentExtractionTechnique> _techniques;

        public DocumentExtractionOrchestrator(
            ILogger<DocumentExtractionOrchestrator> logger,
            IExtractionResultFusion resultFusion)
        {
            _logger = logger;
            _resultFusion = resultFusion;
            _techniques = new List<IDocumentExtractionTechnique>();
        }

        public void RegisterTechnique(IDocumentExtractionTechnique technique)
        {
            if (technique == null)
                throw new ArgumentNullException(nameof(technique));

            _techniques.Add(technique);
            _logger.LogDebug("Registered extraction technique: {TechniqueName} (Priority: {Priority}, Available: {Available})",
                technique.TechniqueName, technique.Priority, technique.IsAvailable);
        }

        public IDocumentExtractionTechnique[] GetRegisteredTechniques()
        {
            return _techniques.ToArray();
        }

        public async Task<ConsolidatedExtractionResult> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger.LogInformation("Starting multi-technique extraction for file: {FilePath}", filePath);

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"File not found: {filePath}");
                }

                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                var result = await ExtractAsync(fileStream, Path.GetFileName(filePath), cancellationToken);
                result.TotalProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Multi-technique extraction failed for file: {FilePath}", filePath);
                throw;
            }
        }

        public async Task<ConsolidatedExtractionResult> ExtractAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger.LogInformation("Starting multi-technique extraction for stream: {FileName}", fileName);

                // Get applicable techniques for this file
                var applicableTechniques = GetApplicableTechniques(fileName);
                
                if (!applicableTechniques.Any())
                {
                    _logger.LogWarning("No applicable extraction techniques found for file: {FileName}", fileName);
                    return new ConsolidatedExtractionResult
                    {
                        TotalProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                        OverallConfidence = 0.0
                    };
                }

                _logger.LogDebug("Found {TechniqueCount} applicable techniques for {FileName}: {Techniques}",
                    applicableTechniques.Count, fileName, 
                    string.Join(", ", applicableTechniques.Select(t => t.TechniqueName)));

                // Execute all techniques in parallel
                var results = await ExecuteTechniquesInParallelAsync(stream, fileName, applicableTechniques, cancellationToken);

                // Fuse results into consolidated output
                var consolidatedResult = _resultFusion.FuseResults(results.ToArray());
                consolidatedResult.TotalProcessingTimeMs = stopwatch.ElapsedMilliseconds;

                _logger.LogInformation("Multi-technique extraction completed for {FileName}. " +
                    "Successful techniques: {SuccessfulCount}/{TotalCount}, Overall confidence: {Confidence:F2}",
                    fileName, consolidatedResult.QualityMetrics.SuccessfulTechniques,
                    results.Count, consolidatedResult.OverallConfidence);

                // Log details about each technique result
                foreach (var result in results)
                {
                    _logger.LogInformation("🔍 Technique: {TechniqueName}, Success: {Success}, Confidence: {Confidence:F2}, Text length: {TextLength}",
                        result.TechniqueName, result.IsSuccessful, result.ConfidenceScore, result.RawText?.Length ?? 0);
                }

                return consolidatedResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Multi-technique extraction failed for stream: {FileName}", fileName);
                throw;
            }
        }

        private List<IDocumentExtractionTechnique> GetApplicableTechniques(string fileName)
        {
            return _techniques
                .Where(t => t.IsAvailable && t.CanHandle(fileName))
                .OrderByDescending(t => t.Priority)
                .ToList();
        }

        private async Task<List<ExtractionResult>> ExecuteTechniquesInParallelAsync(
            Stream stream, 
            string fileName, 
            List<IDocumentExtractionTechnique> techniques,
            CancellationToken cancellationToken)
        {
            var results = new ConcurrentBag<ExtractionResult>();
            var semaphore = new SemaphoreSlim(Environment.ProcessorCount); // Limit concurrent operations

            var tasks = techniques.Select(async technique =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var techniqueStopwatch = Stopwatch.StartNew();
                    _logger.LogDebug("Starting extraction with technique: {TechniqueName}", technique.TechniqueName);

                    // Create a fresh stream copy for each technique to avoid corruption
                    var streamCopy = await CreateFreshStreamCopyAsync(stream);

                    var result = await technique.ExtractAsync(streamCopy, fileName, cancellationToken);
                    result.ProcessingTimeMs = techniqueStopwatch.ElapsedMilliseconds;
                    
                    results.Add(result);
                    
                    _logger.LogDebug("Technique {TechniqueName} completed in {ProcessingTime}ms. " +
                        "Success: {Success}, Confidence: {Confidence:F2}, Text length: {TextLength}",
                        technique.TechniqueName, result.ProcessingTimeMs, result.IsSuccessful, 
                        result.ConfidenceScore, result.RawText?.Length ?? 0);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Technique {TechniqueName} failed", technique.TechniqueName);
                    
                    results.Add(new ExtractionResult
                    {
                        TechniqueName = technique.TechniqueName,
                        IsSuccessful = false,
                        ErrorMessage = ex.Message,
                        ConfidenceScore = 0.0
                    });
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            
            return results.ToList();
        }

        private byte[]? _cachedStreamData;
        private readonly object _cachelock = new object();

        private Task<Stream> CreateFreshStreamCopyAsync(Stream originalStream)
        {
            try
            {
                // Cache the original stream data once
                if (_cachedStreamData == null)
                {
                    lock (_cachelock)
                    {
                        if (_cachedStreamData == null)
                        {
                            _cachedStreamData = ReadStreamToByteArray(originalStream);
                            _logger.LogDebug("Cached original stream data: {Size} bytes", _cachedStreamData.Length);

                            // Validate that the data is a valid PDF
                            if (_cachedStreamData.Length >= 4)
                            {
                                var header = System.Text.Encoding.ASCII.GetString(_cachedStreamData, 0, 4);
                                if (header == "%PDF")
                                {
                                    _logger.LogDebug("Original stream validated as PDF format");
                                }
                                else
                                {
                                    _logger.LogWarning("Original stream does not appear to be a valid PDF (header: {Header})", header);
                                }
                            }
                        }
                    }
                }

                // Create a fresh MemoryStream from the cached data
                var copy = new MemoryStream(_cachedStreamData);
                copy.Position = 0;

                _logger.LogDebug("Created fresh stream copy: {Size} bytes", copy.Length);
                return Task.FromResult<Stream>(copy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create fresh stream copy");
                throw;
            }
        }

        private byte[] ReadStreamToByteArray(Stream stream)
        {
            // Reset stream position if possible
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using (var memoryStream = new MemoryStream())
            {
                // Use synchronous copy to avoid async issues on macOS
                stream.CopyTo(memoryStream);
                var data = memoryStream.ToArray();

                // Reset original stream position if possible
                if (stream.CanSeek)
                {
                    stream.Position = 0;
                }

                return data;
            }
        }
    }
}
