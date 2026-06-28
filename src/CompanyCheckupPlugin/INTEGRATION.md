# CompanyCheckupPlugin Integration Guide

## Overview

The CompanyCheckupPlugin provides automated company information lookup using OpenCorporates and VIES APIs. It's designed to integrate seamlessly with the FortnoxPlugin to enable automatic customer creation from company names or organization numbers.

## Configuration

No specific configuration is required as the plugin uses free public APIs:

```json
{
  // No additional configuration needed for CompanyCheckupPlugin
  // OpenCorporates and VIES APIs are free for basic usage
}
```

## Agent Integration

The plugin automatically registers as a KernelFunction that can be called by the AI agent:

### Function Details
- **Name**: `LookupCompanyAsync`
- **Description**: "Slår upp ett företag baserat på namn, organisationsnummer eller VAT-nummer och returnerar strukturerad information för att skapa kund i Fortnox."
- **Parameter**: `companyIdentifier` - Company name, organization number, or VAT number

### Example Agent Conversation

**User**: "Kolla upp företaget Volvo och skapa sedan dem som kund i Fortnox"

**Agent Flow**:
1. Agent calls `CompanyCheckupPlugin.LookupCompanyAsync("Volvo")`
2. Plugin returns structured JSON with company details and Fortnox draft
3. Agent extracts the `drafts.fortnoxSupplier` data
4. Agent calls `FortnoxPlugin.CreateCustomerAsync()` with the extracted data
5. Agent reports success to user

## Integration with FortnoxPlugin

The plugin output is specifically designed to work with FortnoxPlugin:

```csharp
// CompanyCheckupPlugin output includes a ready-to-use draft:
{
  "drafts": {
    "fortnoxSupplier": {
      "Name": "Company Name",
      "OrganisationNumber": "556123-4567",
      "VATNumber": "SE556123456701",
      "Address1": "Street Address",
      "ZipCode": "12345",
      "City": "City",
      "CountryCode": "SE"
    }
  }
}
```

This draft can be directly mapped to FortnoxPlugin's Customer model with minimal additional data:

```csharp
var customer = new Customer
{
    Name = draft.Name,
    OrganisationNumber = draft.OrganisationNumber,
    VATNumber = draft.VATNumber,
    Address1 = draft.Address1,
    ZipCode = draft.ZipCode,
    City = draft.City,
    CountryCode = draft.CountryCode,
    // Add required fields
    Active = true,
    Type = "COMPANY"
};
```

## Error Handling

The plugin handles various error scenarios:

1. **Network errors**: Returns descriptive error messages
2. **Invalid input**: Handles malformed organization numbers gracefully  
3. **API failures**: Fallback mechanisms (VIES REST → SOAP)
4. **No results found**: Clear feedback to user

## Data Sources

### OpenCorporates
- **Purpose**: Company information lookup
- **Coverage**: Global, with good Swedish coverage
- **Cost**: Free for basic usage
- **Rate limits**: Apply for high-volume usage

### VIES (VAT Information Exchange System)
- **Purpose**: VAT number validation
- **Coverage**: EU countries
- **Cost**: Free
- **Rate limits**: Reasonable usage accepted

## Plugin Discovery

The plugin uses the `[PluginDiscoverable]` attribute for automatic discovery:

```csharp
[PluginDiscoverable("CompanyCheckup", "Company information lookup using OpenCorporates and VIES", 
    Category = "Business", Priority = 60)]
```

This allows the Agent system to automatically load and register the plugin without manual configuration.

## Testing

Run tests to verify functionality:

```bash
dotnet test tests/CompanyCheckupPlugin.Tests/
```

The tests verify:
- Plugin factory creation
- VAT number derivation logic
- Address parsing functionality
- Error handling scenarios

## Monitoring and Logging

The plugin includes comprehensive logging:

```csharp
_logger.LogInformation($"Looking up company: {companyIdentifier}");
_logger.LogInformation($"Found company in OpenCorporates: {companyName}");
_logger.LogInformation($"VAT validated in VIES: {vatNumber}");
```

Monitor these logs to track usage and identify any issues with external APIs.