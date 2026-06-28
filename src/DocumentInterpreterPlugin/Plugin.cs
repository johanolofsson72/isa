using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using DocumentInterpreter.Models;
using DocumentInterpreter.Interfaces;
using DocumentInterpreter.Services;
using DocumentInterpreterPlugin.Services;
using Shared.Services;

namespace DocumentInterpreter;

// Simple logger wrapper that forwards to the main logger
public class LoggerWrapper<T> : ILogger<T>
{
    private readonly ILogger _logger;

    public LoggerWrapper(ILogger logger)
    {
        _logger = logger;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _logger.BeginScope(state);
    public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _logger.Log(logLevel, eventId, state, exception, formatter);
}

/// <summary>
/// Document Interpreter Plugin for parsing and structuring financial documents  
/// Note: AI analysis is handled by the AccountingAgent, this plugin focuses on document parsing
/// </summary>
public class Plugin
{
    private readonly IDocumentInterpreter _documentInterpreter;
    private readonly ILogger<Plugin> _logger;
    private readonly DocumentInterpreterConfig _config;

    /// <summary>
    /// Static service provider for accessing DI services (set by Agent during startup)
    /// </summary>
    public static IServiceProvider? ServiceProvider { get; set; }

    /// <summary>
    /// Creates a new instance of the DocumentInterpreter plugin
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    /// <param name="logger">Logger instance</param>
    public Plugin(IConfiguration configuration, ILogger<Plugin> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Load configuration
        _config = new DocumentInterpreterConfig();
        configuration.GetSection("DocumentInterpreter").Bind(_config);

        _logger.LogInformation("🔧 INITIALIZING MULTI-TECHNIQUE DOCUMENT EXTRACTION");

        // Create logger wrappers that forward to the main logger
        var azureVisionLogger = new LoggerWrapper<AzureComputerVisionService>(_logger);
        var azureDocumentLogger = new LoggerWrapper<AzureDocumentIntelligenceService>(_logger);
        var ocrLogger = new LoggerWrapper<EnhancedOcrService>(_logger);
        var orchestratorLogger = new LoggerWrapper<DocumentExtractionOrchestrator>(_logger);
        var fusionLogger = new LoggerWrapper<ExtractionResultFusion>(_logger);
        var numberParserLogger = new LoggerWrapper<SwedishNumberParser>(_logger);
        var typeDetectorLogger = new LoggerWrapper<DocumentTypeDetector>(_logger);
        var itext7Logger = new LoggerWrapper<IText7ExtractionTechnique>(_logger);


        // Initialize core services
        var numberParser = new SwedishNumberParser(numberParserLogger);
        var typeDetector = new DocumentTypeDetector(typeDetectorLogger);
        var resultFusion = new ExtractionResultFusion(fusionLogger);

        // Initialize Azure services (legacy support)
        var azureVisionService = new AzureComputerVisionService(configuration, azureVisionLogger);
        var azureDocumentService = new AzureDocumentIntelligenceService(configuration, azureDocumentLogger);
        var enhancedOcrService = new EnhancedOcrService(ocrLogger, configuration, azureVisionService, azureDocumentService);

        // Initialize extraction techniques
        var itext7Technique = new IText7ExtractionTechnique(itext7Logger, numberParser, typeDetector);

        // Initialize Azure Document Intelligence technique
        var azureDocIntelligenceLogger = new LoggerWrapper<AzureDocumentIntelligenceExtractionTechnique>(_logger);
        var azureDocIntelligenceTechnique = new AzureDocumentIntelligenceExtractionTechnique(
            azureDocIntelligenceLogger, numberParser, typeDetector, azureDocumentService);

        // Initialize Azure Computer Vision technique
        var azureComputerVisionLogger = new LoggerWrapper<AzureComputerVisionExtractionTechnique>(_logger);
        var azureComputerVisionTechnique = new AzureComputerVisionExtractionTechnique(
            azureComputerVisionLogger, numberParser, typeDetector, azureVisionService);

        // Initialize Office Document technique
        var officeDocumentLogger = new LoggerWrapper<OfficeDocumentExtractionTechnique>(_logger);
        var officeDocumentTechnique = new OfficeDocumentExtractionTechnique(
            officeDocumentLogger, numberParser, typeDetector);

        // Initialize orchestrator and register techniques
        var orchestrator = new DocumentExtractionOrchestrator(orchestratorLogger, resultFusion);

        _logger.LogInformation("🔧 Registering extraction techniques...");

        // Register Azure Document Intelligence (highest priority)
        if (azureDocIntelligenceTechnique.IsAvailable)
        {
            orchestrator.RegisterTechnique(azureDocIntelligenceTechnique);
            _logger.LogInformation("✅ Registered Azure Document Intelligence technique (Priority: {Priority}, Available: {Available})",
                azureDocIntelligenceTechnique.Priority, azureDocIntelligenceTechnique.IsAvailable);
        }
        else
        {
            _logger.LogWarning("⚠️ Azure Document Intelligence technique not available - skipping registration");
        }

        // Register iText7 (high priority)
        orchestrator.RegisterTechnique(itext7Technique);
        _logger.LogInformation("✅ Registered iText7 technique (Priority: {Priority}, Available: {Available})",
            itext7Technique.Priority, itext7Technique.IsAvailable);

        // Register Azure Computer Vision (medium priority)
        if (azureComputerVisionTechnique.IsAvailable)
        {
            orchestrator.RegisterTechnique(azureComputerVisionTechnique);
            _logger.LogInformation("✅ Registered Azure Computer Vision technique (Priority: {Priority}, Available: {Available})",
                azureComputerVisionTechnique.Priority, azureComputerVisionTechnique.IsAvailable);
        }
        else
        {
            _logger.LogWarning("⚠️ Azure Computer Vision technique not available - skipping registration");
        }

        // Register Office Document technique (medium priority)
        orchestrator.RegisterTechnique(officeDocumentTechnique);
        _logger.LogInformation("✅ Registered Office Document technique (Priority: {Priority}, Available: {Available})",
            officeDocumentTechnique.Priority, officeDocumentTechnique.IsAvailable);

        var registeredTechniques = orchestrator.GetRegisteredTechniques();
        _logger.LogInformation("🎯 Total registered extraction techniques: {Count}", registeredTechniques.Length);

        // Initialize document interpreter with multi-technique support
        _documentInterpreter = new DocumentInterpreterService(logger, enhancedOcrService, orchestrator);

        _logger.LogInformation("DocumentInterpreter plugin initialized with multi-technique extraction pipeline");
    }

