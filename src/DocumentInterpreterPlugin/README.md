# DocumentInterpreterPlugin

A plugin for parsing and structuring financial documents with OCR capabilities within the AccountingAgent ecosystem.

## Overview

The DocumentInterpreterPlugin provides comprehensive document processing capabilities for financial documents such as invoices, receipts, credit notes, and bank statements. The plugin includes OCR (Optical Character Recognition) for processing scanned documents and images, while AI-powered content analysis is handled by the AccountingAgent itself.

## Architecture

- **Plugin Role**: Document parsing, OCR text extraction, structure detection, basic field extraction
- **Agent Role**: AI-powered content analysis, decision making, complex interpretation  
- **Integration**: The agent calls this plugin to parse documents (including OCR processing), then uses its own AI capabilities to analyze the content

## OCR Capabilities

The plugin now includes full OCR support for processing scanned financial documents:

- **Image OCR**: Tesseract-based text extraction from JPG, PNG, TIFF, BMP images
- **PDF Processing**: iText7-based text extraction from both text-based and scanned PDFs  
- **Image Enhancement**: Automatic preprocessing for better OCR results
- **Multi-language**: Supports English and Swedish for financial document processing

**Supported Formats**: PDF, JPG/JPEG, PNG, TIFF, BMP, TXT, DOC, DOCX, XLS, XLSX

*Note: Microsoft Office documents (.doc, .docx, .xls, .xlsx) are supported with basic text extraction. For best results, convert Office documents to PDF format before processing.*

## Supported Document Types

- **SupplierInvoice**: Vendor invoices with line items and payment information
- **CustomerInvoice**: Customer billing documents
- **Receipt**: Store receipts and purchase confirmations
- **CreditNote**: Credit notes and refund documents
- **PaymentAdvice**: Payment notification documents
- **BankStatement**: Account statements with transaction history
- **CustomsDocument**: Import/export customs documents
- **ExpenseReport**: Employee expense reports
- **ReminderNotice**: Payment reminder notices
- **VendorOnboarding**: Vendor registration documents
- **CustomerOnboarding**: Customer information files for creating new customers in Fortnox

### CustomerOnboarding Document Format

The CustomerOnboarding document type processes structured text files containing customer information for Fortnox integration.

**Required fields:**
- `Namn:` Customer name
- `Adress:` Billing address
- `Stad:` City
- `Postnummer:` Postal code
- `Landskod:` Country code (2 characters, e.g., SE)
- `E-post:` Email address
- `Typ:` Customer type (COMPANY or PRIVATE)

**Optional fields:**
- `Telefonnummer:` Phone number
- `Organisationsnummer:` Organization number
- `Land:` Country name
- `Valuta:` Currency (defaults to SEK)
- `Leveransadress:` Delivery address (if different from billing)
- `Kommentarer:` Comments about the customer
- `Webbplats:` Website URL
- `Betalningsvillkor:` Payment terms
- `Leveransvillkor:` Delivery terms

**Example format:**
```
Nytt företag att lägga in i Fortnox

Namn: Bamse AB
Adress: Hemma 1
Stad: Staden
Postnummer: 123 45
Landskod: SE
E-post: bamse@bamse.se
Typ: COMPANY
Telefonnummer: 08-123 456 78
Organisationsnummer: 556123-4567
Webbplats: https://bamse.se
```

## Key Functions

### `InterpretDocumentFromTextAsync`
Parses document text and creates structured document objects with:
- Document type detection using keyword patterns
- Basic field extraction using regex patterns
- Document structure validation
- Quality metrics

### `InterpretDocumentFromBinaryAsync` *(NEW)*
Processes binary documents (PDFs, images) with OCR:
- Automatic OCR text extraction from images
- PDF text extraction (text-based and scanned)
- Base64 content support for web integration
- Enhanced quality metrics including OCR confidence

