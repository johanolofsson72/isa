using System.ComponentModel;
using System.Text.Json.Serialization;

namespace CompanyCheckupPlugin;

/// <summary>
/// Company checkup result containing all gathered information
/// </summary>
public class CompanyCheckupResult
{
    [Description("Match confidence and source information")]
    public MatchInfo Match { get; set; } = new();

    [Description("Company profile with all gathered information")]
    public CompanyProfile Profile { get; set; } = new();

    [Description("Ready-to-use drafts for external systems")]
    public DraftObjects Drafts { get; set; } = new();
}

/// <summary>
/// Match confidence and source information
/// </summary>
public class MatchInfo
{
    [Description("Confidence score (0.0 - 1.0)")]
    public double Confidence { get; set; }

    [Description("Sources used for information gathering")]
    public string[] Sources { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Complete company profile
/// </summary>
public class CompanyProfile
{
    [Description("Legal company name")]
    public string? LegalName { get; set; }

    [Description("Registration information")]
    public RegistrationInfo Registration { get; set; } = new();

    [Description("Company addresses")]
    public AddressInfo Addresses { get; set; } = new();

    [Description("Contact information")]
    public ContactInfo Contacts { get; set; } = new();

    [Description("Metadata about data sources and request")]
    public MetadataInfo Metadata { get; set; } = new();
}

/// <summary>
/// Company registration information
/// </summary>
public class RegistrationInfo
{
    [Description("Organization number")]
    public string? OrgNumber { get; set; }

    [Description("VAT number")]
    public string? VatNumber { get; set; }

    [Description("Country code")]
    public string? CountryCode { get; set; }

    [Description("Company status")]
    public string? Status { get; set; }
}

/// <summary>
/// Address information
/// </summary>
public class AddressInfo
{
    [Description("Registered address")]
    public Address? Registered { get; set; }
}

/// <summary>
/// Single address
/// </summary>
public class Address
{
    [Description("Address line 1")]
    public string? Line1 { get; set; }

    [Description("Postal code")]
    public string? Zip { get; set; }

    [Description("City")]
    public string? City { get; set; }

    [Description("Country code")]
    public string? CountryCode { get; set; }
}

/// <summary>
/// Contact information
/// </summary>
public class ContactInfo
{
    [Description("Email address")]
    public string? Email { get; set; }

    [Description("Phone number")]
    public string? Phone { get; set; }
}

/// <summary>
/// Metadata about the lookup
/// </summary>
public class MetadataInfo
{
    [Description("Sources used")]
    public string[] Sources { get; set; } = Array.Empty<string>();

    [Description("Request identifier for audit trail")]
    public string? RequestIdentifier { get; set; }

    [Description("Request timestamp")]
    public DateTime RequestTimestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Draft objects for external systems
/// </summary>
public class DraftObjects
{
    [Description("Fortnox supplier/customer draft")]
    public FortnoxSupplierDraft? FortnoxSupplier { get; set; }
}

/// <summary>
/// Fortnox supplier draft compatible with FortnoxPlugin
/// </summary>
public class FortnoxSupplierDraft
{
    [Description("Company name (obligatory)")]
    public string? Name { get; set; }

    [Description("Organization number")]
    public string? OrganisationNumber { get; set; }

    [Description("VAT number")]
    public string? VATNumber { get; set; }

    [Description("Address line 1 (obligatory)")]
    public string? Address1 { get; set; }

    [Description("Postal code (obligatory)")]
    public string? ZipCode { get; set; }

    [Description("City (obligatory)")]
    public string? City { get; set; }

    [Description("Country code (obligatory)")]
    public string? CountryCode { get; set; }

    [Description("Email address (obligatory)")]
    public string? Email { get; set; }

    [Description("Phone number")]
    public string? Phone { get; set; }

    [Description("Customer type (obligatory)")]
    public string? Type { get; set; }

    [Description("Customer is active")]
    public bool? Active { get; set; }

    [Description("Currency")]
    public string? Currency { get; set; }

    [Description("Comments")]
    public string? Comments { get; set; }

    [Description("Website URL")]
    public string? Url { get; set; }

    [Description("Address line 2")]
    public string? Address2 { get; set; }

    [Description("Delivery address")]
    public string? DeliveryAddress1 { get; set; }

    [Description("Delivery city")]
    public string? DeliveryCity { get; set; }

    [Description("Delivery postal code")]
    public string? DeliveryZipCode { get; set; }

    [Description("Payment terms")]
    public string? TermsOfPayment { get; set; }

    [Description("Delivery terms")]
    public string? TermsOfDelivery { get; set; }

    [Description("Phone number 2")]
    public string? Phone2 { get; set; }

    [Description("Fax number")]
    public string? Fax { get; set; }
}



/// <summary>
/// VIES VAT validation response
/// </summary>
public class ViesValidationResponse
{
    public bool Valid { get; set; }
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? RequestDate { get; set; }
    public string? RequestIdentifier { get; set; }
}

/// <summary>
/// Swedish company search result
/// </summary>
public class SwedishCompanyResult
{
    public string? Name { get; set; }
    public string? OrgNumber { get; set; }
    public string? Status { get; set; }
    public Address? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
}

/// <summary>
/// Ratsit search result data
/// </summary>
public class RatsitData
{
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? ZipCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

/// <summary>
/// Google Places API result data
/// </summary>
public class GooglePlacesData
{
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string? PlaceId { get; set; }
}