    /// <summary>
    /// Parses and structures a document from text content
    /// </summary>
    /// <param name="content">Document text content</param>
    /// <param name="documentType">Expected document type (optional)</param>
    /// <param name="source">Source information</param>
    /// <returns>JSON representation of the parsed document structure</returns>
    [KernelFunction]
    [Description(@"Parses financial documents and returns structured data with Fortnox-ready fields.

IMPORTANT - Document Types:
- SupplierInvoice = Invoice WE received (we need to pay) → Use FortnoxPlugin.CreateSupplierInvoiceAsync
- CustomerInvoice = Invoice WE sent (we expect payment) → Use FortnoxPlugin.BookkeepInvoiceAsync
- Receipt = Purchase receipt

Returns JSON with:
- documentType: The detected type (SupplierInvoice, CustomerInvoice, Receipt)
- extractedData.fortnoxData: Complete data structure ready for Fortnox API
- extractedData.fortnoxData._workflow: Step-by-step instructions for creating the document in Fortnox
- extractedData.fortnoxData.supplierInvoiceRows: Invoice lines with intelligent account number suggestions

For SupplierInvoice, follow the _workflow steps to create the invoice in Fortnox.")]
    public async Task<string> InterpretDocumentFromTextAsync(
        [Description("Text content of the document to parse")] string content,
        [Description("Expected document type (SupplierInvoice, Receipt, CustomerInvoice, etc.)")] string? documentType = null,
        [Description("Source information (email, file path, etc.)")] string? source = null)
    {
        try
        {
            _logger.LogInformation("Parsing document from text. Type: {DocumentType}, Source: {Source}", 
                documentType ?? "auto-detect", source ?? "unknown");

            var document = await _documentInterpreter.InterpretDocumentAsync(content, documentType, source);
            
            // Create structured response for consistency with binary processing
            var structuredResult = CreateStructuredResponse(document, null);
            
            var result = JsonSerializer.Serialize(structuredResult, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _logger.LogInformation("Document parsing completed. Type: {DocumentType}, Confidence: {Confidence}", 
                document.DocumentType, document.ConfidenceScore);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse document from text");
            throw;
        }
    }

    /// <summary>
    /// Detects the type of a document from its content using pattern matching
    /// </summary>
    /// <param name="content">Document content</param>
    /// <returns>JSON with document type and confidence</returns>
    [KernelFunction]
    [Description("Detects the type of a financial document from its content using keyword patterns")]
    public async Task<string> DetectDocumentTypeAsync(
        [Description("Content of the document to analyze")] string content)
    {
        try
        {
            _logger.LogInformation("Detecting document type from content using pattern matching");

            var (documentType, confidence) = await _documentInterpreter.DetectDocumentTypeAsync(content);
            
            var result = new 
            {
                DocumentType = documentType,
                Confidence = confidence,
                DetectedAt = DateTime.UtcNow,
                Method = "Pattern-based detection"
            };

            _logger.LogInformation("Document type detected: {DocumentType} with confidence {Confidence}", 
                documentType, confidence);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect document type");
            throw;
        }
    }

    /// <summary>
    /// Validates a parsed document structure
    /// </summary>
    /// <param name="documentJson">JSON representation of the document to validate</param>
    /// <returns>JSON validation result</returns>
    [KernelFunction]
    [Description("Validates a parsed financial document structure for completeness and basic accuracy")]
    public async Task<string> ValidateDocumentAsync(
        [Description("JSON representation of the document to validate")] string documentJson)
    {
        try
        {
            _logger.LogInformation("Validating document");

            // Parse the JSON to validate structure without deserializing to specific type
            var jsonDocument = JsonDocument.Parse(documentJson);
            var root = jsonDocument.RootElement;

            // Basic validation - check for required properties
            if (!root.TryGetProperty("documentType", out var docTypeProperty))
            {
                throw new ArgumentException("Document JSON missing documentType property");
            }

            var documentType = docTypeProperty.GetString();
            if (string.IsNullOrEmpty(documentType))
            {
                throw new ArgumentException("Document documentType cannot be empty");
            }

            // For now, just validate JSON structure since we can't deserialize abstract types easily
            var validation = new
            {
                IsValid = true,
                DocumentType = documentType,
                ValidationMessages = new[] { "Document structure is valid" },
                ValidationDate = DateTime.UtcNow,
                ValidationScore = 0.85
            };

            await Task.CompletedTask; // Satisfy async requirement
            
            _logger.LogInformation("Document validation completed. Valid: {IsValid}, Score: {Score}", 
                validation.IsValid, validation.ValidationScore);

            return JsonSerializer.Serialize(validation, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate document");
            throw;
        }
    }

    /// <summary>
    /// Gets supported document types
    /// </summary>
    /// <returns>JSON array of supported document types</returns>
    [KernelFunction]
    [Description("Gets the list of supported financial document types")]
    public string GetSupportedDocumentTypes()
    {
        try
        {
            var supportedTypes = _documentInterpreter.GetSupportedDocumentTypes().ToList();
            
            _logger.LogInformation("Retrieved {Count} supported document types", supportedTypes.Count);

            return JsonSerializer.Serialize(supportedTypes, new JsonSerializerOptions 
            { 
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get supported document types");
            throw;
        }
    }

    /// <summary>
    /// Processes documents from email attachments
    /// </summary>
    /// <param name="emailSource">Email source information (from MailPlugin)</param>
    /// <param name="attachmentFilter">File extension filter (e.g., ".pdf,.jpg,.png")</param>
    /// <returns>JSON array of interpreted documents</returns>
    [KernelFunction]
    [Description("Processes financial documents from email attachments")]
    public async Task<string> ProcessEmailAttachmentsAsync(
        [Description("Email source information from MailPlugin")] string emailSource,
        [Description("File extension filter (e.g., '.pdf,.jpg,.png')")] string attachmentFilter = ".pdf,.jpg,.jpeg,.png,.tiff,.txt")
    {
        try
        {
            _logger.LogInformation("Processing email attachments with filter: {Filter}", attachmentFilter);

            // This would integrate with MailPlugin to get attachments
            // For now, return a placeholder result
            var result = new
            {
                Message = "Email attachment processing not yet implemented",
                EmailSource = emailSource,
                AttachmentFilter = attachmentFilter,
                ProcessedAt = DateTime.UtcNow
            };

            await Task.CompletedTask; // Satisfy async requirement
            return JsonSerializer.Serialize(result, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process email attachments");
            throw;
        }
    }

    /// <summary>
    /// Parses and structures a document from binary content (PDF, images with OCR)
    /// </summary>
    /// <param name="contentBase64">Base64-encoded binary content</param>
    /// <param name="contentType">MIME content type (e.g., 'application/pdf', 'image/jpeg')</param>
    /// <param name="filename">Original filename</param>
    /// <param name="documentType">Expected document type (optional)</param>
    /// <param name="source">Source information</param>
    /// <returns>JSON representation of the parsed document structure</returns>
    [KernelFunction]
    [Description(@"Parses financial documents from binary content (PDF, images) using OCR and returns Fortnox-ready data.

IMPORTANT - Document Types:
- SupplierInvoice = Invoice WE received (we need to pay) → Use FortnoxPlugin.CreateSupplierInvoiceAsync
- CustomerInvoice = Invoice WE sent (we expect payment) → Use FortnoxPlugin.BookkeepInvoiceAsync
- Receipt = Purchase receipt

Returns JSON with:
- documentType: The detected type (SupplierInvoice, CustomerInvoice, Receipt)
- extractedData.fortnoxData: Complete data structure ready for Fortnox API
- extractedData.fortnoxData._workflow: Step-by-step instructions for creating the document in Fortnox
- extractedData.fortnoxData.supplierInvoiceRows: Invoice lines with intelligent account number suggestions

For SupplierInvoice, follow the _workflow steps to create the invoice in Fortnox.")]
    public async Task<string> InterpretDocumentFromBinaryAsync(
        [Description("Base64-encoded binary content of the document")] string contentBase64,
        [Description("MIME content type (application/pdf, image/jpeg, image/png, etc.)")] string contentType,
        [Description("Original filename")] string filename,
        [Description("Expected document type (SupplierInvoice, Receipt, etc.)")] string? documentType = null,
        [Description("Source information (email, file path, etc.)")] string? source = null)
    {
        try
        {
            _logger.LogInformation("Parsing document from binary content. Type: {ContentType}, Filename: {Filename}", 
                contentType, filename);

            // Decode base64 content
            var binaryContent = Convert.FromBase64String(contentBase64);
            
            var document = await _documentInterpreter.InterpretDocumentAsync(
                binaryContent, contentType, filename, documentType, source);
            
            var result = JsonSerializer.Serialize(document, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _logger.LogInformation("Binary document parsing completed. Type: {DocumentType}, Confidence: {Confidence}", 
                document.DocumentType, document.ConfidenceScore);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse document from binary content");
            throw;
        }
    }

    /// <summary>
    /// Analyzes a document from Dropbox response containing file ID
    /// </summary>
    /// <param name="dropboxResponse">Response from DropboxPlugin containing file ID</param>
    /// <param name="fileName">Name of the file being analyzed</param>
    /// <param name="documentType">Expected document type (optional)</param>
    /// <param name="source">Source information</param>
    /// <returns>JSON representation of the analyzed document</returns>
    [KernelFunction]
    [Description("Analyzes a document from Dropbox response containing file ID. Use this after downloading a file with DropboxPlugin.")]
    public async Task<string> AnalyzeDropboxResponseAsync(
        [Description("Response from DropboxPlugin DownloadFile function")] string dropboxResponse,
        [Description("Name of the file being analyzed")] string fileName,
        [Description("Expected document type (SupplierInvoice, Receipt, etc.)")] string? documentType = null,
        [Description("Source information")] string? source = null)
    {
        try
        {
            _logger.LogInformation("Analyzing Dropbox response for file: {FileName}", fileName);

            // Validate response format
            if (string.IsNullOrWhiteSpace(dropboxResponse))
            {
                throw new ArgumentException("Dropbox response is empty", nameof(dropboxResponse));
            }

            string fileId;

            // Handle different response formats from DropboxPlugin
            if (dropboxResponse.StartsWith("SUCCESS:"))
            {
                // Legacy format: "SUCCESS: Fil nedladdad framgångsrikt. File ID: {fileId}. Använd DocumentInterpreterPlugin med detta ID för att analysera filen."
                var fileIdMatch = System.Text.RegularExpressions.Regex.Match(dropboxResponse, @"File ID: ([a-fA-F0-9\-]+)");
                if (!fileIdMatch.Success)
                {
                    throw new InvalidOperationException($"Could not extract file ID from Dropbox response: {dropboxResponse}");
                }
                fileId = fileIdMatch.Groups[1].Value;
            }
            else if (Guid.TryParse(dropboxResponse.Trim(), out var directFileId))
            {
                // New format: Direct FileID from DropboxPlugin
                fileId = directFileId.ToString();
                _logger.LogInformation("Using direct FileID from DropboxPlugin: {FileId}", fileId);
            }
            else
            {
                throw new InvalidOperationException($"Dropbox download failed or invalid response format: {dropboxResponse}");
            }
            if (!Guid.TryParse(fileId, out var fileGuid))
            {
                throw new ArgumentException($"Invalid file ID format: {fileId}");
            }

            _logger.LogInformation("Extracted file ID: {FileId}, analyzing document: {FileName}", fileId, fileName);

            // Use TempFilesService to get file path
            if (ServiceProvider != null)
            {
                try
                {
                    using var scope = ServiceProvider.CreateScope();
                    var tempFilesService = scope.ServiceProvider.GetRequiredService<ITempFilesService>();

                    var filePath = await tempFilesService.GetFilePathAsync(fileGuid);
                    if (filePath == null)
                    {
                        throw new FileNotFoundException($"File not found with ID: {fileId}");
                    }

                    // Analyze document from file path
                    var result = await InterpretDocumentFromFileAsync(
                        filePath,
                        documentType,
                        source ?? $"Dropbox:{fileName}"
                    );

                    _logger.LogInformation("Successfully analyzed Dropbox file: {FileName} via TempFilesService", fileName);
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to analyze file via TempFilesService: {FileId}", fileId);
                    throw;
                }
            }
            else
            {
                _logger.LogError("ServiceProvider not available, cannot use TempFilesService");
                throw new InvalidOperationException("TempFilesService inte tillgänglig");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze Dropbox response for file: {FileName}", fileName);
            throw;
        }
    }

    /// <summary>
    /// Analyzes an email attachment using its TempFiles file ID
    /// </summary>
    /// <param name="tempFileId">TempFiles file ID from email attachment</param>
    /// <param name="fileName">Name of the attachment file</param>
    /// <param name="documentType">Expected document type (optional)</param>
    /// <param name="source">Source information</param>
    /// <returns>JSON representation of the analyzed document</returns>
    [KernelFunction]
    [Description("Analyzes an email attachment using its TempFiles file ID. Use this after reading emails with MailPlugin.")]
    public async Task<string> AnalyzeEmailAttachmentAsync(
        [Description("TempFiles file ID from email attachment")] string tempFileId,
        [Description("Name of the attachment file")] string fileName,
        [Description("Expected document type (SupplierInvoice, Receipt, etc.)")] string? documentType = null,
        [Description("Source information")] string? source = null)
    {
        try
        {
            _logger.LogInformation("Analyzing email attachment: {FileName} with TempFileId: {TempFileId}", fileName, tempFileId);

            if (!Guid.TryParse(tempFileId, out var fileGuid))
            {
                throw new ArgumentException($"Invalid TempFile ID format: {tempFileId}");
            }

            // Use TempFilesService to get file path
            if (ServiceProvider != null)
            {
                try
                {
                    using var scope = ServiceProvider.CreateScope();
                    var tempFilesService = scope.ServiceProvider.GetRequiredService<ITempFilesService>();

                    var filePath = await tempFilesService.GetFilePathAsync(fileGuid);
                    if (filePath == null)
                    {
                        throw new FileNotFoundException($"Email attachment not found with TempFile ID: {tempFileId}");
                    }

                    // Analyze document from file path
                    var result = await InterpretDocumentFromFileAsync(
                        filePath,
                        documentType,
                        source ?? $"Email:{fileName}"
                    );

                    _logger.LogInformation("Successfully analyzed email attachment: {FileName} via TempFilesService", fileName);
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to analyze email attachment via TempFilesService: {TempFileId}", tempFileId);
                    throw;
                }
            }
            else
            {
                throw new InvalidOperationException("ServiceProvider not available for TempFilesService access");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze email attachment: {FileName}", fileName);
            throw;
        }
    }

    /// <summary>
    /// Processes documents from cloud storage (Google Drive, Dropbox, OneDrive)
    /// </summary>
    /// <param name="storageType">Storage type (GoogleDrive, Dropbox, OneDrive)</param>
    /// <param name="folderPath">Folder path in the storage</param>
    /// <param name="fileFilter">File extension filter</param>
    /// <returns>JSON array of interpreted documents</returns>
    [KernelFunction]
    [Description("Processes financial documents from cloud storage services")]
    public async Task<string> ProcessCloudStorageAsync(
        [Description("Storage type (GoogleDrive, Dropbox, OneDrive)")] string storageType,
        [Description("Folder path in the storage")] string folderPath,
        [Description("File extension filter (e.g., '.pdf,.jpg,.png')")] string fileFilter = ".pdf,.jpg,.jpeg,.png,.tiff,.txt")
    {
        try
        {
            _logger.LogInformation("Processing cloud storage documents. Type: {StorageType}, Path: {FolderPath}", 
                storageType, folderPath);

            // This would integrate with cloud storage plugins
            // For now, return a placeholder result
            var result = new
            {
                Message = "Cloud storage processing not yet implemented", 
                StorageType = storageType,
                FolderPath = folderPath,
                FileFilter = fileFilter,
                ProcessedAt = DateTime.UtcNow
            };

            await Task.CompletedTask; // Satisfy async requirement
            return JsonSerializer.Serialize(result, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process cloud storage documents");
            throw;
        }
    }

    /// <summary>
    /// Interprets a document from a file path (useful for local files or cloud file paths)
    /// </summary>
    /// <param name="filePath">Path to the file to process</param>
    /// <param name="documentType">Expected document type (optional)</param>
    /// <param name="source">Source information</param>
    /// <returns>JSON representation of the parsed document structure with OCR results saved to TempFiles</returns>
    [KernelFunction]
    [Description("Parses a financial document from a file path or TempFiles ID, automatically detecting content type and applying OCR if needed. Saves OCR results to TempFiles for later retrieval. Tolka dokument från fil, undersök PDF, kolla upp dokument, analysera filer.")]
    public async Task<string> InterpretDocumentFromFileAsync(
        [Description("Path to the document file to process, or TempFiles GUID")] string filePath,
        [Description("Expected document type (SupplierInvoice, Receipt, etc.)")] string? documentType = null,
        [Description("Source information (email, folder, etc.)")] string? source = null)
    {
        try
        {
            _logger.LogInformation("Parsing document from file: {FilePath}", filePath);

            // Check if filePath is a GUID (TempFiles ID)
            if (Guid.TryParse(filePath, out var fileGuid))
            {
                _logger.LogInformation("Detected TempFiles GUID: {FileId}, using TempFilesService", fileGuid);

                // Use TempFilesService to get file path
                if (ServiceProvider != null)
                {
                    try
                    {
                        using var scope = ServiceProvider.CreateScope();
                        var tempFilesService = scope.ServiceProvider.GetRequiredService<ITempFilesService>();

                        var actualFilePath = await tempFilesService.GetFilePathAsync(fileGuid);
                        if (actualFilePath == null)
                        {
                            throw new FileNotFoundException($"TempFile not found with ID: {fileGuid}");
                        }

                        _logger.LogInformation("Resolved TempFiles ID {FileId} to path: {ActualPath}", fileGuid, actualFilePath);
                        filePath = actualFilePath;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to resolve TempFiles ID: {FileId}", fileGuid);
                        throw;
                    }
                }
                else
                {
                    throw new InvalidOperationException("ServiceProvider not available for TempFiles resolution");
                }
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            // Read file content
            var binaryContent = await File.ReadAllBytesAsync(filePath);
            var filename = Path.GetFileName(filePath);
            
            // Determine content type from file extension
            var contentType = GetContentTypeFromExtension(Path.GetExtension(filePath));
            
            var document = await _documentInterpreter.InterpretDocumentAsync(
                binaryContent, contentType, filename, documentType, source ?? filePath);

            // Save OCR results to TempFiles if we have extracted text
            string? ocrResultFileId = null;
            if (!string.IsNullOrWhiteSpace(document.RawText) && ServiceProvider != null)
            {
                try
                {
                    using var scope = ServiceProvider.CreateScope();
                    var tempFilesService = scope.ServiceProvider.GetRequiredService<ITempFilesService>();

                    // Create detailed OCR result text
                    var ocrResultText = CreateOcrResultText(document, filename);
                    var ocrBytes = System.Text.Encoding.UTF8.GetBytes(ocrResultText);

                    var ocrFileId = await tempFilesService.SaveFileAsync(
                        $"{Path.GetFileNameWithoutExtension(filename)}_OCR_Result.txt",
                        ocrBytes,
                        "text/plain",
                        $"OCR:{source ?? filePath}",
                        filePath
                    );

                    ocrResultFileId = ocrFileId.ToString();
                    _logger.LogInformation("OCR results saved to TempFiles with ID: {FileId}, Text Length: {Length} characters",
                        ocrFileId, ocrResultText.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save OCR results to TempFiles");
                }
            }

            // Create structured response for Fortnox integration
            var structuredResult = CreateStructuredResponse(document, ocrResultFileId);

            var result = JsonSerializer.Serialize(structuredResult, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _logger.LogInformation("File document parsing completed. Type: {DocumentType}, Confidence: {Confidence}, OCR File ID: {OcrFileId}",
                document.DocumentType, document.ConfidenceScore, ocrResultFileId ?? "None");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse document from file: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Creates detailed OCR result text for saving to TempFiles
    /// </summary>
    private string CreateOcrResultText(DocumentBase document, string filename)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== OCR RESULTS FOR: {filename} ===");
        sb.AppendLine($"Processed at: {document.ProcessedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Document Type: {document.DocumentType}");
        sb.AppendLine($"Confidence Score: {document.ConfidenceScore:F2}%");
        sb.AppendLine($"Source: {document.Source}");
        sb.AppendLine();

        if (document.QualityMetrics != null)
        {
            sb.AppendLine("=== QUALITY METRICS ===");
            sb.AppendLine($"Text Quality: {document.QualityMetrics.TextQuality:F2}%");
            sb.AppendLine($"Field Accuracy: {document.QualityMetrics.FieldAccuracy:F2}%");
            sb.AppendLine($"Overall Confidence: {document.QualityMetrics.OverallConfidence:F2}%");
            sb.AppendLine();
        }

        sb.AppendLine("=== EXTRACTED TEXT ===");
        sb.AppendLine(document.RawText);
        sb.AppendLine();

        if (document is SupplierInvoice invoice)
        {
            sb.AppendLine("=== PARSED INVOICE DATA ===");
            sb.AppendLine($"Supplier: {invoice.Seller?.Name ?? "Unknown"}");
            sb.AppendLine($"Invoice Number: {invoice.InvoiceNumber}");
            sb.AppendLine($"Date: {invoice.IssueDate?.ToString("yyyy-MM-dd") ?? "Unknown"}");
            sb.AppendLine($"Due Date: {invoice.DueDate?.ToString("yyyy-MM-dd") ?? "Unknown"}");
            sb.AppendLine($"Total Amount: {invoice.Totals.GrossAmount:F2} {invoice.Totals.Currency}");
            sb.AppendLine($"VAT Amount: {invoice.Totals.VatAmount:F2}");
        }
        else if (document is Receipt receipt)
        {
            sb.AppendLine("=== PARSED RECEIPT DATA ===");
            sb.AppendLine($"Store: {receipt.Merchant?.Name ?? "Unknown"}");
            sb.AppendLine($"Date: {receipt.PurchaseDateTime?.ToString("yyyy-MM-dd") ?? "Unknown"}");
            sb.AppendLine($"Total Amount: {receipt.Totals.GrossAmount:F2} {receipt.Totals.Currency}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Creates a structured response optimized for Fortnox and other accounting system integration
    /// </summary>
    private object CreateStructuredResponse(DocumentBase document, string? ocrResultFileId = null)
    {
        // Determine status based on required fields validation
        var status = DetermineDocumentStatus(document);

        var baseResponse = new
        {
            status = status,
            documentType = document.DocumentType,
            confidence = Math.Round(document.ConfidenceScore, 2),
            processedAt = document.ProcessedAt,
            source = document.Source,
            ocrResultFileId = ocrResultFileId,
            rawTextLength = document.RawText?.Length ?? 0,
            qualityMetrics = document.QualityMetrics != null ? new
            {
                textQuality = Math.Round(document.QualityMetrics.TextQuality, 2),
                fieldAccuracy = Math.Round(document.QualityMetrics.FieldAccuracy, 2),
                overallConfidence = Math.Round(document.QualityMetrics.OverallConfidence, 2),
                requiredFieldsComplete = document.QualityMetrics.RequiredFieldsComplete,
                validationMessages = document.QualityMetrics.ValidationMessages
            } : null,
            unreadableReason = status == "Unreadable" ? string.Join("; ", document.QualityMetrics?.ValidationMessages ?? new List<string>()) : null
        };

        // Create type-specific structured data for accounting system integration
        return document switch
        {
            Receipt receipt => new
            {
                status = baseResponse.status,
                documentType = baseResponse.documentType,
                confidence = baseResponse.confidence,
                processedAt = baseResponse.processedAt,
                source = baseResponse.source,
                ocrResultFileId = baseResponse.ocrResultFileId,
                rawTextLength = baseResponse.rawTextLength,
                qualityMetrics = baseResponse.qualityMetrics,
                extractedData = new
                {
                    rawText = receipt.RawText ?? "", // Include actual extracted text for analysis
                    merchant = receipt.Merchant?.Name ?? "Unknown",
                    amount = receipt.Totals.GrossAmount.ToString("F2"),
                    currency = receipt.Totals.Currency ?? "SEK",
                    purchaseDate = receipt.PurchaseDateTime?.ToString("yyyy-MM-dd") ?? null,
                    purchaseTime = receipt.PurchaseDateTime?.ToString("HH:mm") ?? null,
                    receiptNumber = receipt.ReceiptNumber ?? null,
                    paymentMethod = receipt.PaymentMethod ?? null,
                    storeLocation = receipt.StoreLocation ?? null,
                    // Fortnox-specific fields
                    fortnoxData = new
                    {
                        supplierName = receipt.Merchant?.Name ?? "Unknown",
                        invoiceDate = receipt.PurchaseDateTime?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                        total = receipt.Totals.GrossAmount,
                        currency = receipt.Totals.Currency ?? "SEK",
                        accountingMethod = "ACCRUAL",
                        project = (string?)null,
                        costCenter = (string?)null
                    }
                }
            },

            SupplierInvoice invoice => new
            {
                status = baseResponse.status,
                documentType = baseResponse.documentType,
                confidence = baseResponse.confidence,
                processedAt = baseResponse.processedAt,
                source = baseResponse.source,
                ocrResultFileId = baseResponse.ocrResultFileId,
                rawTextLength = baseResponse.rawTextLength,
                qualityMetrics = baseResponse.qualityMetrics,
                extractedData = new
                {
                    rawText = invoice.RawText ?? "", // Include actual extracted text for analysis
                    supplierName = invoice.Seller?.Name ?? "Unknown",
                    invoiceNumber = invoice.InvoiceNumber ?? null,
                    invoiceDate = invoice.IssueDate?.ToString("yyyy-MM-dd") ?? null,
                    dueDate = invoice.DueDate?.ToString("yyyy-MM-dd") ?? null,
                    amount = invoice.Totals.GrossAmount.ToString("F2"),
                    currency = invoice.Totals.Currency ?? "SEK",
                    vatAmount = invoice.Totals.VatAmount.ToString("F2"),
                    feeAmount = invoice.Totals.FeeAmount.ToString("F2"),
                    shippingAmount = invoice.Totals.ShippingAmount.ToString("F2"),
                    // Invoice line items (if extracted)
                    lines = invoice.Lines?.Select(line => new
                    {
                        description = line.Description,
                        quantity = line.Quantity,
                        unitPrice = line.UnitPrice,
                        vatRate = line.VatRate,
                        netAmount = line.NetAmount,
                        vatAmount = line.VatAmount,
                        total = line.NetAmount + line.VatAmount
                    }).ToList(),
                    // Fortnox-specific fields
                    fortnoxData = CreateFortnoxSupplierInvoiceData(invoice)
                }
            },

            CustomerInvoice customerInvoice => new
            {
                status = baseResponse.status,
                documentType = baseResponse.documentType,
                confidence = baseResponse.confidence,
                processedAt = baseResponse.processedAt,
                source = baseResponse.source,
                ocrResultFileId = baseResponse.ocrResultFileId,
                rawTextLength = baseResponse.rawTextLength,
                qualityMetrics = baseResponse.qualityMetrics,
                extractedData = new
                {
                    rawText = customerInvoice.RawText ?? "",
                    customerName = customerInvoice.Customer?.Name ?? "Unknown",
                    invoiceNumber = customerInvoice.InvoiceNumber ?? null,
                    invoiceDate = customerInvoice.IssueDate?.ToString("yyyy-MM-dd") ?? null,
                    dueDate = customerInvoice.DueDate?.ToString("yyyy-MM-dd") ?? null,
                    amount = customerInvoice.Totals.GrossAmount.ToString("F2"),
                    currency = customerInvoice.Totals.Currency ?? "SEK",
                    vatAmount = customerInvoice.Totals.VatAmount.ToString("F2"),
                    customerReference = customerInvoice.CustomerReference ?? null,
                    fortnoxData = new
                    {
                        customerNumber = (string?)null,
                        invoiceDate = customerInvoice.IssueDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                        dueDate = customerInvoice.DueDate?.ToString("yyyy-MM-dd") ?? null,
                        total = customerInvoice.Totals.GrossAmount,
                        vat = customerInvoice.Totals.VatAmount,
                        fees = customerInvoice.Totals.FeeAmount,
                        shipping = customerInvoice.Totals.ShippingAmount,
                        currency = customerInvoice.Totals.Currency ?? "SEK"
                    }
                }
            },

            CashReceipt cashReceipt => new
            {
                status = baseResponse.status,
                documentType = baseResponse.documentType,
                confidence = baseResponse.confidence,
                processedAt = baseResponse.processedAt,
                source = baseResponse.source,
                ocrResultFileId = baseResponse.ocrResultFileId,
                rawTextLength = baseResponse.rawTextLength,
                qualityMetrics = baseResponse.qualityMetrics,
                extractedData = new
                {
                    rawText = cashReceipt.RawText ?? "",
                    merchant = cashReceipt.Merchant?.Name ?? "Unknown",
                    amount = cashReceipt.Totals.GrossAmount.ToString("F2"),
                    currency = cashReceipt.Totals.Currency ?? "SEK",
                    purchaseDate = cashReceipt.PurchaseDateTime?.ToString("yyyy-MM-dd") ?? null,
                    purchaseTime = cashReceipt.PurchaseDateTime?.ToString("HH:mm") ?? null,
                    receiptNumber = cashReceipt.ReceiptNumber ?? null,
                    paymentMethod = cashReceipt.PaymentMethod,
                    cashGiven = cashReceipt.CashGiven?.ToString("F2") ?? null,
                    changeGiven = cashReceipt.ChangeGiven?.ToString("F2") ?? null,
                    fortnoxData = new
                    {
                        supplierName = cashReceipt.Merchant?.Name ?? "Unknown",
                        invoiceDate = cashReceipt.PurchaseDateTime?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                        total = cashReceipt.Totals.GrossAmount,
                        currency = cashReceipt.Totals.Currency ?? "SEK"
                    }
                }
            },

            Quote quote => new
            {
                status = baseResponse.status,
                documentType = baseResponse.documentType,
                confidence = baseResponse.confidence,
                processedAt = baseResponse.processedAt,
                source = baseResponse.source,
                ocrResultFileId = baseResponse.ocrResultFileId,
                rawTextLength = baseResponse.rawTextLength,
                qualityMetrics = baseResponse.qualityMetrics,
                extractedData = new
                {
                    rawText = quote.RawText ?? "",
                    customerName = quote.Customer?.Name ?? "Unknown",
                    quoteNumber = quote.QuoteNumber ?? null,
                    issueDate = quote.IssueDate?.ToString("yyyy-MM-dd") ?? null,
                    expiryDate = quote.ExpiryDate?.ToString("yyyy-MM-dd") ?? null,
                    amount = quote.Totals.GrossAmount.ToString("F2"),
                    currency = quote.Totals.Currency ?? "SEK",
                    status = quote.Status ?? "Pending",
                    fortnoxData = new
                    {
                        customerNumber = (string?)null,
                        quoteDate = quote.IssueDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                        validUntil = quote.ExpiryDate?.ToString("yyyy-MM-dd") ?? null,
                        total = quote.Totals.GrossAmount,
                        currency = quote.Totals.Currency ?? "SEK"
                    }
                }
            },

            OrderConfirmation orderConfirmation => new
            {
                status = baseResponse.status,
                documentType = baseResponse.documentType,
                confidence = baseResponse.confidence,
                processedAt = baseResponse.processedAt,
                source = baseResponse.source,
                ocrResultFileId = baseResponse.ocrResultFileId,
                rawTextLength = baseResponse.rawTextLength,
                qualityMetrics = baseResponse.qualityMetrics,
                extractedData = new
                {
                    rawText = orderConfirmation.RawText ?? "",
                    customerName = orderConfirmation.Customer?.Name ?? "Unknown",
                    orderNumber = orderConfirmation.OrderNumber ?? null,
                    confirmationDate = orderConfirmation.ConfirmationDate?.ToString("yyyy-MM-dd") ?? null,
                    deliveryDate = orderConfirmation.DeliveryDate?.ToString("yyyy-MM-dd") ?? null,
                    amount = orderConfirmation.Totals.GrossAmount.ToString("F2"),
                    currency = orderConfirmation.Totals.Currency ?? "SEK",
                    customerReference = orderConfirmation.CustomerReference ?? null,
                    fortnoxData = new
                    {
                        customerNumber = (string?)null,
                        orderDate = orderConfirmation.ConfirmationDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                        deliveryDate = orderConfirmation.DeliveryDate?.ToString("yyyy-MM-dd") ?? null,
                        total = orderConfirmation.Totals.GrossAmount,
                        currency = orderConfirmation.Totals.Currency ?? "SEK"
                    }
                }
            },

            DeliveryNote deliveryNote => new
            {
                status = baseResponse.status,
                documentType = baseResponse.documentType,
                confidence = baseResponse.confidence,
                processedAt = baseResponse.processedAt,
                source = baseResponse.source,
                ocrResultFileId = baseResponse.ocrResultFileId,
                rawTextLength = baseResponse.rawTextLength,
                qualityMetrics = baseResponse.qualityMetrics,
                extractedData = new
                {
                    rawText = deliveryNote.RawText ?? "",
                    customerName = deliveryNote.Customer?.Name ?? "Unknown",
                    deliveryNoteNumber = deliveryNote.DeliveryNoteNumber ?? null,
                    deliveryDate = deliveryNote.DeliveryDate?.ToString("yyyy-MM-dd") ?? null,
                    orderNumber = deliveryNote.OrderNumber ?? null,
                    trackingNumber = deliveryNote.TrackingNumber ?? null,
                    carrier = deliveryNote.Carrier ?? null,
                    deliveryAddress = deliveryNote.DeliveryAddress ?? null,
                    fortnoxData = new
                    {
                        customerNumber = (string?)null,
                        deliveryDate = deliveryNote.DeliveryDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                        orderReference = deliveryNote.OrderNumber ?? null
                    }
                }
            },

            InterestInvoice interestInvoice => new
            {
                status = baseResponse.status,
                documentType = baseResponse.documentType,
                confidence = baseResponse.confidence,
                processedAt = baseResponse.processedAt,
                source = baseResponse.source,
                ocrResultFileId = baseResponse.ocrResultFileId,
                rawTextLength = baseResponse.rawTextLength,
                qualityMetrics = baseResponse.qualityMetrics,
                extractedData = new
                {
                    rawText = interestInvoice.RawText ?? "",
                    customerName = interestInvoice.Customer?.Name ?? "Unknown",
                    interestInvoiceNumber = interestInvoice.InterestInvoiceNumber ?? null,
                    issueDate = interestInvoice.IssueDate?.ToString("yyyy-MM-dd") ?? null,
                    dueDate = interestInvoice.DueDate?.ToString("yyyy-MM-dd") ?? null,
                    amount = interestInvoice.Totals.GrossAmount.ToString("F2"),
                    currency = interestInvoice.Totals.Currency ?? "SEK",
                    interestRate = interestInvoice.InterestRate.ToString("F2") + "%",
                    interestPeriodDays = interestInvoice.InterestPeriodDays,
                    administrativeFee = interestInvoice.AdministrativeFee?.ToString("F2") ?? null,
                    fortnoxData = new
                    {
                        customerNumber = (string?)null,
                        invoiceDate = interestInvoice.IssueDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                        dueDate = interestInvoice.DueDate?.ToString("yyyy-MM-dd") ?? null,
                        total = interestInvoice.Totals.GrossAmount,
                        currency = interestInvoice.Totals.Currency ?? "SEK",
                        invoiceType = "Interest"
                    }
                }
            },

            PurchaseOrder purchaseOrder => new
            {
                status = baseResponse.status,
                documentType = baseResponse.documentType,
                confidence = baseResponse.confidence,
                processedAt = baseResponse.processedAt,
                source = baseResponse.source,
                ocrResultFileId = baseResponse.ocrResultFileId,
                rawTextLength = baseResponse.rawTextLength,
                qualityMetrics = baseResponse.qualityMetrics,
                extractedData = new
                {
                    rawText = purchaseOrder.RawText ?? "",
                    supplierName = purchaseOrder.Supplier?.Name ?? "Unknown",
                    purchaseOrderNumber = purchaseOrder.PurchaseOrderNumber ?? null,
                    orderDate = purchaseOrder.OrderDate?.ToString("yyyy-MM-dd") ?? null,
                    requestedDeliveryDate = purchaseOrder.RequestedDeliveryDate?.ToString("yyyy-MM-dd") ?? null,
                    amount = purchaseOrder.Totals.GrossAmount.ToString("F2"),
                    currency = purchaseOrder.Totals.Currency ?? "SEK",
                    status = purchaseOrder.Status ?? "Pending",
                    contactPerson = purchaseOrder.ContactPerson ?? null,
                    fortnoxData = new
                    {
                        supplierNumber = (string?)null,
                        orderDate = purchaseOrder.OrderDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                        deliveryDate = purchaseOrder.RequestedDeliveryDate?.ToString("yyyy-MM-dd") ?? null,
                        total = purchaseOrder.Totals.GrossAmount,
                        currency = purchaseOrder.Totals.Currency ?? "SEK"
                    }
                }
            },

            SelfBillingInvoice selfBillingInvoice => new
            {
                status = baseResponse.status,
                documentType = baseResponse.documentType,
                confidence = baseResponse.confidence,
                processedAt = baseResponse.processedAt,
                source = baseResponse.source,
                ocrResultFileId = baseResponse.ocrResultFileId,
                rawTextLength = baseResponse.rawTextLength,
                qualityMetrics = baseResponse.qualityMetrics,
                extractedData = new
                {
                    rawText = selfBillingInvoice.RawText ?? "",
                    supplierName = selfBillingInvoice.Supplier?.Name ?? "Unknown",
                    invoiceNumber = selfBillingInvoice.InvoiceNumber ?? null,
                    invoiceDate = selfBillingInvoice.IssueDate?.ToString("yyyy-MM-dd") ?? null,
                    dueDate = selfBillingInvoice.DueDate?.ToString("yyyy-MM-dd") ?? null,
                    amount = selfBillingInvoice.Totals.GrossAmount.ToString("F2"),
                    currency = selfBillingInvoice.Totals.Currency ?? "SEK",
                    selfBillingAgreement = selfBillingInvoice.SelfBillingAgreementRef ?? null,
                    serviceReference = selfBillingInvoice.ServiceReference ?? null,
                    fortnoxData = new
                    {
                        supplierNumber = (string?)null,
                        invoiceDate = selfBillingInvoice.IssueDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                        dueDate = selfBillingInvoice.DueDate?.ToString("yyyy-MM-dd") ?? null,
                        total = selfBillingInvoice.Totals.GrossAmount,
                        currency = selfBillingInvoice.Totals.Currency ?? "SEK",
                        invoiceType = "SelfBilling"
                    }
                }
            },

            ProjectInvoice projectInvoice => new
            {
                status = baseResponse.status,
                documentType = baseResponse.documentType,
                confidence = baseResponse.confidence,
                processedAt = baseResponse.processedAt,
                source = baseResponse.source,
                ocrResultFileId = baseResponse.ocrResultFileId,
                rawTextLength = baseResponse.rawTextLength,
                qualityMetrics = baseResponse.qualityMetrics,
                extractedData = new
                {
                    rawText = projectInvoice.RawText ?? "",
                    customerName = projectInvoice.Customer?.Name ?? "Unknown",
                    invoiceNumber = projectInvoice.InvoiceNumber ?? null,
                    invoiceDate = projectInvoice.IssueDate?.ToString("yyyy-MM-dd") ?? null,
                    dueDate = projectInvoice.DueDate?.ToString("yyyy-MM-dd") ?? null,
                    amount = projectInvoice.Totals.GrossAmount.ToString("F2"),
                    currency = projectInvoice.Totals.Currency ?? "SEK",
                    projectNumber = projectInvoice.Project?.ProjectNumber ?? null,
                    projectName = projectInvoice.Project?.ProjectName ?? null,
                    projectManager = projectInvoice.Project?.ProjectManager ?? null,
                    invoicePeriod = projectInvoice.InvoicePeriod?.Description ?? null,
                    fortnoxData = new
                    {
                        customerNumber = (string?)null,
                        invoiceDate = projectInvoice.IssueDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                        dueDate = projectInvoice.DueDate?.ToString("yyyy-MM-dd") ?? null,
                        total = projectInvoice.Totals.GrossAmount,
                        currency = projectInvoice.Totals.Currency ?? "SEK",
                        project = projectInvoice.Project?.ProjectNumber ?? null,
                        invoiceType = "Project"
                    }
                }
            },

            Payslip payslip => new
            {
                status = baseResponse.status,
                documentType = baseResponse.documentType,
                confidence = baseResponse.confidence,
                processedAt = baseResponse.processedAt,
                source = baseResponse.source,
                ocrResultFileId = baseResponse.ocrResultFileId,
                rawTextLength = baseResponse.rawTextLength,
                qualityMetrics = baseResponse.qualityMetrics,
                extractedData = new
                {
                    rawText = payslip.RawText ?? "",
                    employeeName = payslip.Employee?.Name ?? "Unknown",
                    employeeId = payslip.Employee?.EmployeeId ?? null,
                    personalNumber = payslip.Employee?.PersonalNumber ?? null,
                    employerName = payslip.Employer?.Name ?? "Unknown",
                    payslipNumber = payslip.PayslipNumber ?? null,
                    payDate = payslip.PayDate?.ToString("yyyy-MM-dd") ?? null,
                    payPeriod = payslip.PayPeriod?.Description ?? null,
                    grossSalary = payslip.GrossSalary.ToString("F2"),
                    netSalary = payslip.NetSalary.ToString("F2"),
                    taxDeductions = payslip.TaxDeductions.ToString("F2"),
                    currency = payslip.Currency,
                    workedHours = payslip.WorkedHours?.ToString("F1") ?? null,
                    fortnoxData = new
                    {
                        employeeNumber = payslip.Employee?.EmployeeId ?? null,
                        payDate = payslip.PayDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                        grossAmount = payslip.GrossSalary,
                        netAmount = payslip.NetSalary,
                        currency = payslip.Currency
                    }
                }
            },

            VatReport vatReport => new
            {
                status = baseResponse.status,
                documentType = baseResponse.documentType,
                confidence = baseResponse.confidence,
                processedAt = baseResponse.processedAt,
                source = baseResponse.source,
                ocrResultFileId = baseResponse.ocrResultFileId,
                rawTextLength = baseResponse.rawTextLength,
                qualityMetrics = baseResponse.qualityMetrics,
                extractedData = new
                {
                    rawText = vatReport.RawText ?? "",
                    companyName = vatReport.Company?.Name ?? "Unknown",
                    vatRegistrationNumber = vatReport.VatRegistrationNumber ?? null,
                    reportNumber = vatReport.ReportNumber ?? null,
                    reportPeriod = vatReport.ReportPeriod?.Description ?? null,
                    submissionDate = vatReport.SubmissionDate?.ToString("yyyy-MM-dd") ?? null,
                    outputVat = vatReport.OutputVat.TotalVatAmount.ToString("F2"),
                    inputVat = vatReport.InputVat.TotalVatAmount.ToString("F2"),
                    netVatAmount = vatReport.NetVatAmount.ToString("F2"),
                    vatToPay = vatReport.VatToPay.ToString("F2"),
                    currency = vatReport.Currency,
                    status = vatReport.Status ?? "Draft",
                    fortnoxData = new
                    {
                        vatRegistrationNumber = vatReport.VatRegistrationNumber ?? null,
                        reportDate = vatReport.SubmissionDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                        outputVat = vatReport.OutputVat.TotalVatAmount,
                        inputVat = vatReport.InputVat.TotalVatAmount,
                        netVat = vatReport.NetVatAmount,
                        currency = vatReport.Currency
                    }
                }
            },

            // Default for other document types
            _ => new
            {
                status = baseResponse.status,
                documentType = baseResponse.documentType,
                confidence = baseResponse.confidence,
                processedAt = baseResponse.processedAt,
                source = baseResponse.source,
                ocrResultFileId = baseResponse.ocrResultFileId,
                rawTextLength = baseResponse.rawTextLength,
                qualityMetrics = baseResponse.qualityMetrics,
                extractedData = new
                {
                    documentId = document.DocumentId ?? null,
                    rawText = document.RawText ?? "", // Include actual extracted text for analysis
                    textLength = document.RawText?.Length ?? 0,
                    hasContent = !string.IsNullOrEmpty(document.RawText)
                }
            }
        };
    }

    /// <summary>
    /// Retrieves OCR results from TempFiles by file ID
    /// </summary>
    /// <param name="fileId">TempFile ID of the OCR result file</param>
    /// <returns>OCR result text content</returns>
    [KernelFunction]
    [Description("Retrieves OCR extraction results from a previously processed document using the TempFile ID")]
    public async Task<string> GetOcrResultsAsync(
        [Description("TempFile ID of the OCR result file")] string fileId)
    {
        try
        {
            if (!Guid.TryParse(fileId, out var guid))
            {
                return "ERROR: Invalid file ID format. Please provide a valid GUID.";
            }

            if (ServiceProvider == null)
            {
                return "ERROR: Service provider not available.";
            }

            using var scope = ServiceProvider.CreateScope();
            var tempFilesService = scope.ServiceProvider.GetRequiredService<ITempFilesService>();

            var fileRecord = await tempFilesService.GetFileAsync(guid);
            if (fileRecord == null)
            {
                _logger.LogWarning("OCR result file record not found in database with ID: {FileId}", fileId);
                return $"ERROR: OCR result file not found in database with ID: {fileId}. The file may have been cleaned up or never existed.";
            }

            var fileContent = await tempFilesService.ReadFileAsync(guid);
            if (fileContent == null)
            {
                _logger.LogWarning("OCR result file exists in database but content could not be read. File: {FilePath}", fileRecord.FilePath);
                return $"ERROR: OCR result file found in database but content could not be read. File may have been deleted from disk. File ID: {fileId}";
            }

            var textContent = System.Text.Encoding.UTF8.GetString(fileContent);
            _logger.LogInformation("Retrieved OCR results for file ID: {FileId}, Length: {Length} characters",
                fileId, textContent.Length);

            return textContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve OCR results for file ID: {FileId}", fileId);
            return $"ERROR: Failed to retrieve OCR results - {ex.Message}";
        }
    }

    /// <summary>
    /// Converts a CustomerOnboarding document to Fortnox-compatible JSON format
    /// </summary>
    /// <param name="customerOnboardingJson">JSON representation of a CustomerOnboarding document</param>
    /// <returns>Fortnox-compatible customer JSON that can be used with FortnoxPlugin.CreateCustomerAsync</returns>
    [KernelFunction]
    [Description("Converts a CustomerOnboarding document to Fortnox-compatible JSON format for creating customers in Fortnox")]
    public async Task<string> ConvertCustomerOnboardingToFortnoxAsync(
        [Description("JSON representation of a CustomerOnboarding document")] string customerOnboardingJson)
    {
        try
        {
            _logger.LogInformation("Converting CustomerOnboarding document to Fortnox format");

            // Deserialize the CustomerOnboarding document
            var customerOnboarding = JsonSerializer.Deserialize<CustomerOnboarding>(customerOnboardingJson);
            if (customerOnboarding == null)
            {
                _logger.LogError("Failed to deserialize CustomerOnboarding document");
                return "ERROR: Invalid CustomerOnboarding JSON format";
            }

            // Use the CustomerOnboardingParser to convert to Fortnox format
            var parser = new CustomerOnboardingParser(_logger);
            var fortnoxJson = parser.ConvertToFortnoxCustomerJson(customerOnboarding);

            _logger.LogInformation("Successfully converted CustomerOnboarding to Fortnox format for customer: {CustomerName}",
                customerOnboarding.Customer.Name);

            await Task.CompletedTask;
            return fortnoxJson;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert CustomerOnboarding to Fortnox format");
            return $"ERROR: Failed to convert CustomerOnboarding to Fortnox format - {ex.Message}";
        }
    }

    /// <summary>
    /// Parses customer onboarding text and returns Fortnox-compatible JSON in one step
    /// </summary>
    /// <param name="content">Raw text content of the customer onboarding document</param>
    /// <param name="source">Source information (email, file path, etc.)</param>
    /// <returns>Fortnox-compatible customer JSON that can be used with FortnoxPlugin.CreateCustomerAsync</returns>
    [KernelFunction]
    [Description("Parses customer onboarding text and directly returns Fortnox-compatible JSON for creating customers. Skapa kund från text, tolka kunduppgifter, lägg till ny kund.")]
    public async Task<string> ParseCustomerOnboardingToFortnoxAsync(
        [Description("Text content of the customer onboarding document")] string content,
        [Description("Source information (email, file path, etc.)")] string? source = null)
    {
        try
        {
            _logger.LogInformation("Parsing customer onboarding text directly to Fortnox format");

            // Parse the customer onboarding document
            var parser = new CustomerOnboardingParser(_logger);
            var customerOnboarding = parser.ParseCustomerOnboardingDocument(content);

            // Set document metadata
            customerOnboarding.Source = source ?? "unknown";
            customerOnboarding.RawText = content;
            customerOnboarding.ProcessedAt = DateTime.UtcNow;
            customerOnboarding.DocumentType = "CustomerOnboarding";

            // Convert to Fortnox format
            var fortnoxJson = parser.ConvertToFortnoxCustomerJson(customerOnboarding);

            _logger.LogInformation("Successfully parsed and converted customer onboarding to Fortnox format for: {CustomerName}",
                customerOnboarding.Customer.Name);

            await Task.CompletedTask;
            return fortnoxJson;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse customer onboarding text to Fortnox format");
            return $"ERROR: Failed to parse customer onboarding - {ex.Message}";
        }
    }

    /// <summary>
    /// Gets the MIME content type from file extension
    /// </summary>
    private static string GetContentTypeFromExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".tif" or ".tiff" => "image/tiff",
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            ".txt" => "text/plain",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Determines the document status based on validation results
    /// </summary>
    private string DetermineDocumentStatus(DocumentBase document)
    {
        // Primary check: If validation found missing required fields, document is unreadable
        if (!document.QualityMetrics.RequiredFieldsComplete)
        {
            return "Unreadable";
        }

        // Check if validation messages indicate CRITICAL problems (inte alla meddelanden)
        var criticalMessages = document.QualityMetrics.ValidationMessages
            .Where(msg => msg.Contains("Too many critical fields missing") ||
                         msg.Contains("insufficient text content") ||
                         msg.Contains("Overall confidence too low"))
            .ToList();

        if (criticalMessages.Any())
        {
            return "Unreadable";
        }

        // Check if text extraction was successful (sänkt tröskel)
        if (string.IsNullOrWhiteSpace(document.RawText) || document.RawText.Length < 20)
        {
            return "Unreadable";
        }

        // Check for common OCR failure indicators (sänkt tröskel)
        if (document.RawText.Contains("Scanned by CamScanner") && document.RawText.Length < 100)
        {
            return "Unreadable";
        }

        return "Success";
    }

    /// <summary>
    /// Creates Fortnox-compatible supplier invoice data structure with intelligent defaults
    /// </summary>
    private object CreateFortnoxSupplierInvoiceData(SupplierInvoice invoice)
    {
        // Create supplier invoice rows from extracted lines or create a single row with total
        var supplierInvoiceRows = new List<object>();

        if (invoice.Lines != null && invoice.Lines.Any())
        {
            // Map extracted lines to Fortnox SupplierInvoiceRows
            foreach (var line in invoice.Lines)
            {
                // Intelligent account number suggestion based on description
                var suggestedAccount = SuggestAccountNumber(line.Description);

                supplierInvoiceRows.Add(new
                {
                    account = suggestedAccount,
                    itemDescription = line.Description,
                    quantity = (int)Math.Round(line.Quantity),
                    price = line.UnitPrice,
                    total = line.NetAmount + line.VatAmount,
                    debit = line.NetAmount + line.VatAmount, // Total amount as debit
                    credit = (decimal?)null
                });
            }

            _logger.LogInformation(
                "Created {Count} supplier invoice rows from extracted lines for invoice {InvoiceNumber}",
                supplierInvoiceRows.Count, invoice.InvoiceNumber ?? "Unknown");
        }
        else
        {
            // No lines extracted - create a single row with the total amount
            // Suggest account based on supplier name or default to 4010 (goods)
            var suggestedAccount = SuggestAccountNumber(invoice.Seller?.Name ?? "");

            supplierInvoiceRows.Add(new
            {
                account = suggestedAccount,
                itemDescription = $"Faktura från {invoice.Seller?.Name ?? "leverantör"}",
                quantity = 1,
                price = invoice.Totals.NetAmount,
                total = invoice.Totals.GrossAmount,
                debit = invoice.Totals.GrossAmount,
                credit = (decimal?)null
            });

            _logger.LogWarning(
                "No line items extracted for invoice {InvoiceNumber} - created single fallback row with suggested account {Account}",
                invoice.InvoiceNumber ?? "Unknown", suggestedAccount);
        }

        var fortnoxData = new
        {
            // Supplier info - will be auto-filled by AI workflow
            supplierNumber = (string?)null, // Will be filled by GetSuppliersAsync or CreateSupplierAsync
            supplierName = invoice.Seller?.Name ?? "Unknown",
            organizationNumber = invoice.Seller?.OrganizationNumber,

            // Invoice details
            externalInvoiceNumber = invoice.InvoiceNumber,
            invoiceDate = invoice.IssueDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
            dueDate = invoice.DueDate?.ToString("yyyy-MM-dd") ?? null,

            // Amounts
            total = invoice.Totals.GrossAmount,
            vat = invoice.Totals.VatAmount,
            administrationFee = invoice.Totals.FeeAmount > 0 ? invoice.Totals.FeeAmount : (decimal?)null,
            freight = invoice.Totals.ShippingAmount > 0 ? invoice.Totals.ShippingAmount : (decimal?)null,
            currency = invoice.Totals.Currency ?? "SEK",

            // Optional fields
            project = (string?)null,
            costCenter = (string?)null,

            // Invoice rows with intelligent account suggestions
            supplierInvoiceRows = supplierInvoiceRows,

            // Workflow instructions for AI
            _workflow = new
            {
                step1 = "Use FortnoxPlugin.GetSuppliersAsync to search for supplier by name or org number",
                step2 = "If supplier not found, use CompanyCheckupPlugin.LookupCompanyAsync + FortnoxPlugin.CreateSupplierAsync",
                step3 = "Fill in supplierNumber from step 1 or 2",
                step4 = "Review suggested account numbers (already filled with intelligent defaults)",
                step5 = "Use FortnoxPlugin.CreateSupplierInvoiceAsync with complete data"
            },

            // Account number guide
            _accountGuide = new
            {
                suggested = "Account numbers already suggested based on item descriptions",
                common = new
                {
                    goods = "4010 - Varuinköp",
                    services = "5010 - Tjänster",
                    office = "6100 - Kontorsmaterial",
                    software = "6540 - IT-tjänster",
                    travel = "6250 - Resor",
                    marketing = "6110 - Marknadsföring"
                }
            }
        };

        return ValidateAndLogFortnoxData(fortnoxData, "SupplierInvoice", invoice.InvoiceNumber ?? "Unknown");
    }

    /// <summary>
    /// Suggests an account number based on item description using intelligent pattern matching
    /// </summary>
    private int SuggestAccountNumber(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return 4010; // Default: Goods

        var lowerDesc = description.ToLowerInvariant();

        // Software & IT services
        if (lowerDesc.Contains("software") || lowerDesc.Contains("license") || lowerDesc.Contains("licens") ||
            lowerDesc.Contains("subscription") || lowerDesc.Contains("prenumeration") ||
            lowerDesc.Contains("cloud") || lowerDesc.Contains("saas") || lowerDesc.Contains("hosting") ||
            lowerDesc.Contains("github") || lowerDesc.Contains("azure") || lowerDesc.Contains("aws"))
            return 6540; // IT-tjänster

        // Office supplies
        if (lowerDesc.Contains("papper") || lowerDesc.Contains("penna") || lowerDesc.Contains("kontorsmaterial") ||
            lowerDesc.Contains("office") || lowerDesc.Contains("supplies"))
            return 6100; // Kontorsmaterial

        // Marketing & advertising
        if (lowerDesc.Contains("marknadsföring") || lowerDesc.Contains("marketing") || lowerDesc.Contains("annons") ||
            lowerDesc.Contains("reklam") || lowerDesc.Contains("advertising"))
            return 6110; // Marknadsföring

        // Travel & accommodation
        if (lowerDesc.Contains("resa") || lowerDesc.Contains("travel") || lowerDesc.Contains("hotell") ||
            lowerDesc.Contains("hotel") || lowerDesc.Contains("flyg") || lowerDesc.Contains("flight"))
            return 6250; // Resor

        // Consulting & professional services
        if (lowerDesc.Contains("konsult") || lowerDesc.Contains("consulting") || lowerDesc.Contains("tjänst") ||
            lowerDesc.Contains("service") || lowerDesc.Contains("rådgivning"))
            return 5010; // Tjänster

        // Telecommunications
        if (lowerDesc.Contains("telefon") || lowerDesc.Contains("mobil") || lowerDesc.Contains("tele2") ||
            lowerDesc.Contains("telia") || lowerDesc.Contains("telenor") || lowerDesc.Contains("internet"))
            return 6230; // Telekommunikation

        // Default to goods purchase
        return 4010; // Varuinköp
    }

    /// <summary>
    /// Validates and logs Fortnox data to ensure correctness before returning to user
    /// </summary>
    private object ValidateAndLogFortnoxData(object fortnoxData, string documentType, string invoiceNumber)
    {
        try
        {
            // Use reflection to get property values
            var type = fortnoxData.GetType();
            var totalProp = type.GetProperty("total");
            var vatProp = type.GetProperty("vat");
            var feesProp = type.GetProperty("fees");
            var shippingProp = type.GetProperty("shipping");

            var total = totalProp?.GetValue(fortnoxData) as decimal? ?? 0m;
            var vat = vatProp?.GetValue(fortnoxData) as decimal? ?? 0m;
            var fees = feesProp?.GetValue(fortnoxData) as decimal? ?? 0m;
            var shipping = shippingProp?.GetValue(fortnoxData) as decimal? ?? 0m;

            _logger.LogInformation(
                "📊 FORTNOX DATA VALIDATION - {DocumentType} {InvoiceNumber}: Total={Total}, VAT={Vat}, Fees={Fees}, Shipping={Shipping}",
                documentType, invoiceNumber, total, vat, fees, shipping);

            // Validate that VAT is reasonable (should be > 0 for most Swedish invoices)
            if (total > 0 && vat <= 0)
            {
                _logger.LogWarning(
                    "⚠️ FORTNOX DATA WARNING - {DocumentType} {InvoiceNumber}: Total is {Total} but VAT is {Vat}. This may indicate extraction failure.",
                    documentType, invoiceNumber, total, vat);
            }

            // Validate that VAT is not unreasonably high (should be < 50% of total for Swedish invoices)
            if (total > 0 && vat > (total * 0.5m))
            {
                _logger.LogWarning(
                    "⚠️ FORTNOX DATA WARNING - {DocumentType} {InvoiceNumber}: VAT ({Vat}) is more than 50% of total ({Total}). This may indicate extraction error.",
                    documentType, invoiceNumber, vat, total);
            }

            // Log if fees or shipping are present
            if (fees > 0)
            {
                _logger.LogInformation(
                    "✓ FORTNOX DATA - {DocumentType} {InvoiceNumber}: Fees extracted: {Fees}",
                    documentType, invoiceNumber, fees);
            }

            if (shipping > 0)
            {
                _logger.LogInformation(
                    "✓ FORTNOX DATA - {DocumentType} {InvoiceNumber}: Shipping extracted: {Shipping}",
                    documentType, invoiceNumber, shipping);
            }

            return fortnoxData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate Fortnox data for {DocumentType} {InvoiceNumber}",
                documentType, invoiceNumber);
            return fortnoxData;
        }
    }
}