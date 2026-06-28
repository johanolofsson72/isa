# OCR Testing Example

Here's how to test the OCR functionality with the DocumentInterpreterPlugin:

## Example 1: Processing a Text File (Simple Test)

```csharp
// Create a simple text document
var testInvoice = @"
FAKTURA NR: 2024-001
Leverantör: Test Företag AB
Org.nr: 556123-4567

Beskrivning: Konsulttjänster
Belopp: 10,000 SEK
Moms: 2,500 SEK
Totalt: 12,500 SEK
";

var textBytes = System.Text.Encoding.UTF8.GetBytes(testInvoice);
var base64Content = Convert.ToBase64String(textBytes);

// Process with the plugin
var result = await plugin.InterpretDocumentFromBinaryAsync(
    base64Content,
    "text/plain", 
    "test-invoice.txt",
    "SupplierInvoice",
    "test"
);

Console.WriteLine(result);
```

## Example 2: Agent Integration Test

```csharp
// How the agent would use the plugin for document type detection
var unknownDocument = "ICA Supermarket\nKvitto: 12345\nDatum: 2024-01-15\nBelopp: 100 kr";

// Agent asks plugin to detect document type
var typeResult = await plugin.DetectDocumentTypeAsync(unknownDocument);
Console.WriteLine($"Document type detected: {typeResult}");

// Agent then asks plugin to parse the document
var parseResult = await plugin.InterpretDocumentFromTextAsync(
    unknownDocument, 
    null, // Let plugin auto-detect
    "test-source"
);
Console.WriteLine($"Parsed document: {parseResult}");
```

## Example 3: Complete Dropbox Workflow (Concept)

```csharp
// This shows how all plugins work together:

// 1. List files in Dropbox folder
var dropboxFiles = await dropboxPlugin.ListFiles("/inkommande-fakturor");

foreach (var file in dropboxFiles)
{
    // 2. Download file content from Dropbox
    var fileContent = await dropboxPlugin.ReadFileAsBinary(file.path);
    var base64Content = Convert.ToBase64String(fileContent);
    
    // 3. DocumentInterpreter processes with OCR
    var documentResult = await documentInterpreterPlugin.InterpretDocumentFromBinaryAsync(
        base64Content,
        file.contentType,
        file.name
    );
    
    // 4. Agent analyzes the parsed document with AI
    var aiAnalysis = await agent.AnalyzeDocument(documentResult);
    
    // 5. FortnoxPlugin creates accounting entry
    await fortnoxPlugin.CreateSupplierInvoice(aiAnalysis);
}
```

## Testing OCR with Real Files

To test with actual image files, you would:

1. Save an image of an invoice/receipt
2. Read it as bytes: `var imageBytes = File.ReadAllBytes("invoice.jpg");`
3. Convert to base64: `var base64 = Convert.ToBase64String(imageBytes);`
4. Process: `await plugin.InterpretDocumentFromBinaryAsync(base64, "image/jpeg", "invoice.jpg");`

## Configuration for Testing

Add to appsettings.json:

```json
{
  "DocumentInterpreter": {
    "MinConfidenceThreshold": 0.5,
    "TimeoutSeconds": 60,
    "VerboseLogging": true
  }
}
```

## Expected Results

The plugin will return structured JSON with:
- Document type detection
- Extracted text content  
- Basic field parsing (invoice numbers, dates, amounts)
- Quality metrics and confidence scores
- Processing metadata

The Agent can then use its AI capabilities to enhance this basic extraction with sophisticated content analysis and interpretation.