### `InterpretDocumentFromFileAsync` *(NEW)*
Processes documents directly from file system:
- Automatic content type detection from file extension
- Direct file processing without manual encoding
- Perfect for cloud storage integration (Dropbox, Google Drive)

### `DetectDocumentTypeAsync`
Identifies document types using keyword patterns and document structure analysis.

### `ValidateDocumentAsync`
Validates parsed document structures for completeness and basic accuracy.

### `GetSupportedDocumentTypes`
Returns list of supported financial document types.

## Usage Examples

### Text Processing
```csharp
// The agent would call the plugin like this:
var result = await plugin.InterpretDocumentFromTextAsync(documentContent, "SupplierInvoice", "email");
var document = JsonSerializer.Deserialize<SupplierInvoice>(result);

// Then the agent uses its AI to analyze and enhance the document data
var aiAnalysis = await agent.AnalyzeDocument(result);
```

### OCR Processing
```csharp
// Process a scanned invoice image
var imageBytes = File.ReadAllBytes("scanned-invoice.jpg");
var base64Content = Convert.ToBase64String(imageBytes);

var result = await plugin.InterpretDocumentFromBinaryAsync(
    base64Content, 
    "image/jpeg", 
    "scanned-invoice.jpg",
    "SupplierInvoice"
);

// Process a PDF document  
var pdfBytes = File.ReadAllBytes("invoice.pdf");
var base64Pdf = Convert.ToBase64String(pdfBytes);

var pdfResult = await plugin.InterpretDocumentFromBinaryAsync(
    base64Pdf,
    "application/pdf", 
    "invoice.pdf"
);
```

### File Processing
```csharp
// Process a file directly (great for Dropbox integration)
var result = await plugin.InterpretDocumentFromFileAsync(
    "/path/to/document.pdf",
    "SupplierInvoice", 
    "Dropbox:/invoices"
);
```

### Complete Workflow with Dropbox
```csharp
// 1. Agent asks Dropbox plugin for files
var files = await dropboxPlugin.ListFiles("/inkommande-fakturor");

foreach (var file in files)
{
    // 2. Agent asks Dropbox plugin to read file
    var content = await dropboxPlugin.ReadFileAsBinary(file.path);
    var base64 = Convert.ToBase64String(content);
    
    // 3. Agent asks DocumentInterpreter to process with OCR
    var document = await documentInterpreterPlugin.InterpretDocumentFromBinaryAsync(
        base64, file.contentType, file.name
    );
    
    // 4. Agent analyzes with AI
    var analysis = await agent.AnalyzeDocument(document);
    
    // 5. Agent creates Fortnox entry
    await fortnoxPlugin.CreateSupplierInvoice(analysis);
}
```

## Configuration

```json
{
  "DocumentInterpreter": {
    "MinConfidenceThreshold": 0.7,
    "VerboseLogging": false,
    "TimeoutSeconds": 30
  }
}
```

## Output Format

All documents return JSON with consistent structure:
- Document metadata (type, source, processing time)
- Extracted fields specific to document type (basic pattern matching)
- Quality metrics and confidence scores
- Validation results and warnings

The plugin outputs structured data that the AccountingAgent can then analyze and enhance using its AI capabilities.

## Testing

```bash
dotnet test tests/DocumentInterpreterPlugin.Tests/
```

## Dependencies

- Microsoft.SemanticKernel
- Microsoft.Extensions.Configuration  
- Microsoft.Extensions.Logging
- System.Text.Json
- **Tesseract** (OCR engine for image processing)
- **iText7** (PDF text extraction)
- **SixLabors.ImageSharp** (Image preprocessing)
- Shared project (for plugin registration interfaces)

## OCR Setup

For OCR to work properly, you need Tesseract language data:

1. Create a `tessdata` folder in your application directory
2. Download language files from https://github.com/tesseract-ocr/tessdata
3. Place `eng.traineddata` and `swe.traineddata` in the tessdata folder

See [OCR_SETUP.md](OCR_SETUP.md) for detailed setup instructions.