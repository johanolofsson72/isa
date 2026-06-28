using DocumentInterpreter.Interfaces;
using DocumentInterpreter.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DocumentInterpreter.Services
{
    /// <summary>
    /// Intelligent fusion algorithm that combines results from multiple extraction techniques
    /// </summary>
    public class ExtractionResultFusion : IExtractionResultFusion
    {
        private readonly ILogger<ExtractionResultFusion> _logger;

        public ExtractionResultFusion(ILogger<ExtractionResultFusion> logger)
        {
            _logger = logger;
        }

        public ConsolidatedExtractionResult FuseResults(ExtractionResult[] results)
        {
            _logger.LogDebug("Starting result fusion for {ResultCount} extraction results", results.Length);

            var consolidatedResult = new ConsolidatedExtractionResult
            {
                TechniqueResults = results.ToList()
            };

            if (!results.Any())
            {
                _logger.LogWarning("No extraction results to fuse");
                return consolidatedResult;
            }

            var successfulResults = results.Where(r => r.IsSuccessful).ToArray();
            
            if (!successfulResults.Any())
            {
                _logger.LogWarning("No successful extraction results to fuse");
                consolidatedResult.QualityMetrics.FailedTechniques = results.Length;
                return consolidatedResult;
            }

            _logger.LogDebug("Fusing {SuccessfulCount} successful results out of {TotalCount}",
                successfulResults.Length, results.Length);

            // Update quality metrics
            consolidatedResult.QualityMetrics.SuccessfulTechniques = successfulResults.Length;
            consolidatedResult.QualityMetrics.FailedTechniques = results.Length - successfulResults.Length;

            // Fuse document type
            FuseDocumentType(consolidatedResult, successfulResults);

            // Fuse amounts
            FuseAmounts(consolidatedResult, successfulResults);

            // Fuse dates
            FuseDates(consolidatedResult, successfulResults);

            // Fuse fields
            FuseFields(consolidatedResult, successfulResults);

            // Calculate overall confidence and quality metrics
            CalculateOverallMetrics(consolidatedResult, successfulResults);

            _logger.LogDebug("Result fusion completed. Overall confidence: {Confidence:F2}, " +
                "Cross-validation score: {CrossValidation:F2}",
                consolidatedResult.OverallConfidence, consolidatedResult.QualityMetrics.CrossValidationScore);

            return consolidatedResult;
        }

        public double CalculateCrossValidationScore(ExtractionResult[] results)
        {
            var successfulResults = results.Where(r => r.IsSuccessful).ToArray();
            
            if (successfulResults.Length < 2)
                return 0.0;

            double totalScore = 0.0;
            int comparisons = 0;

            // Compare amounts across techniques
            var amountScore = CalculateAmountAgreement(successfulResults);
            totalScore += amountScore;
            comparisons++;

            // Compare document types across techniques
            var docTypeScore = CalculateDocumentTypeAgreement(successfulResults);
            totalScore += docTypeScore;
            comparisons++;

            return comparisons > 0 ? totalScore / comparisons : 0.0;
        }

        public FusionDecision[] ResolveConflicts(ExtractionResult[] results)
        {
            var decisions = new List<FusionDecision>();
            var successfulResults = results.Where(r => r.IsSuccessful).ToArray();

            if (successfulResults.Length < 2)
                return decisions.ToArray();

            // Resolve amount conflicts
            decisions.AddRange(ResolveAmountConflicts(successfulResults));

            // Resolve document type conflicts
            decisions.AddRange(ResolveDocumentTypeConflicts(successfulResults));

            return decisions.ToArray();
        }

        private void FuseDocumentType(ConsolidatedExtractionResult consolidatedResult, ExtractionResult[] results)
        {
            var docTypes = results
                .Where(r => !string.IsNullOrEmpty(r.ExtractedData.DocumentType))
                .GroupBy(r => r.ExtractedData.DocumentType)
                .Select(g => new
                {
                    Type = g.Key,
                    Count = g.Count(),
                    AvgConfidence = g.Average(r => r.ExtractedData.DocumentTypeConfidence),
                    TechniqueConfidence = g.Average(r => r.ConfidenceScore)
                })
                .OrderByDescending(x => x.Count)
                .ThenByDescending(x => x.AvgConfidence * x.TechniqueConfidence)
                .ToList();

            if (docTypes.Any())
            {
                var bestType = docTypes.First();
                consolidatedResult.ConsolidatedData.DocumentType = bestType.Type;
                consolidatedResult.ConsolidatedData.DocumentTypeConfidence = bestType.AvgConfidence;

                _logger.LogDebug("Fused document type: {Type} (confidence: {Confidence:F2}, agreement: {Count}/{Total})",
                    bestType.Type, bestType.AvgConfidence, bestType.Count, results.Length);
            }
        }

        private void FuseAmounts(ConsolidatedExtractionResult consolidatedResult, ExtractionResult[] results)
        {
            var allAmounts = results
                .SelectMany(r => r.ExtractedData.Amounts)
                .Where(a => a.Value > 0)
                .ToList();

            if (!allAmounts.Any())
            {
                _logger.LogDebug("No amounts found to fuse");
                return;
            }

            // Group amounts by type and value (with tolerance for rounding differences)
            var amountGroups = allAmounts
                .GroupBy(a => new { Type = a.Type, Value = Math.Round(a.Value, 2) })
                .Select(g => new
                {
                    Type = g.Key.Type,
                    Value = g.Key.Value,
                    Count = g.Count(),
                    AvgConfidence = g.Average(a => a.Confidence),
                    Currency = g.First().Currency,
                    BestAmount = g.OrderByDescending(a => a.Confidence).First()
                })
                .OrderByDescending(x => x.Count)
                .ThenByDescending(x => x.AvgConfidence)
                .ToList();

            foreach (var group in amountGroups)
            {
                var fusedAmount = new ExtractedAmount
                {
                    Value = group.Value,
                    Type = group.Type,
                    Currency = group.Currency,
                    Confidence = group.AvgConfidence,
                    RawText = group.BestAmount.RawText,
                    Context = group.BestAmount.Context,
                    Position = group.BestAmount.Position
                };

                consolidatedResult.ConsolidatedData.Amounts.Add(fusedAmount);

                _logger.LogDebug("Fused amount: {Type} = {Value} {Currency} (confidence: {Confidence:F2}, agreement: {Count}/{Total})",
                    group.Type, group.Value, group.Currency, group.AvgConfidence, group.Count, results.Length);
            }
        }

        private void FuseDates(ConsolidatedExtractionResult consolidatedResult, ExtractionResult[] results)
        {
            var allDates = results
                .SelectMany(r => r.ExtractedData.Dates)
                .ToList();

            if (!allDates.Any())
                return;

            // Group dates by type and value (with tolerance for same day)
            var dateGroups = allDates
                .GroupBy(d => new { Type = d.Type, Date = d.Value.Date })
                .Select(g => new
                {
                    Type = g.Key.Type,
                    Date = g.Key.Date,
                    Count = g.Count(),
                    AvgConfidence = g.Average(d => d.Confidence),
                    BestDate = g.OrderByDescending(d => d.Confidence).First()
                })
                .OrderByDescending(x => x.Count)
                .ThenByDescending(x => x.AvgConfidence)
                .ToList();

            foreach (var group in dateGroups)
            {
                var fusedDate = new ExtractedDate
                {
                    Value = group.Date,
                    Type = group.Type,
                    Confidence = group.AvgConfidence,
                    RawText = group.BestDate.RawText,
                    Position = group.BestDate.Position
                };

                consolidatedResult.ConsolidatedData.Dates.Add(fusedDate);
            }
        }

        private void FuseFields(ConsolidatedExtractionResult consolidatedResult, ExtractionResult[] results)
        {
            var allFields = results
                .SelectMany(r => r.ExtractedData.Fields)
                .ToList();

            if (!allFields.Any())
                return;

            // Group fields by name and value
            var fieldGroups = allFields
                .GroupBy(f => new { Name = f.Name, Value = f.Value })
                .Select(g => new
                {
                    Name = g.Key.Name,
                    Value = g.Key.Value,
                    Count = g.Count(),
                    AvgConfidence = g.Average(f => f.Confidence),
                    BestField = g.OrderByDescending(f => f.Confidence).First()
                })
                .OrderByDescending(x => x.Count)
                .ThenByDescending(x => x.AvgConfidence)
                .ToList();

            foreach (var group in fieldGroups)
            {
                var fusedField = new ExtractedField
                {
                    Name = group.Name,
                    Value = group.Value,
                    Confidence = group.AvgConfidence,
                    Position = group.BestField.Position
                };

                consolidatedResult.ConsolidatedData.Fields.Add(fusedField);
            }
        }

        private void CalculateOverallMetrics(ConsolidatedExtractionResult consolidatedResult, ExtractionResult[] successfulResults)
        {
            // Calculate overall confidence as weighted average
            var totalWeight = successfulResults.Sum(r => r.ConfidenceScore);
            if (totalWeight > 0)
            {
                consolidatedResult.OverallConfidence = successfulResults
                    .Sum(r => r.ConfidenceScore * r.ConfidenceScore) / totalWeight;
            }

            // Calculate cross-validation score
            consolidatedResult.QualityMetrics.CrossValidationScore = CalculateCrossValidationScore(successfulResults);

            // Calculate completeness score
            var expectedDataTypes = new[] { "Total", "VAT", "Net" };
            var foundDataTypes = consolidatedResult.ConsolidatedData.Amounts
                .Select(a => a.Type.ToString())
                .Distinct()
                .Count();
            consolidatedResult.QualityMetrics.CompletenessScore = (double)foundDataTypes / expectedDataTypes.Length;

            // Calculate consistency score based on agreement between techniques
            var agreementScores = new List<double>();
            
            if (consolidatedResult.ConsolidatedData.Amounts.Any())
            {
                var amountAgreement = CalculateAmountAgreement(successfulResults);
                agreementScores.Add(amountAgreement);
            }

            if (!string.IsNullOrEmpty(consolidatedResult.ConsolidatedData.DocumentType))
            {
                var docTypeAgreement = CalculateDocumentTypeAgreement(successfulResults);
                agreementScores.Add(docTypeAgreement);
            }

            consolidatedResult.QualityMetrics.ConsistencyScore = agreementScores.Any() ? 
                agreementScores.Average() : 0.0;
        }

        private double CalculateAmountAgreement(ExtractionResult[] results)
        {
            var totalAmounts = results
                .SelectMany(r => r.ExtractedData.Amounts)
                .Where(a => a.Type == AmountType.Total)
                .ToList();

            if (totalAmounts.Count < 2)
                return 1.0; // Perfect agreement if only one or no amounts

            var avgAmount = (double)totalAmounts.Average(a => a.Value);
            var maxDeviation = (double)totalAmounts.Max(a => Math.Abs(a.Value - (decimal)avgAmount));

            // Agreement score based on relative deviation
            return maxDeviation < avgAmount * 0.05 ? 1.0 : Math.Max(0.0, 1.0 - (maxDeviation / avgAmount));
        }

        private double CalculateDocumentTypeAgreement(ExtractionResult[] results)
        {
            var docTypes = results
                .Where(r => !string.IsNullOrEmpty(r.ExtractedData.DocumentType))
                .Select(r => r.ExtractedData.DocumentType)
                .ToList();

            if (docTypes.Count < 2)
                return 1.0;

            var mostCommon = docTypes.GroupBy(t => t)
                .OrderByDescending(g => g.Count())
                .First();

            return (double)mostCommon.Count() / docTypes.Count;
        }

        private List<FusionDecision> ResolveAmountConflicts(ExtractionResult[] results)
        {
            var decisions = new List<FusionDecision>();
            
            // Find conflicting total amounts
            var totalAmounts = results
                .SelectMany(r => r.ExtractedData.Amounts.Where(a => a.Type == AmountType.Total))
                .GroupBy(a => Math.Round(a.Value, 2))
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in totalAmounts)
            {
                var techniques = group.Select(a => results.First(r => r.ExtractedData.Amounts.Contains(a)).TechniqueName).ToList();
                var bestAmount = group.OrderByDescending(a => a.Confidence).First();
                var chosenTechnique = results.First(r => r.ExtractedData.Amounts.Contains(bestAmount)).TechniqueName;

                decisions.Add(new FusionDecision
                {
                    DataType = "TotalAmount",
                    ConflictingTechniques = techniques,
                    ChosenTechnique = chosenTechnique,
                    Reason = $"Highest confidence score ({bestAmount.Confidence:F2})",
                    DecisionConfidence = bestAmount.Confidence
                });
            }

            return decisions;
        }

        private List<FusionDecision> ResolveDocumentTypeConflicts(ExtractionResult[] results)
        {
            var decisions = new List<FusionDecision>();
            
            var docTypes = results
                .Where(r => !string.IsNullOrEmpty(r.ExtractedData.DocumentType))
                .GroupBy(r => r.ExtractedData.DocumentType)
                .ToList();

            if (docTypes.Count > 1)
            {
                var bestType = docTypes
                    .OrderByDescending(g => g.Count())
                    .ThenByDescending(g => g.Average(r => r.ExtractedData.DocumentTypeConfidence))
                    .First();

                decisions.Add(new FusionDecision
                {
                    DataType = "DocumentType",
                    ConflictingTechniques = docTypes.SelectMany(g => g.Select(r => r.TechniqueName)).ToList(),
                    ChosenTechnique = bestType.First().TechniqueName,
                    Reason = $"Most common type with highest confidence",
                    DecisionConfidence = bestType.Average(r => r.ExtractedData.DocumentTypeConfidence)
                });
            }

            return decisions;
        }
    }
}
