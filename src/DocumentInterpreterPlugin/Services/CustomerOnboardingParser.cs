using System.Text.RegularExpressions;
using DocumentInterpreter.Models;
using Microsoft.Extensions.Logging;

namespace DocumentInterpreter.Services;

/// <summary>
/// Specialized parser for customer onboarding documents
/// </summary>
public class CustomerOnboardingParser
{
    private readonly ILogger _logger;

    public CustomerOnboardingParser(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Parses customer onboarding text and extracts structured data
    /// </summary>
    /// <param name="content">Raw text content of the document</param>
    /// <returns>Parsed CustomerOnboarding document</returns>
    public CustomerOnboarding ParseCustomerOnboardingDocument(string content)
    {
        var document = new CustomerOnboarding();
        
        try
        {
            // Parse required fields
            document.Customer.Name = ExtractField(content, "Namn", required: true) ?? string.Empty;
            document.Customer.Address = ExtractField(content, "Adress", required: true) ?? string.Empty;
            document.Customer.City = ExtractField(content, "Stad", required: true) ?? string.Empty;
            document.Customer.PostalCode = ExtractField(content, "Postnummer", required: true) ?? string.Empty;
            document.Customer.CountryCode = ExtractField(content, "Landskod", required: true) ?? string.Empty;
            document.Customer.Email = ExtractField(content, "E-post", required: true) ?? string.Empty;
            document.Customer.Type = ExtractField(content, "Typ", required: true) ?? string.Empty;

            // Parse optional fields
            document.Customer.PhoneNumber = ExtractField(content, "Telefonnummer");
            document.Customer.OrganizationNumber = ExtractField(content, "Organisationsnummer");
            document.Customer.Country = ExtractField(content, "Land");
            document.Customer.Currency = ExtractField(content, "Valuta");
            document.Customer.Website = ExtractField(content, "Webbplats");
            document.Customer.Comments = ExtractField(content, "Kommentarer");

            // Parse business terms
            document.Preferences.PaymentTerms = ExtractField(content, "Betalningsvillkor");
            document.Preferences.DeliveryTerms = ExtractField(content, "Leveransvillkor");

            // Parse delivery address if present
            var deliveryAddress = ExtractField(content, "Leveransadress");
            if (!string.IsNullOrEmpty(deliveryAddress))
            {
                document.DeliveryAddress = new DeliveryAddressInfo
                {
                    Address = deliveryAddress,
                    // Try to extract delivery city and postal code if they exist as separate fields
                    City = ExtractField(content, "Leveransstad") ?? document.Customer.City,
                    PostalCode = ExtractField(content, "Leveranspostnummer") ?? document.Customer.PostalCode,
                    CountryCode = ExtractField(content, "Leveranslandskod") ?? document.Customer.CountryCode,
                    Country = ExtractField(content, "Leveransland") ?? document.Customer.Country
                };
            }

            // Validate required fields
            ValidateRequiredFields(document);

            _logger.LogInformation("Successfully parsed customer onboarding document for: {CustomerName}", 
                document.Customer.Name);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse customer onboarding document");
            throw;
        }

        return document;
    }

    /// <summary>
    /// Extracts a field value from the document content
    /// </summary>
    /// <param name="content">Document content</param>
    /// <param name="fieldName">Name of the field to extract</param>
    /// <param name="required">Whether the field is required</param>
    /// <returns>Extracted field value or null if not found</returns>
    private string? ExtractField(string content, string fieldName, bool required = false)
    {
        try
        {
            // Create regex pattern to match "FieldName: Value" format
            // This handles various formats like "Namn:", "Namn :", "Namn  :", etc.
            var pattern = $@"{Regex.Escape(fieldName)}\s*:\s*(.+?)(?:\r?\n|$)";
            var match = Regex.Match(content, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);

            if (match.Success)
            {
                var value = match.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(value))
                {
                    _logger.LogDebug("Extracted {FieldName}: {Value}", fieldName, value);
                    return value;
                }
            }

            if (required)
            {
                _logger.LogWarning("Required field '{FieldName}' not found in document", fieldName);
                throw new InvalidOperationException($"Required field '{fieldName}' not found in document");
            }

            _logger.LogDebug("Optional field '{FieldName}' not found in document", fieldName);
            return null;
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            _logger.LogError(ex, "Error extracting field '{FieldName}'", fieldName);
            if (required)
                throw;
            return null;
        }
    }

