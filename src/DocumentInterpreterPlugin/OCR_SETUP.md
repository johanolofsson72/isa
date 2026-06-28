# OCR Setup for DocumentInterpreterPlugin

## Overview

The DocumentInterpreterPlugin now includes OCR (Optical Character Recognition) capabilities to process scanned documents and images. This enables the plugin to extract text from:

- PDF documents (both text-based and scanned)
- Images (JPG, PNG, TIFF, BMP, GIF)
- Scanned financial documents (invoices, receipts, etc.)

## OCR Engine

The plugin uses **Tesseract OCR** for image text recognition and **iText7** for PDF text extraction.

### Tesseract Language Data

For OCR to work properly, you need to download Tesseract language data files:

1. Create a `tessdata` folder in your application directory
2. Download language data from: https://github.com/tesseract-ocr/tessdata
3. Place the `.traineddata` files in the tessdata folder

**Required languages for financial documents:**
- `eng.traineddata` - English
- `swe.traineddata` - Swedish (for Swedish financial documents)

**Example setup:**
```
/your-app-directory/
├── tessdata/
│   ├── eng.traineddata
│   └── swe.traineddata
```

## Usage Examples

### Processing Images with OCR

```csharp
// Process a scanned invoice image
var base64Image = Convert.ToBase64String(imageBytes);
var result = await plugin.InterpretDocumentFromBinaryAsync(
    base64Image, 
    "image/jpeg", 
    "invoice.jpg", 
    "SupplierInvoice"
);
```

### Processing PDF Documents

```csharp
// Process a PDF document (text extraction + OCR for scanned pages)
var base64Pdf = Convert.ToBase64String(pdfBytes);
var result = await plugin.InterpretDocumentFromBinaryAsync(
    base64Pdf, 
    "application/pdf", 
    "invoice.pdf", 
    "SupplierInvoice"
);
```

### Processing Files Directly

```csharp
// Process a file directly from file system
var result = await plugin.InterpretDocumentFromFileAsync(
    "/path/to/scanned-receipt.png",
    "Receipt",
    "Dropbox folder"
);
```

## Supported File Formats

| Format | Extension | Content Type | Processing Method |
|--------|-----------|--------------|-------------------|
| PDF | .pdf | application/pdf | iText7 text extraction |
| JPEG | .jpg, .jpeg | image/jpeg | Tesseract OCR |
| PNG | .png | image/png | Tesseract OCR |
| TIFF | .tif, .tiff | image/tiff | Tesseract OCR |
| BMP | .bmp | image/bmp | Tesseract OCR |
| Plain Text | .txt | text/plain | Direct UTF-8 decoding |

## Configuration

Add OCR settings to your configuration:

```json
{
  "DocumentInterpreter": {
    "MinConfidenceThreshold": 0.7,
    "TimeoutSeconds": 60,
    "VerboseLogging": true,
    "OcrSettings": {
      "Languages": "eng+swe",
      "PageSegMode": "Auto",
      "EnhanceImages": true
    }
  }
}
```

## Integration with Cloud Storage

The OCR functionality integrates seamlessly with the Dropbox workflow:

1. **DropboxPlugin** lists files in a folder
2. **DropboxPlugin** downloads file content
3. **DocumentInterpreterPlugin** performs OCR/text extraction
4. **Agent** analyzes the extracted content with AI
5. **FortnoxPlugin** creates entries in the accounting system

```csharp
// Example workflow
var files = await dropboxPlugin.ListFiles("/invoices");
foreach (var file in files)
{
    var content = await dropboxPlugin.ReadFile(file.path);
    var document = await documentInterpreter.InterpretDocumentFromBinaryAsync(
        content, file.contentType, file.name
    );
    // Agent processes the document...
}
```

## Error Handling

The OCR service provides robust error handling:

- **Missing Tesseract data**: Logs warning and creates tessdata directory
- **Low OCR confidence**: Logs warning when confidence < 50%
- **Unsupported formats**: Throws `NotSupportedException`
- **Processing errors**: Throws `InvalidOperationException` with details

## Performance Considerations

- **Image preprocessing**: Images are automatically converted to grayscale and contrast-enhanced for better OCR results
- **Memory usage**: Large images are processed in chunks to manage memory
- **Timeout handling**: Configurable timeouts prevent hanging on problematic documents
- **Parallel processing**: Multiple documents can be processed concurrently

## Quality Metrics

The OCR service provides quality metrics:

```json
{
  "documentType": "SupplierInvoice",
  "confidenceScore": 0.85,
  "qualityMetrics": {
    "textQuality": 0.9,
    "fieldAccuracy": 0.8,
    "overallConfidence": 0.85,
    "ocrConfidence": 0.78
  }
}
```

## Troubleshooting

### OCR not working
1. Check if tessdata folder exists and contains language files
2. Verify image quality - low resolution images may have poor OCR results
3. Check logs for Tesseract initialization errors

### Poor OCR results
1. Ensure images are high resolution (300+ DPI recommended)
2. Use images with good contrast
3. Try different image formats (PNG usually works better than JPEG)

### PDF processing issues
1. Some PDFs may be password protected or corrupted
2. Scanned PDFs require OCR processing, which takes longer
3. Very large PDFs may timeout - increase TimeoutSeconds in configuration