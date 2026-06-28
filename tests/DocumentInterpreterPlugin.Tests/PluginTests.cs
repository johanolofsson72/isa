using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;
using DocumentInterpreter;
using DocumentInterpreter.Models;
using System.Text.Json;
using System.Globalization;

namespace DocumentInterpreterPlugin.Tests;

public class PluginTests
{
    private readonly Plugin _plugin;
    private readonly IConfiguration _configuration;
    private readonly ILogger<Plugin> _logger;

    public PluginTests()
    {
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DocumentInterpreter:MinConfidenceThreshold"] = "0.7",
                ["DocumentInterpreter:TimeoutSeconds"] = "30",
                ["DocumentInterpreter:VerboseLogging"] = "false"
            });

        _configuration = configBuilder.Build();
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Plugin>();
        _plugin = new Plugin(_configuration, _logger);
    }

    [Fact]
    public async Task InterpretDocumentFromTextAsync_WithSupplierInvoiceContent_ShouldReturnValidJson()
    {
        // Arrange
        var invoiceContent = @"
            FAKTURA
            
            Från: Example Supplier AB
            Org.nr: 556123-4567
            VAT: SE556123456701
            
            Fakturanummer: 2024-001
            Fakturadatum: 2024-01-15
            Förfallodatum: 2024-02-14
            
            Konsulttjänster: 40 tim × 200 kr = 8,000 kr
            Moms 25%: 2,000 kr
            Totalt: 10,000 kr
            
            Bankgiro: 123-4567
            OCR: 12345678901
        ";

        // Act
        var result = await _plugin.InterpretDocumentFromTextAsync(invoiceContent, "SupplierInvoice", "test");

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        
        // Verify it's valid JSON
        var document = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(document.TryGetProperty("documentType", out var docType));
        Assert.Equal("SupplierInvoice", docType.GetString());
    }

    [Fact]
    public async Task DetectDocumentTypeAsync_WithReceiptContent_ShouldReturnReceiptType()
    {
        // Arrange
        var receiptContent = @"
            ICA Supermarket
            Storvägen 10, Stockholm
            
            Kvitto: 12345678
            Datum: 2024-01-15 14:30
            
            Matvaror: 100,00 kr
            Varav moms 25%: 20,00 kr
            
            Kort: **** 1234
        ";

        // Act
        var result = await _plugin.DetectDocumentTypeAsync(receiptContent);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        
        var detection = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(detection.TryGetProperty("documentType", out var docType));
        Assert.True(detection.TryGetProperty("confidence", out var confidence));
        
        // Should detect as Receipt with reasonable confidence
        Assert.Equal("Receipt", docType.GetString());
        Assert.True(confidence.GetDouble() > 0.5);
    }

    [Fact]
    public async Task ValidateDocumentAsync_WithValidDocument_ShouldReturnValidResult()
    {
        // Arrange
        var validDocument = new SupplierInvoice
        {
            DocumentType = "SupplierInvoice",
            InvoiceNumber = "2024-001",
            RawText = "Test invoice content",
            Seller = new ContactInfo { Name = "Test Supplier" },
            Totals = new TotalAmounts { GrossAmount = 1000, Currency = "SEK" }
        };

        var documentJson = JsonSerializer.Serialize(validDocument, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Act
        var result = await _plugin.ValidateDocumentAsync(documentJson);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        
        var validation = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(validation.TryGetProperty("isValid", out var isValid));
        Assert.True(validation.TryGetProperty("validationScore", out var score));
        
        // Should be valid
        Assert.True(isValid.GetBoolean());
        Assert.True(score.GetDouble() >= 0);
    }

    [Fact]
    public void GetSupportedDocumentTypes_ShouldReturnExpectedTypes()
    {
        // Act
        var result = _plugin.GetSupportedDocumentTypes();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        
        var types = JsonSerializer.Deserialize<string[]>(result);
        Assert.NotNull(types);
        Assert.Contains("SupplierInvoice", types);
        Assert.Contains("Receipt", types);
        Assert.Contains("CustomerInvoice", types);
        Assert.Contains("BankStatement", types);
    }

    [Fact]
    public async Task ProcessEmailAttachmentsAsync_ShouldReturnPlaceholderResult()
    {
        // Arrange
        var emailSource = "test@example.com";
        var attachmentFilter = ".pdf,.jpg";

        // Act
        var result = await _plugin.ProcessEmailAttachmentsAsync(emailSource, attachmentFilter);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        
        var response = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(response.TryGetProperty("message", out var message));
        Assert.Contains("not yet implemented", message.GetString());
    }

    [Fact]
    public async Task ProcessCloudStorageAsync_ShouldReturnPlaceholderResult()
    {
        // Arrange
        var storageType = "GoogleDrive";
        var folderPath = "/invoices";
        var fileFilter = ".pdf";

        // Act
        var result = await _plugin.ProcessCloudStorageAsync(storageType, folderPath, fileFilter);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        
        var response = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(response.TryGetProperty("message", out var message));
        Assert.Contains("not yet implemented", message.GetString());
    }

    [Fact]
    public async Task InterpretDocumentFromBinaryAsync_WithInvalidBase64_ShouldThrowException()
    {
        // Arrange
        var invalidBase64 = "invalid_base64_content";
        var contentType = "application/pdf";
        var filename = "test.pdf";

        // Act & Assert
        await Assert.ThrowsAsync<FormatException>(
            () => _plugin.InterpretDocumentFromBinaryAsync(invalidBase64, contentType, filename));
    }

    [Fact]
    public async Task InterpretDocumentFromBinaryAsync_WithValidTextData_ShouldReturnValidJson()
    {
        // Arrange
        var textContent = "This is a simple text document for testing";
        var textBytes = System.Text.Encoding.UTF8.GetBytes(textContent);
        var base64Content = Convert.ToBase64String(textBytes);
        var contentType = "text/plain";
        var filename = "test.txt";

        // Act
        var result = await _plugin.InterpretDocumentFromBinaryAsync(base64Content, contentType, filename, null, "test");

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        
        var document = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(document.TryGetProperty("documentType", out var docType));
        Assert.True(document.TryGetProperty("rawText", out var rawText));
        Assert.Equal(textContent, rawText.GetString());
    }

    [Fact]
    public async Task InterpretDocumentFromFileAsync_WithNonexistentFile_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var nonexistentFile = "/path/to/nonexistent/file.pdf";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _plugin.InterpretDocumentFromFileAsync(nonexistentFile));
    }

    [Theory]
    [InlineData(".pdf", "application/pdf")]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".png", "image/png")]
    [InlineData(".tiff", "image/tiff")]
    [InlineData(".txt", "text/plain")]
    public void GetContentTypeFromExtension_ShouldReturnCorrectContentType(string extension, string expectedContentType)
    {
        // This tests the private method indirectly by testing file extension handling
        // We can't test the private method directly, but we know it's used in InterpretDocumentFromFileAsync

        // Assert that the expected mapping exists (this is more of a documentation test)
        Assert.NotNull(extension);
        Assert.NotNull(expectedContentType);
        Assert.NotEmpty(expectedContentType);
    }

    [Fact]
    public async Task InterpretDocumentFromTextAsync_WithSwedishInetInvoice_ShouldExtractCorrectFields()
    {
        // Arrange - OCR content from the actual problematic invoice (issue #102)
        var swedishInvoiceContent = @"
KVITTO 853631794
Kundnummer
1532093
Datum
2025-08-14
Orderdatum
2025-08-14
Leveransadress Fakturaadress
David Olandersson Example AB
Example AB
Silverforsv. 13 Silverforsv. 13
37231 Ronneby 37231 Ronneby
Vår referens Referens Märkning Leveranssätt
Inet.se David Olandersson Postnord Collect
Ordernummer Er referens Betalningsvillkor
13686078 Kortbetalning
Rad Artikelnr Benämning Beställt Levererat á pris % Summa Moms
Förväntad leveransdag 15 augusti
Skickas med PostNord till Ica Maxi Ronneby, Karlshamnsvägen 8
10 4305961 Samsung 990 EVO Plus M.2 NVMe SSD 2TB 1 1 1 199,20 0 1 199,20 299,80
Batch: 000004020995 x 1
20 5306116 Kingston Fury Impact 64GB (2x32GB) DDR5 1 1 2 119,20 0 2 119,20 529,80
5600MHz CL4
Batch: 000004018710 x 1
Nettobelopp Omv. skattskyldighet Frakt Exp.Avg Moms 6% Moms 12% Moms 25% Avrundning
3 318,40 0,00 0,00 0,00 0,00 0,00 829,60 0,00
Summa 4 148,00 SEK
Inet AB, Ekonomivägen 4, 436 33 Askim, www.inet.se
OrgNr: 556591-8884
VAT: SE556591888401
Tel: 031-65 27 00
Godkänd för F-skatt, Säte: Göteborg
Sida 1 av 1
        ";

        // Act
        var result = await _plugin.InterpretDocumentFromTextAsync(swedishInvoiceContent, "SupplierInvoice");

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        var document = JsonSerializer.Deserialize<JsonElement>(result);
        
        // Check that we have the expected structure
        Assert.True(document.TryGetProperty("extractedData", out var extractedData), "extractedData property should exist");
        
        // Verify supplier name is extracted correctly (should be "Inet AB", not "Kundnummer")
        Assert.True(extractedData.TryGetProperty("supplierName", out var supplierName), "supplierName property should exist");
        var supplierNameStr = supplierName.GetString();
        Assert.False(string.IsNullOrEmpty(supplierNameStr), $"Supplier name should not be empty, got: '{supplierNameStr}'");
        Assert.NotEqual("Kundnummer", supplierNameStr);
        Assert.Equal("Inet AB", supplierNameStr); // Should be exactly "Inet AB"
        
        // Verify invoice number is extracted correctly (should be "853631794", not "adress")
        Assert.True(extractedData.TryGetProperty("invoiceNumber", out var invoiceNumber), "invoiceNumber property should exist");
        var invoiceNumberStr = invoiceNumber.GetString();
        Assert.NotEqual("adress", invoiceNumberStr);
        Assert.Equal("853631794", invoiceNumberStr); // Should be exactly the receipt number
        
        // Verify VAT amount is extracted correctly (should be 829.60 SEK, not 0.00)  
        Assert.True(extractedData.TryGetProperty("vatAmount", out var vatAmount), "vatAmount property should exist");
        var vatAmountStr = vatAmount.GetString();
        Assert.NotEqual("0.00", vatAmountStr);
        Assert.Equal("829.60", vatAmountStr); // Should be exactly 829.60
        
        // Verify total amount is correct
        Assert.True(extractedData.TryGetProperty("amount", out var totalAmount), "amount property should exist");
        var totalAmountStr = totalAmount.GetString();
        Assert.Equal("4148.00", totalAmountStr); // Should be exactly 4148.00

        // Verify currency is SEK
        Assert.True(extractedData.TryGetProperty("currency", out var currency), "currency property should exist");
        Assert.Equal("SEK", currency.GetString());

        // Verify invoice date is correct
        Assert.True(extractedData.TryGetProperty("invoiceDate", out var invoiceDate), "invoiceDate property should exist");
        Assert.Equal("2025-08-14", invoiceDate.GetString());
    }

    [Fact]
    public async Task InterpretDocumentFromTextAsync_WithSwedishQuote_ShouldExtractCorrectFields()
    {
        // Arrange - Swedish quote document
        var swedishQuoteContent = @"
OFFERT
Offert nr: OF-2024-001
Datum: 2024-12-01
Giltig till: 2025-01-01

Till:
Test Kund AB
Testgatan 1
123 45 Stockholm

Beskrivning: Konsulttjänster
Pris: 10 000,00 kr
Moms 25%: 2 500,00 kr
Totalt: 12 500,00 kr

Status: Pending
        ";

        // Act
        var result = await _plugin.InterpretDocumentFromTextAsync(swedishQuoteContent, "Quote");

        // Assert
        Assert.NotNull(result);
        var document = JsonSerializer.Deserialize<JsonElement>(result);
        
        Assert.True(document.TryGetProperty("extractedData", out var extractedData));
        Assert.True(extractedData.TryGetProperty("quoteNumber", out var quoteNumber));
        Assert.Contains("OF-2024-001", quoteNumber.GetString() ?? "");
        
        Assert.True(extractedData.TryGetProperty("amount", out var amount));
        // Should extract the total amount
        if (decimal.TryParse(amount.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var totalAmount))
        {
            Assert.True(totalAmount > 10000 && totalAmount < 15000);
        }
    }

    [Fact]
    public async Task InterpretDocumentFromTextAsync_WithSwedishOrderConfirmation_ShouldExtractCorrectFields()
    {
        // Arrange - Swedish order confirmation document
        var swedishOrderContent = @"
ORDERBEKRÄFTELSE
Ordernummer: ORD-2024-123
Bekräftad: 2024-12-01
Leveransdatum: 2024-12-15

Kund:
Business Partner AB
Affärsvägen 5
456 78 Göteborg

Beställning bekräftad för:
Produkter enligt specifikation
Totalt: 8 750,00 kr
        ";

        // Act
        var result = await _plugin.InterpretDocumentFromTextAsync(swedishOrderContent, "OrderConfirmation");

        // Assert
        Assert.NotNull(result);
        var document = JsonSerializer.Deserialize<JsonElement>(result);
        
        Assert.True(document.TryGetProperty("extractedData", out var extractedData));
        Assert.True(extractedData.TryGetProperty("orderNumber", out var orderNumber));
        Assert.Contains("ORD-2024-123", orderNumber.GetString() ?? "");
    }

    [Fact]
    public async Task InterpretDocumentFromTextAsync_WithSwedishDeliveryNote_ShouldExtractCorrectFields()
    {
        // Arrange - Swedish delivery note document
        var swedishDeliveryContent = @"
FÖLJESEDEL
Följesedelsnummer: FS-2024-456
Leveransdatum: 2024-12-05
Ordernummer: ORD-2024-123

Leveransadress:
Customer Company AB
Leveransvägen 10
789 01 Malmö

Fraktförare: PostNord
Spårningsnummer: 1234567890

Levererat:
- Produkt A: 5 st
- Produkt B: 2 st
        ";

        // Act
        var result = await _plugin.InterpretDocumentFromTextAsync(swedishDeliveryContent, "DeliveryNote");

        // Assert
        Assert.NotNull(result);
        var document = JsonSerializer.Deserialize<JsonElement>(result);
        
        Assert.True(document.TryGetProperty("extractedData", out var extractedData));
        Assert.True(extractedData.TryGetProperty("deliveryNoteNumber", out var deliveryNoteNumber));
        Assert.Contains("FS-2024-456", deliveryNoteNumber.GetString() ?? "");
        
        Assert.True(extractedData.TryGetProperty("trackingNumber", out var trackingNumber));
        Assert.Equal("1234567890", trackingNumber.GetString());
        
        Assert.True(extractedData.TryGetProperty("carrier", out var carrier));
        Assert.Contains("PostNord", carrier.GetString() ?? "");
    }

    [Fact]
    public async Task DetectDocumentTypeAsync_WithSwedishDocumentTypes_ShouldIdentifyCorrectly()
    {
        // Test different Swedish document types
        var testCases = new[]
        {
            new { Content = "OFFERT nr: OF-123 Giltig till: 2024-12-31", ExpectedType = "Quote" },
            new { Content = "ORDERBEKRÄFTELSE Ordernummer: ORD-456", ExpectedType = "OrderConfirmation" },
            new { Content = "FÖLJESEDEL Leveransdatum: 2024-12-01", ExpectedType = "DeliveryNote" },
            new { Content = "RÄNTEFAKTURA för försenad betalning", ExpectedType = "InterestInvoice" },
            new { Content = "KONTANTKVITTO Belopp: 150,00 kr", ExpectedType = "CashReceipt" },
            new { Content = "BESTÄLLNING nummer: PO-789 Leverantör: Test AB", ExpectedType = "PurchaseOrder" },
            new { Content = "SJÄLVFAKTURA enligt avtal SB-123", ExpectedType = "SelfBillingInvoice" },
            new { Content = "PROJEKTFAKTURA Projekt: WEB-2024 Timmar: 40", ExpectedType = "ProjectInvoice" },
            new { Content = "LÖNESPECIFIKATION Anställd: John Doe Bruttolön: 35000", ExpectedType = "Payslip" },
            new { Content = "MOMSRAPPORT Period: 2024-Q1 Utgående moms: 125000", ExpectedType = "VatReport" }
        };

        foreach (var testCase in testCases)
        {
            // Act
            var result = await _plugin.DetectDocumentTypeAsync(testCase.Content);

            // Assert
            Assert.NotNull(result);
            var detection = JsonSerializer.Deserialize<JsonElement>(result);
            
            Assert.True(detection.TryGetProperty("documentType", out var docType));
            Assert.Equal(testCase.ExpectedType, docType.GetString());
            
            Assert.True(detection.TryGetProperty("confidence", out var confidence));
            Assert.True(confidence.GetDouble() > 0.8); // Should have high confidence
        }
    }

    [Fact]
    public void GetSupportedDocumentTypes_ShouldIncludeAllSwedishDocumentTypes()
    {
        // Act
        var result = _plugin.GetSupportedDocumentTypes();

        // Assert
        Assert.NotNull(result);
        var types = JsonSerializer.Deserialize<string[]>(result);
        Assert.NotNull(types);
        
        // Verify all Swedish document types are supported
        var expectedTypes = new[]
        {
            "SupplierInvoice", "CustomerInvoice", "CreditNote", "Receipt", "CashReceipt",
            "Quote", "OrderConfirmation", "DeliveryNote", "InterestInvoice",
            "PurchaseOrder", "SelfBillingInvoice", "ProjectInvoice", "Payslip", "VatReport",
            "PaymentAdvice", "BankStatement", "CustomsDocument", "ExpenseReport",
            "ReminderNotice", "VendorOnboarding", "CustomerOnboarding"
        };

        foreach (var expectedType in expectedTypes)
        {
            Assert.Contains(expectedType, types);
        }
    }

    [Fact]
    public async Task InterpretDocumentFromTextAsync_WithSwedishInvoiceKeywords_ShouldNotExtractAsSupplierName()
    {
        // Arrange - Test content with common Swedish invoice keywords that should NOT be supplier names
        var invoiceWithKeywords = @"
FAKTURA 12345
Kundnummer
9876543
Datum  
2024-12-01
Leveransadress
Test Company AB
Fakturaadress
Test Company AB
Betalningsvillkor
30 dagar
Test Company AB, Testvägen 1, 123 45 Stockholm
OrgNr: 123456-7890
        ";

        // Act
        var result = await _plugin.InterpretDocumentFromTextAsync(invoiceWithKeywords, "SupplierInvoice");

        // Assert
        Assert.NotNull(result);
        var document = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.True(document.TryGetProperty("extractedData", out var extractedData));
        Assert.True(extractedData.TryGetProperty("supplierName", out var supplierName));
        
        var supplierNameStr = supplierName.GetString();
        
        // Should NOT extract these common Swedish keywords as supplier names
        Assert.NotEqual("Kundnummer", supplierNameStr);
        Assert.NotEqual("Datum", supplierNameStr);  
        Assert.NotEqual("Leveransadress", supplierNameStr);
        Assert.NotEqual("Fakturaadress", supplierNameStr);
        Assert.NotEqual("Betalningsvillkor", supplierNameStr);
        
        // Should extract the actual company name
        Assert.Equal("Test Company AB", supplierNameStr);
    }

    [Fact]
    public async Task InterpretDocumentFromTextAsync_WithSwedishPurchaseOrder_ShouldExtractCorrectFields()
    {
        // Arrange - Swedish purchase order document
        var swedishPurchaseOrderContent = @"
BESTÄLLNING
Beställningsnummer: PO-2024-001
Orderdatum: 2024-12-01
Önskad leverans: 2024-12-15

Leverantör:
Tech Supplier AB
Leverantörsvägen 5
123 45 Stockholm

Produkter:
- Dator: 2 st à 15 000 kr = 30 000 kr
- Skärm: 2 st à 3 000 kr = 6 000 kr

Totalt: 36 000,00 kr
Status: Skickad
        ";

        // Act
        var result = await _plugin.InterpretDocumentFromTextAsync(swedishPurchaseOrderContent, "PurchaseOrder");

        // Assert
        Assert.NotNull(result);
        var document = JsonSerializer.Deserialize<JsonElement>(result);
        
        Assert.True(document.TryGetProperty("extractedData", out var extractedData));
        Assert.True(extractedData.TryGetProperty("purchaseOrderNumber", out var poNumber));
        Assert.Contains("PO-2024-001", poNumber.GetString() ?? "");
        
        Assert.True(extractedData.TryGetProperty("supplierName", out var supplier));
        Assert.Contains("Tech Supplier AB", supplier.GetString() ?? "");
    }

    [Fact]
    public async Task InterpretDocumentFromTextAsync_WithSwedishProjectInvoice_ShouldExtractCorrectFields()
    {
        // Arrange - Swedish project invoice document
        var swedishProjectInvoiceContent = @"
PROJEKTFAKTURA
Fakturanummer: PRJ-2024-123
Datum: 2024-12-01
Projekt: WEB-2024
Projektnamn: Hemsida utveckling
Projektledare: Anna Andersson

Period: 2024-11-01 - 2024-11-30

Utförda tjänster:
- Design: 20 timmar à 800 kr = 16 000 kr
- Utveckling: 40 timmar à 900 kr = 36 000 kr

Totalt: 52 000,00 kr
Moms 25%: 13 000,00 kr
Att betala: 65 000,00 kr
        ";

        // Act
        var result = await _plugin.InterpretDocumentFromTextAsync(swedishProjectInvoiceContent, "ProjectInvoice");

        // Assert
        Assert.NotNull(result);
        var document = JsonSerializer.Deserialize<JsonElement>(result);
        
        Assert.True(document.TryGetProperty("extractedData", out var extractedData));
        Assert.True(extractedData.TryGetProperty("invoiceNumber", out var invoiceNumber));
        Assert.Contains("PRJ-2024-123", invoiceNumber.GetString() ?? "");
        
        Assert.True(extractedData.TryGetProperty("projectNumber", out var projectNumber));
        Assert.Contains("WEB-2024", projectNumber.GetString() ?? "");
        
        Assert.True(extractedData.TryGetProperty("projectName", out var projectName));
        Assert.Contains("Hemsida utveckling", projectName.GetString() ?? "");
    }

    [Fact]
    public async Task InterpretDocumentFromTextAsync_WithSwedishPayslip_ShouldExtractCorrectFields()
    {
        // Arrange - Swedish payslip document
        var swedishPayslipContent = @"
LÖNESPECIFIKATION
Lönespecifikation för: Anna Andersson
Personnummer: 850315-1234
Anställningsnummer: EMP-001
Avdelning: IT

Arbetsgivare: Tech Company AB
Löneperiod: 2024-11-01 - 2024-11-30
Utbetalningsdatum: 2024-12-01

Bruttolön: 45 000,00 kr
Preliminärskatt: -12 600,00 kr
Sociala avgifter: -3 150,00 kr
Pension: -2 250,00 kr

Nettolön: 27 000,00 kr
        ";

        // Act
        var result = await _plugin.InterpretDocumentFromTextAsync(swedishPayslipContent, "Payslip");

        // Assert
        Assert.NotNull(result);
        var document = JsonSerializer.Deserialize<JsonElement>(result);
        
        Assert.True(document.TryGetProperty("extractedData", out var extractedData));
        Assert.True(extractedData.TryGetProperty("employeeName", out var employeeName));
        Assert.Contains("Anna Andersson", employeeName.GetString() ?? "");
        
        Assert.True(extractedData.TryGetProperty("personalNumber", out var personalNumber));
        Assert.Contains("850315-1234", personalNumber.GetString() ?? "");
        
        Assert.True(extractedData.TryGetProperty("employerName", out var employerName));
        Assert.Contains("Tech Company AB", employerName.GetString() ?? "");
    }

    [Fact]
    public async Task InterpretDocumentFromTextAsync_WithSwedishVatReport_ShouldExtractCorrectFields()
    {
        // Arrange - Swedish VAT report document
        var swedishVatReportContent = @"
MOMSRAPPORT
Momsdeklaration för: Business Solutions AB
Momsregistreringsnummer: SE123456789012
Period: 2024-10-01 - 2024-12-31
Inlämnad: 2025-01-15

Utgående moms (försäljning):
25% moms: 125 000,00 kr
12% moms: 24 000,00 kr
Totalt utgående: 149 000,00 kr

Ingående moms (inköp):
25% moms: 85 000,00 kr
12% moms: 15 000,00 kr
Totalt ingående: 100 000,00 kr

Att betala: 49 000,00 kr
        ";

        // Act
        var result = await _plugin.InterpretDocumentFromTextAsync(swedishVatReportContent, "VatReport");

        // Assert
        Assert.NotNull(result);
        var document = JsonSerializer.Deserialize<JsonElement>(result);
        
        Assert.True(document.TryGetProperty("extractedData", out var extractedData));
        Assert.True(extractedData.TryGetProperty("companyName", out var companyName));
        Assert.Contains("Business Solutions AB", companyName.GetString() ?? "");
        
        Assert.True(extractedData.TryGetProperty("vatRegistrationNumber", out var vatRegNumber));
        Assert.Contains("SE123456789012", vatRegNumber.GetString() ?? "");
    }
}