    /// <summary>
    /// Validates that all required fields are present and valid
    /// </summary>
    /// <param name="document">Document to validate</param>
    private void ValidateRequiredFields(CustomerOnboarding document)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(document.Customer.Name))
            errors.Add("Customer name is required");

        if (string.IsNullOrEmpty(document.Customer.Address))
            errors.Add("Customer address is required");

        if (string.IsNullOrEmpty(document.Customer.City))
            errors.Add("Customer city is required");

        if (string.IsNullOrEmpty(document.Customer.PostalCode))
            errors.Add("Customer postal code is required");

        if (string.IsNullOrEmpty(document.Customer.CountryCode))
            errors.Add("Customer country code is required");

        if (string.IsNullOrEmpty(document.Customer.Email))
            errors.Add("Customer email is required");

        if (string.IsNullOrEmpty(document.Customer.Type))
            errors.Add("Customer type is required");

        // Validate customer type
        if (!string.IsNullOrEmpty(document.Customer.Type) && 
            !document.Customer.Type.Equals("COMPANY", StringComparison.OrdinalIgnoreCase) &&
            !document.Customer.Type.Equals("PRIVATE", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Customer type must be either 'COMPANY' or 'PRIVATE'");
        }

        // Validate email format
        if (!string.IsNullOrEmpty(document.Customer.Email) && !IsValidEmail(document.Customer.Email))
        {
            errors.Add("Invalid email format");
        }

        // Validate country code format (should be 2 characters)
        if (!string.IsNullOrEmpty(document.Customer.CountryCode) && document.Customer.CountryCode.Length != 2)
        {
            errors.Add("Country code should be 2 characters (ISO 3166-1 alpha-2)");
        }

        if (errors.Any())
        {
            var errorMessage = "Validation failed: " + string.Join(", ", errors);
            _logger.LogError("Customer onboarding validation failed: {Errors}", errorMessage);
            throw new InvalidOperationException(errorMessage);
        }
    }

    /// <summary>
    /// Validates email format using a simple regex
    /// </summary>
    /// <param name="email">Email to validate</param>
    /// <returns>True if email format is valid</returns>
    private static bool IsValidEmail(string email)
    {
        try
        {
            var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
            return emailRegex.IsMatch(email);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Converts CustomerOnboarding to Fortnox-compatible CustomerRequest format
    /// </summary>
    /// <param name="customerOnboarding">Parsed customer onboarding document</param>
    /// <returns>JSON string compatible with Fortnox API</returns>
    public string ConvertToFortnoxCustomerJson(CustomerOnboarding customerOnboarding)
    {
        try
        {
            var customer = customerOnboarding.Customer;
            var preferences = customerOnboarding.Preferences;
            var deliveryAddress = customerOnboarding.DeliveryAddress;

            // Create Fortnox customer object structure
            var fortnoxCustomer = new
            {
                Customer = new
                {
                    Active = preferences.Active,
                    Address1 = customer.Address,
                    Address2 = (string?)null, // Not provided in the input format
                    City = customer.City,
                    Comments = customer.Comments,
                    Country = customer.Country,
                    CountryCode = customer.CountryCode.ToUpperInvariant(),
                    Currency = customer.Currency ?? "SEK",
                    Email = customer.Email,
                    Name = customer.Name,
                    OrganisationNumber = customer.OrganizationNumber,
                    Phone1 = customer.PhoneNumber,
                    Type = customer.Type.ToUpperInvariant(),
                    Url = customer.Website,
                    ZipCode = customer.PostalCode,
                    TermsOfPayment = preferences.PaymentTerms,
                    TermsOfDelivery = preferences.DeliveryTerms,
                    
                    // Delivery address fields if provided
                    DeliveryAddress1 = deliveryAddress?.Address,
                    DeliveryCity = deliveryAddress?.City,
                    DeliveryCountry = deliveryAddress?.Country,
                    DeliveryCountryCode = deliveryAddress?.CountryCode?.ToUpperInvariant(),
                    DeliveryZipCode = deliveryAddress?.PostalCode
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(fortnoxCustomer, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            _logger.LogInformation("Successfully converted customer onboarding to Fortnox format for: {CustomerName}", 
                customer.Name);

            return json;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert customer onboarding to Fortnox format");
            throw;
        }
    }
}
