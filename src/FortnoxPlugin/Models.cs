using System;
using System.ComponentModel;
using System.Numerics;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fortnox
{
    public record CustomerRequest
    {
        [Description("Kund")]
        public required Customer Customer { get; init; }
    }

    public record Customer
    {
        [Description("Kunden är aktiv")]
        public bool? Active { get; init; }

        [Description("Adress 1")]
        public required string Address1 { get; init; }

        [Description("Adress 2")]
        public string? Address2 { get; init; }

        [Description("Stad")]
        public required string City { get; init; }

        [Description("Kommentarer")]
        public string? Comments { get; init; }

        [Description("Kostnadsställe")]
        public string? CostCenter { get; init; }

        [Description("Land")]
        public string? Country { get; init; }

        [Description("Landskod")]
        public required string CountryCode { get; init; }

        [Description("Valuta")]
        public string? Currency { get; init; }

        [Description("Kundnummer")]
        public string? CustomerNumber { get; init; }

        [Description("Standardleveranssätt")]
        public DefaultDeliveryTypes? DefaultDeliveryTypes { get; init; }

        [Description("Standardmallar")]
        public DefaultTemplates? DefaultTemplates { get; init; }

        [Description("Leveransadress 1")]
        public string? DeliveryAddress1 { get; init; }

        [Description("Leveransadress 2")]
        public string? DeliveryAddress2 { get; init; }

        [Description("Leveransstad")]
        public string? DeliveryCity { get; init; }

        [Description("Leveransland")]
        public string? DeliveryCountry { get; init; }

        [Description("Leveranslandskod")]
        public string? DeliveryCountryCode { get; init; }

        [Description("Leveransfax")]
        public string? DeliveryFax { get; init; }

        [Description("Leveransnamn")]
        public string? DeliveryName { get; init; }

        [Description("Leveranstelefon 1")]
        public string? DeliveryPhone1 { get; init; }

        [Description("Leveranstelefon 2")]
        public string? DeliveryPhone2 { get; init; }

        [Description("Leverans postnummer")]
        public string? DeliveryZipCode { get; init; }

        [Description("E-post")]
        public required string Email { get; init; }

        [Description("E-post faktura")]
        public string? EmailInvoice { get; init; }

        [Description("E-post faktura BCC")]
        public string? EmailInvoiceBCC { get; init; }

        [Description("E-post faktura CC")]
        public string? EmailInvoiceCC { get; init; }

        [Description("E-post offert")]
        public string? EmailOffer { get; init; }

        [Description("E-post offert BCC")]
        public string? EmailOfferBCC { get; init; }

        [Description("E-post offert CC")]
        public string? EmailOfferCC { get; init; }

        [Description("E-post order")]
        public string? EmailOrder { get; init; }

        [Description("E-post order BCC")]
        public string? EmailOrderBCC { get; init; }

        [Description("E-post order CC")]
        public string? EmailOrderCC { get; init; }

        [Description("Extern referens")]
        public string? ExternalReference { get; init; }

        [Description("Fax")]
        public string? Fax { get; init; }

        [Description("GLN")]
        public string? GLN { get; init; }

        [Description("GLN leverans")]
        public string? GLNDelivery { get; init; }

        [Description("Faktura administrationsavgift")]
        public string? InvoiceAdministrationFee { get; init; }

        [Description("Faktura rabatt")]
        decimal InvoiceDiscount { get; init; }

        [Description("Faktura frakt")]
        public string? InvoiceFreight { get; init; }

        [Description("Faktura kommentar")]
        public string? InvoiceRemark { get; init; }

        [Description("Namn")]
        public required string Name { get; init; }

        [Description("Organisationsnummer")]
        public string? OrganisationNumber { get; init; }

        [Description("Vår referens")]
        public string? OurReference { get; init; }

        [Description("Telefon 1")]
        public string? Phone1 { get; init; }

        [Description("Telefon 2")]
        public string? Phone2 { get; init; }

        [Description("Prislista")]
        public string? PriceList { get; init; }

        [Description("Projekt")]
        public string? Project { get; init; }

        [Description("Försäljningskonto")]
        public string? SalesAccount { get; init; }

        [Description("Visa pris inkl moms")]
        bool ShowPriceVATIncluded { get; init; }

        [Description("Leveransvillkor")]
        public string? TermsOfDelivery { get; init; }

        [Description("Betalningsvillkor")]
        public string? TermsOfPayment { get; init; }

        [Description("Typ")]
        public required string Type { get; init; }

        [Description("Kundens URL")]
        public string? Url { get; init; }

        [Description("Momsnummer")]
        public string? VATNumber { get; init; }

        [Description("Momstyp")]
        public string? VATType { get; init; }

        [Description("Besöksadress")]
        public string? VisitingAddress { get; init; }
        
        [Description("Besöksstad")]
        public string? VisitingCity { get; init; }
        
        [Description("Besöksland")]
        public string? VisitingCountry { get; init; }
        
        [Description("Besökslandskod")]
        public string? VisitingCountryCode { get; init; }
        
        [Description("Besökspostnummer")]
        public string? VisitingZipCode { get; init; }
        
        [Description("Webbplats")]
        public string? WWW { get; init; }
        
        [Description("Leveranssätt")]
        public string? WayOfDelivery { get; init; }
        
        [Description("Er referens")]
        public string? YourReference { get; init; }
        
        [Description("Postnummer")]
        public required string ZipCode { get; init; }

    }

    public record DefaultDeliveryTypes
    {
        [Description("Faktura leveranssätt")]
        public string? Invoice { get; init; }
        
        [Description("Offert leveranssätt")]
        public string? Offer { get; init; }
        
        [Description("Order leveranssätt")]
        public string? Order { get; init; }
    }

    public record DefaultTemplates
    {
        [Description("Kontantfaktura mall")]
        public string? CashInvoice { get; init; }
        
        [Description("Faktura mall")]
        public string? Invoice { get; init; }
        
        [Description("Offert mall")]
        public string? Offer { get; init; }
        
        [Description("Order mall")]
        public string? Order { get; init; }
    }

    public record SupplierInvoiceRequest
    {
        [Description("Leverantörsfaktura")]
        public required SupplierInvoice SupplierInvoice { get; init; }
    }

    public record SupplierInvoice
    {
        [Description("URL")]
        public string? Url { get; init; }
        
        [Description("Redovisningsmetod")]
        public AccountingMethod? AccountingMethod { get; private init; } //Write protected in Fortnox.
        
        [Description("Administrationsavgift")]
        public string? AdministrationFee { get; init; }
        
        [Description("Saldo")]
        public string? Balance { get; init; }
        
        [Description("Bokad")]
        public bool? Booked { get; init; }
        
        [Description("Makulerad")]
        public bool? Cancelled { get; init; }
        
        [Description("Kommentarer")]
        public string? Comments { get; init; }
        
        [Description("Kostnadsställe")]
        public string? CostCenter { get; init; }
        
        [Description("Kredit")]
        public bool? Credit { get; init; }
        
        [Description("Kreditreferens")]
        public int? CreditReference { get; init; }
        
        [Description("Valuta")]
        public string? Currency { get; init; }
        
        [Description("Valutakurs")]
        public string? CurrencyRate { get; init; }
        
        [Description("Valutaenhet")]
        public int? CurrencyUnit { get; init; }
        
        [Description("Inaktivera betalningsfil")]
        public bool? DisablePaymentFile { get; init; }
        
        [Description("Förfallodatum")]
        public string? DueDate { get; init; }
        
        [Description("Extern fakturanummer")]
        public string? ExternalInvoiceNumber { get; init; }
        
        [Description("Extern fakturaserie")]
        public string? ExternalInvoiceSeries { get; init; }
        
        [Description("Slutbetalningsdatum")]
        public string? FinalPayDate { get; init; }
        
        [Description("Frakt")]
        public string? Freight { get; init; }
        
        [Description("Givet nummer")]
        public string? GivenNumber { get; init; }
        
        [Description("Fakturadatum")]
        public required string InvoiceDate { get; init; }
        
        [Description("Fakturanummer")]
        public string? InvoiceNumber { get; init; }
        
        [Description("OCR")]
        public string? OCR { get; init; }
        
        [Description("Vår referens")]
        public string? OurReference { get; init; }
        
        [Description("Betalning väntar")]
        public bool? PaymentPending { get; init; }
        
        [Description("Projekt")]
        public string? Project { get; init; }
        
        [Description("Avrundningsvärde")]
        public string? RoundOffValue { get; init; }
        
        [Description("Försäljningstyp")]
        public SalesType SalesType { get; init; }
        
        [Description("Leverantörsfakturarader")]
        public required List<SupplierInvoiceRow> SupplierInvoiceRows { get; init; }
        
        [Description("Leverantörsnamn")]
        [JsonInclude]
        public string? SupplierName { get; private init; } //Write protected in Fortnox.
        
        [Description("Leverantörsnummer")]
        public required string SupplierNumber { get; init; }
        
        [Description("Totalt")]
        public string? Total { get; init; }
        
        [Description("Moms")]
        public string? VAT { get; init; }
        
        [Description("Momstyp")]
        public VATType VATType { get; init; }
        
        [Description("Verifikationsnummer")]
        public int? VoucherNumber { get; init; }
        
        [Description("Verifikationsserie")]
        public string? VoucherSeries { get; init; }
        
        [Description("Verifikationsår")]
        public int? VoucherYear { get; init; }
        
        [Description("Verifikationer")]
        public List<Voucher>? Vouchers { get; init; }
        
        [Description("Er referens")]
        public string? YourReference { get; init; }
    }

    public record SupplierInvoiceRow
    {
        [Description("Konto")]
        public int? Account { get; init; }
        
        [Description("Kontobeskrivning")]
        public string? AccountDescription { get; init; }
        
        [Description("Artikelnummer")]
        public string? ArticleNumber { get; init; }
        
        [Description("Kod")]
        public Code Code { get; init; }
        
        [Description("Kostnadsställe")]
        public string? CostCenter { get; init; }
        
        [Description("Kredit")]
        public decimal? Credit { get; init; }
        
        [Description("Kreditvaluta")]
        public decimal? CreditCurrency { get; init; }
        
        [Description("Debet")]
        public decimal? Debit { get; init; }
        
        [Description("Debetvaluta")]
        public decimal? DebitCurrency { get; init; }
        
        [Description("Artikelbeskrivning")]
        public string? ItemDescription { get; init; }
        
        [Description("Pris")]
        public decimal? Price { get; init; }
        
        [Description("Projekt")]
        public string? Project { get; init; }
        
        [Description("Antal")]
        public int? Quantity { get; init; }
        
        [Description("Lagerplatskod")]
        public string? StockLocationCode { get; init; }
        
        [Description("Lagerpunktkod")]
        public string? StockPointCode { get; init; }
        
        [Description("Totalt")]
        public decimal? Total { get; init; }
        
        [Description("Transaktionsinformation")]
        public string? TransactionInformation { get; init; }
        
        [Description("Enhet")]
        public string? Unit { get; init; }
    }

    public record Voucher
    {
        [Description("Nummer")]
        public int? Number { get; init; }
        
        [Description("Referenstyp")]
        public string? ReferenceType { get; init; }
        
        [Description("Serie")]
        public string? Series { get; init; }
        
        [Description("År")]
        public int? Year { get; init; }
    }

    public record SupplierRequest
    {
        [Description("Leverantör")]
        public required Supplier Supplier { get; init; }
    }

    public record Supplier
    {
        [Description("URL")]
        public string? Url { get; init; }
        
        [Description("Leverantören är aktiv")]
        public bool? Active { get; init; }
        
        [Description("Adress 1")]
        public string? Address1 { get; init; }
        
        [Description("Adress 2")]
        public string? Address2 { get; init; }
        
        [Description("Bankgiro")]
        public string? BG { get; init; }
        
        [Description("BIC")]
        public string? BIC { get; init; }
        
        [Description("Bank")]
        public string? Bank { get; init; }
        
        [Description("Bankkontonummer")]
        public string? BankAccountNumber { get; init; }
        
        [Description("Bankens filialkod")]
        public string? BranchCode { get; init; }
        
        [Description("Stad")]
        public string? City { get; init; }
        
        [Description("Clearingnummer")]
        public string? ClearingNumber { get; init; }
        
        [Description("Kommentarer")]
        public string? Comments { get; init; }
        
        [Description("Kostnadsställe")]
        public string? CostCenter { get; init; }
        
        [Description("Land")]
        public string? Country { get; init; }
        
        [Description("Landskod")]
        public string? CountryCode { get; init; }
        
        [Description("Valuta")]
        public string? Currency { get; init; }
        
        [Description("Inaktivera betalningsfil")]
        public bool? DisablePaymentFile { get; init; }
        
        [Description("E-post")]
        public string? Email { get; init; }
        
        [Description("Fax")]
        public string? Fax { get; init; }
        
        [Description("IBAN")]
        public string? IBAN { get; init; }
        
        [Description("Namn")]
        public string? Name { get; init; }
        
        [Description("Organisationsnummer")]
        public string? OrganisationNumber { get; init; }
        
        [Description("Vårt kundnummer")]
        public string? OurCustomerNumber { get; init; }
        
        [Description("Vår referens")]
        public string? OurReference { get; init; }
        
        [Description("Plusgiro")]
        public string? PG { get; init; }
        
        [Description("Telefon 1")]
        public string? Phone1 { get; init; }
        
        [Description("Telefon 2")]
        public string? Phone2 { get; init; }
        
        [Description("Fördefinierat konto")]
        public string? PreDefinedAccount { get; init; }
        
        [Description("Projekt")]
        public string? Project { get; init; }
        
        [Description("Leverantörsnummer")]
        public string? SupplierNumber { get; init; }
        
        [Description("Betalningsvillkor")]
        public string? TermsOfPayment { get; init; }
        
        [Description("Momsnummer")]
        public string? VATNumber { get; init; }
        
        [Description("Momstyp")]
        public string? VATType { get; init; }
        
        [Description("Besöksadress")]
        public string? VisitingAddress { get; init; }
        
        [Description("Besöksstad")]
        public string? VisitingCity { get; init; }
        
        [Description("Besöksland")]
        public string? VisitingCountry { get; init; }
        
        [Description("Besökslandskod")]
        public string? VisitingCountryCode { get; init; }
        
        [Description("Besökspostnummer")]
        public string? VisitingZipCode { get; init; }
        
        [Description("Webbplats")]
        public string? WWW { get; init; }
        
        [Description("Arbetsplats")]
        public string? WorkPlace { get; init; }
        
        [Description("Er referens")]
        public string? YourReference { get; init; }
        
        [Description("Postnummer")]
        public string? ZipCode { get; init; }
    }
    
    public record AccountRequest
    {
        [Description("Konto")]
        public required Account Account { get; init; }
    }

    public record Account
    {
        [Description("URL")]
        public string? Url { get; init; }

        [Description("Kontot är aktivt")]
        public bool? Active { get; init; }

        [Description("Ingående balans")]
        public decimal? BalanceBroughtForward { get; init; }

        [Description("Utgående balans")]
        public decimal? BalanceCarriedForward { get; init; }

        [Description("Kostnadsställe")]
        public string? CostCenter { get; init; }

        [Description("Kostnadsställeinställningar")]
        public string? CostCenterSettings { get; init; }

        [Description("Beskrivning")]
        public string? Description { get; init; }

        [Description("Kontonummer")]
        public int Number { get; init; }

        [Description("Ingående kvantiteter")]
        public List<OpeningQuantity>? OpeningQuantities { get; init; }

        [Description("Projekt")]
        public string? Project { get; init; }

        [Description("Projektinställningar")]
        public string? ProjectSettings { get; init; }

        [Description("Kvantitetsinställningar")]
        public string? QuantitySettings { get; init; }

        [Description("Kvantitetsenhet")]
        public string? QuantityUnit { get; init; }

        [Description("SRU-kod")]
        public int? SRU { get; init; }

        [Description("Transaktionsinformation")]
        public string? TransactionInformation { get; init; }

        [Description("Transaktionsinformationsinställningar")]
        public string? TransactionInformationSettings { get; init; }

        [Description("Moms-kod")]
        public string? VATCode { get; init; }

        [Description("År")]
        public int? Year { get; init; }
    }

    public record OpeningQuantity
    {
        [Description("Balans")]
        public decimal? Balance { get; init; }

        [Description("Projekt")]
        public string? Project { get; init; }
    }
    
    public record ProjectRequest
    {
        [Description("Projekt")]
        public required Project Project { get; init; }
    }

    public record Project
    {
        [Description("URL till projektet")]
        [JsonPropertyName("@url")]
        public string? Url { get; init; }

        [Description("Projektnummer - unikt ID i Fortnox")]
        public string? ProjectNumber { get; init; }

        [Description("Projektnamn")]
        public required string Description { get; init; }

        [Description("Status (ACTIVE, ARCHIVED, NOTSTARTED, COMPLETED)")]
        public string? Status { get; init; }

        [Description("Startdatum (YYYY-MM-DD)")]
        public string? StartDate { get; init; }

        [Description("Slutdatum (YYYY-MM-DD)")]
        public string? EndDate { get; init; }

        [Description("Kundnummer kopplat till projektet")]
        public string? CustomerNumber { get; init; }

        [Description("Intern kommentar / anteckningar")]
        public string? Notes { get; init; }

        [Description("Aktivt projekt")]
        public bool? Active { get; init; }
    }
}