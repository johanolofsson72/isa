using AnalysisPlugin.Services;
using AnalysisPlugin.Models;
using AnalysisPlugin.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Text;

namespace AnalysisPlugin.Tests.Integration;

[Trait("Category", "CI")]
public class ReportArchiveIntegrationTests : IDisposable
{
    private readonly ReportArchiveService _service;
    private readonly Mock<ILogger<ReportArchiveService>> _mockLogger;
    private readonly string _testArchiveDirectory;
    private readonly AnalysisPluginOptions _options;

    public ReportArchiveIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<ReportArchiveService>>();
        _testArchiveDirectory = Path.Combine(Path.GetTempPath(), "AnalysisPlugin_Tests", Guid.NewGuid().ToString());
        
        _options = new AnalysisPluginOptions
        {
            ReportArchiveDirectory = _testArchiveDirectory,
            RetentionPolicyDays = 90
        };

        _service = new ReportArchiveService(_mockLogger.Object, _options);
    }

    [Fact]
    public async Task StoreReportAsync_WithValidReport_CreatesFileWithStructuredNaming()
    {
        // Arrange
        var reportContent = Encoding.UTF8.GetBytes("Test Excel Report Content");
        var request = CreateSampleReportRequest();
        var generationTimeMs = 1500L;

        // Act
        var metadata = await _service.StoreReportAsync(reportContent, request, generationTimeMs);

        // Assert
        Assert.NotNull(metadata);
        Assert.True(metadata.IsSuccessful);
        Assert.Equal(reportContent.Length, metadata.FileSizeBytes);
        Assert.Equal(generationTimeMs, metadata.GenerationTimeMs);

        // Verify file exists with structured naming
        Assert.True(File.Exists(metadata.FilePath));
        
        // Check filename structure: {ReportType}_{Format}_{DateRange}_{Timestamp}_{UniqueId}.{extension}
        var fileName = Path.GetFileName(metadata.FilePath);
        Assert.Contains("Financial", fileName);
        Assert.Contains("Excel", fileName);
        Assert.Contains("2024Q1", fileName); // Should detect quarterly range
        Assert.EndsWith(".xlsx", fileName);

        // Verify directory structure (year-month organization)
        var directory = Path.GetDirectoryName(metadata.FilePath);
        var yearMonth = DateTime.UtcNow.ToString("yyyy-MM");
        Assert.Contains(yearMonth, directory);

        // Verify file content
        var storedContent = await File.ReadAllBytesAsync(metadata.FilePath);
        Assert.Equal(reportContent, storedContent);
    }

    [Fact]
    public async Task StoreReportAsync_WithMultipleReports_OrganizesIntoCorrectDirectories()
    {
        // Arrange
        var reports = new[]
        {
            (Content: Encoding.UTF8.GetBytes("Q1 Report"), Request: CreateQuarterlyReportRequest(1)),
            (Content: Encoding.UTF8.GetBytes("Q2 Report"), Request: CreateQuarterlyReportRequest(2)),
            (Content: Encoding.UTF8.GetBytes("Annual Report"), Request: CreateAnnualReportRequest())
        };

        // Act
        var metadataList = new List<ReportMetadata>();
        foreach (var (content, request) in reports)
        {
            var metadata = await _service.StoreReportAsync(content, request, 1000L);
            metadataList.Add(metadata);
        }

        // Assert
        Assert.All(metadataList, metadata => Assert.True(metadata.IsSuccessful));
        
        // Verify all files exist
        Assert.All(metadataList, metadata => Assert.True(File.Exists(metadata.FilePath)));

        // Verify structured naming
        var q1FileName = Path.GetFileName(metadataList[0].FilePath);
        var q2FileName = Path.GetFileName(metadataList[1].FilePath);
        var annualFileName = Path.GetFileName(metadataList[2].FilePath);

        Assert.Contains("2024Q1", q1FileName);
        Assert.Contains("2024Q2", q2FileName);
        Assert.Contains("2024", annualFileName);
        Assert.DoesNotContain("Q", annualFileName); // Annual report shouldn't have quarter
    }

    [Fact]
    public async Task RetrieveReportAsync_WithValidReportId_ReturnsCorrectContent()
    {
        // Arrange
        var originalContent = Encoding.UTF8.GetBytes("Original Report Content for Retrieval Test");
        var request = CreateSampleReportRequest();
        var storedMetadata = await _service.StoreReportAsync(originalContent, request, 800L);

        // Act
        var retrievedData = await _service.RetrieveReportAsync(storedMetadata.ReportId);

        // Assert
        Assert.NotNull(retrievedData);
        var (content, metadata) = retrievedData.Value;
        
        Assert.Equal(originalContent, content);
        Assert.Equal(storedMetadata.ReportId, metadata.ReportId);
        Assert.Equal(storedMetadata.FilePath, metadata.FilePath);
        Assert.Equal(storedMetadata.FileSizeBytes, metadata.FileSizeBytes);
    }

    [Fact]
    public async Task ListReportsAsync_WithMultipleReports_ReturnsFilteredResults()
    {
        // Arrange
        var reports = new[]
        {
            (CreateSampleReportRequest(), "Excel Report 1"),
            (CreatePdfReportRequest(), "PDF Report 1"),
            (CreateSampleReportRequest(), "Excel Report 2")
        };

        var storedMetadata = new List<ReportMetadata>();
        foreach (var (request, content) in reports)
        {
            var contentBytes = Encoding.UTF8.GetBytes(content);
            var metadata = await _service.StoreReportAsync(contentBytes, request, 500L);
            storedMetadata.Add(metadata);
        }

        // Act - List all reports
        var allReports = await _service.ListReportsAsync();
        
        // Act - List Excel reports only
        var excelReports = await _service.ListReportsAsync(new ReportSearchCriteria
        {
            ReportType = ReportType.Financial,
            Format = ReportFormat.Excel
        });

        // Assert
        Assert.Equal(3, allReports.Count());
        Assert.Equal(2, excelReports.Count());
        Assert.All(excelReports, report => Assert.Equal(ReportFormat.Excel, report.Format));
    }

    [Fact]
    public async Task GetArchiveStatisticsAsync_WithMultipleReports_ReturnsAccurateStatistics()
    {
        // Arrange
        var reports = new[]
        {
            (CreateSampleReportRequest(), 1000),
            (CreatePdfReportRequest(), 2000),
            (CreateSampleReportRequest(), 1500)
        };

        foreach (var (request, size) in reports)
        {
            var content = new byte[size];
            await _service.StoreReportAsync(content, request, 1000L);
        }

        // Act
        var statistics = await _service.GetArchiveStatisticsAsync();

        // Assert
        Assert.Equal(3, statistics.TotalReports);
        Assert.Equal(4500, statistics.TotalSizeBytes); // 1000 + 2000 + 1500
        Assert.Equal(2, statistics.ReportsByFormat[ReportFormat.Excel]);
        Assert.Equal(1, statistics.ReportsByFormat[ReportFormat.Pdf]);
        Assert.Equal(3, statistics.ReportsByType[ReportType.Financial]);
    }

    [Fact]
    public async Task StoreReportAsync_WithLargeReport_HandlesCorrectly()
    {
        // Arrange
        var largeContent = new byte[5 * 1024 * 1024]; // 5MB
        new Random(42).NextBytes(largeContent); // Fill with random data
        var request = CreateSampleReportRequest();

        // Act
        var metadata = await _service.StoreReportAsync(largeContent, request, 5000L);

        // Assert
        Assert.NotNull(metadata);
        Assert.True(metadata.IsSuccessful);
        Assert.Equal(largeContent.Length, metadata.FileSizeBytes);
        Assert.True(File.Exists(metadata.FilePath));

        // Verify file size on disk
        var fileInfo = new FileInfo(metadata.FilePath);
        Assert.Equal(largeContent.Length, fileInfo.Length);
    }

    [Fact]
    public async Task DeleteReportAsync_WithValidReportId_RemovesFileAndMetadata()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("Report to be deleted");
        var request = CreateSampleReportRequest();
        var metadata = await _service.StoreReportAsync(content, request, 1000L);
        
        // Verify file exists
        Assert.True(File.Exists(metadata.FilePath));

        // Act
        var deleteResult = await _service.DeleteReportAsync(metadata.ReportId);

        // Assert
        Assert.True(deleteResult);
        Assert.False(File.Exists(metadata.FilePath));
        
        // Verify report is no longer in list
        var reports = await _service.ListReportsAsync();
        Assert.DoesNotContain(reports, r => r.ReportId == metadata.ReportId);
    }

    private ReportRequest CreateSampleReportRequest()
    {
        return new ReportRequest
        {
            RequestId = Guid.NewGuid(),
            RequestType = ReportType.Financial,
            Format = ReportFormat.Excel,
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 3, 31),
            Language = "en",
            RequestedBy = "test-user"
        };
    }

    private ReportRequest CreateQuarterlyReportRequest(int quarter)
    {
        var (startMonth, endMonth) = quarter switch
        {
            1 => (1, 3),
            2 => (4, 6),
            3 => (7, 9),
            4 => (10, 12),
            _ => throw new ArgumentException("Invalid quarter")
        };

        return new ReportRequest
        {
            RequestId = Guid.NewGuid(),
            RequestType = ReportType.Financial,
            Format = ReportFormat.Excel,
            StartDate = new DateTime(2024, startMonth, 1),
            EndDate = new DateTime(2024, endMonth, DateTime.DaysInMonth(2024, endMonth)),
            Language = "en",
            RequestedBy = "test-user"
        };
    }

    private ReportRequest CreateAnnualReportRequest()
    {
        return new ReportRequest
        {
            RequestId = Guid.NewGuid(),
            RequestType = ReportType.Financial,
            Format = ReportFormat.Pdf,
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Language = "en",
            RequestedBy = "test-user"
        };
    }

    private ReportRequest CreatePdfReportRequest()
    {
        return new ReportRequest
        {
            RequestId = Guid.NewGuid(),
            RequestType = ReportType.Financial,
            Format = ReportFormat.Pdf,
            StartDate = new DateTime(2024, 2, 1),
            EndDate = new DateTime(2024, 2, 29),
            Language = "en",
            RequestedBy = "test-user"
        };
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testArchiveDirectory))
        {
            try
            {
                Directory.Delete(_testArchiveDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                // Log but don't fail test cleanup
                Console.WriteLine($"Failed to clean up test directory: {ex.Message}");
            }
        }
    }
}
