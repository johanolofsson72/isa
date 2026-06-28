using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fortnox
{
    [Description("Datastruktur som skickas som parameter för att skapa eller uppdatera en kundfaktura.")]
    public record InvoiceRequest
    {
        [Description("Fakturauppgifter")]
        public required InvoicePayload Invoice { get; init; }
    }

    [Description("Datastruktur som innehåller all information om en kundfaktura samt information om status för anropet med eventuella felmeddelanden")]
    public record InvoiceResponse
    {
        [Description("Faktura")]
        public Invoice? Invoice { get; init; }
        [Description("Talar om ifall anropet lyckades")]
        public required bool Success { get; init; }

        [Description("Innehåller data om eventuella felmedddelanden")]
        public string? ErrorMessage { get; init; }
    }

    [Description("Datastruktur som innehåller all information om framsökta kundfakturor samt information om status för anropet med eventuella felmeddelanden")]
    public record InvoiceListResponse
    {
        [Description("Lista med framsökta kundfakturor")]
        public IReadOnlyList<InvoiceListItem>? InvoiceListItems { get; init; }
        [Description("Talar om ifall anropet lyckades")]
        public required bool Success { get; init; }

        [Description("Innehåller data om eventuella felmedddelanden")]
        public string? ErrorMessage { get; init; }
    }


    [Description("Representerar datastrukturen för att skapa eller uppdatera en kundfaktura.")]
    public record InvoicePayload
    {
        [Description("Adressrad 1.")]
        public string? Address1 { get; init; }

        [Description("Adressrad 2.")]
        public string? Address2 { get; init; }

        [Description("Administrationsavgift.")]
        public double? AdministrationFee { get; init; }

        [Description("Stad. Max 1024 tecken.")]
        public string? City { get; init; }

        [Description("Kommentarer. Max 1024 tecken.")]
        public string? Comments { get; init; }

        [Description("Kostnadsställe.")]
        public string? CostCenter { get; init; }

        [Description("Land.")]
        public string? Country { get; init; }

        [Description("Referens till kreditfaktura.")]
        public string? CreditInvoiceReference { get; init; }

        [Description("Valuta.")]
        public string? Currency { get; init; }

        [Description("Valutakurs.")]
        public double? CurrencyRate { get; init; }

        [Description("Valutaenhet.")]
        public int? CurrencyUnit { get; init; }

        [Description("Kundnummer. Detta fält är obligatoriskt.")]
        public required string CustomerNumber { get; init; }

        [Description("Kundens namn.")]
        public string? CustomerName { get; init; }

        [Description("Leveransadress 1. Max 1024 tecken.")]
        public string? DeliveryAddress1 { get; init; }

        [Description("Leveransadress 2. Max 1024 tecken.")]
        public string? DeliveryAddress2 { get; init; }

        [Description("Leveransstad. Max 1024 tecken.")]
        public string? DeliveryCity { get; init; }

        [Description("Leveransland.")]
        public string? DeliveryCountry { get; init; }

        [Description("Leveransdatum.")]
        public DateTime? DeliveryDate { get; init; }

        [Description("Leveransnamn. Max 1024 tecken.")]
        public string? DeliveryName { get; init; }

        [Description("Postnummer för leverans. Max 1024 tecken.")]
        public string? DeliveryZipCode { get; init; }

        [Description("Fakturanummer.")]
        public string? DocumentNumber { get; init; }

        [Description("Förfallodatum.")]
        public DateTime? DueDate { get; init; }

        [Description("EDI-information för fakturan.")]
        public EDIInformation? EDIInformation { get; init; }

        [Description("Anger om fakturan ska med i EU-kvartalsrapporten.")]
        public bool? EUQuarterlyReport { get; init; }

        [Description("E-postinformation för utskick.")]
        public EmailInformation? EmailInformation { get; init; }

        [Description("Extern fakturareferens 1. Max 80 tecken.")]
        public string? ExternalInvoiceReference1 { get; init; }

        [Description("Extern fakturareferens 2. Max 80 tecken.")]
        public string? ExternalInvoiceReference2 { get; init; }

        [Description("Fraktavgift.")]
        public double? Freight { get; init; }

        [Description("Fakturadatum.")]
        public DateTime? InvoiceDate { get; init; }

        [Description("Lista med fakturarader.")]
        public IReadOnlyList<InvoiceRowPayload>? InvoiceRows { get; init; }

        [Description("Typ av faktura.")]
        public InvoiceType? InvoiceType { get; init; }

        [Description("Lista med etiketter (labels) för fakturan.")]
        public IReadOnlyList<Label>? Labels { get; init; }

        [Description("Språk för fakturan.")]
        public Language? Language { get; init; }

        [Description("Anger om fakturan är ej färdigställd.")]
        public bool? NotCompleted { get; init; }

        [Description("OCR-nummer.")]
        public string? OCR { get; init; }

        [Description("Vår referens. Max 50 tecken.")]
        public string? OurReference { get; init; }

        [Description("Utleveransdatum.")]
        public DateTime? OutboundDate { get; init; }

        [Description("Betalningssätt.")]
        public string? PaymentWay { get; init; }

        [Description("Telefonnummer 1. Max 1024 tecken.")]
        public string? Phone1 { get; init; }

        [Description("Telefonnummer 2. Max 1024 tecken.")]
        public string? Phone2 { get; init; }

        [Description("Prislista.")]
        public string? PriceList { get; init; }

        [Description("Utskriftsmall.")]
        public string? PrintTemplate { get; init; }

        [Description("Projekt.")]
        public string? Project { get; init; }

        [Description("Anmärkningar. Max 1024 tecken.")]
        public string? Remarks { get; init; }

        [Description("Typ av skattereduktion.")]
        public TaxReductionType? TaxReductionType { get; init; }

        [Description("Leveransvillkor.")]
        public string? TermsOfDelivery { get; init; }

        [Description("Betalningsvillkor.")]
        public string? TermsOfPayment { get; init; }

        [Description("Anger om moms är inkluderat i priset.")]
        public bool? VATIncluded { get; init; }

        [Description("Leveranssätt.")]
        public string? WayOfDelivery { get; init; }

        [Description("Er ordernummer. Max 75 tecken.")]
        public string? YourOrderNumber { get; init; }

        [Description("Er referens. Max 50 tecken.")]
        public string? YourReference { get; init; }

        [Description("Postnummer. Max 1024 tecken.")]
        public string? ZipCode { get; init; }
    }

    [Description("Representerar EDI-information (Electronic Data Interchange) för en kundfaktura.")]
    public record EDIInformation
    {
        [Description("Global Location Number (GLN). Max 13 tecken.")]
        public string? EDIGlobalLocationNumber { get; init; }

        [Description("Global Location Number (GLN) för leverans. Max 13 tecken.")]
        public string? EDIGlobalLocationNumberDelivery { get; init; }

        [Description("Extra EDI-information, fält 1.")]
        public string? EDIInvoiceExtra1 { get; init; }

        [Description("Extra EDI-information, fält 2.")]
        public string? EDIInvoiceExtra2 { get; init; }

        [Description("Vår elektroniska referens för EDI.")]
        public string? EDIOurElectronicReference { get; init; }

        [Description("Status för EDI.")]
        public string? EDIStatus { get; init; }

        [Description("Er elektroniska referens för EDI.")]
        public string? EDIYourElectronicReference { get; init; }
    }

    [Description("Representerar e-postinformation för utskick av en kundfaktura.")]
    public record EmailInformation
    {
        [Description("E-postadress för mottagare (BCC - dold kopia).")]
        public string? EmailAddressBCC { get; init; }

        [Description("E-postadress för mottagare (CC - kopia).")]
        public string? EmailAddressCC { get; init; }

        [Description("Avsändarens e-postadress.")]
        public string? EmailAddressFrom { get; init; }

        [Description("Mottagarens e-postadress.")]
        public string? EmailAddressTo { get; init; }

        [Description("Brödtexten i e-postmeddelandet. Max 20000 tecken.")]
        public string? EmailBody { get; init; }

        [Description("Ämnesraden för e-postmeddelandet. Max 100 tecken.")]
        public string? EmailSubject { get; init; }
    }


    [Description("Representerar datastrukturen för en enskild fakturarad med artikelinformation, pris och antal för att skapa eller uppdatera en kundfaktura.")]
    public record InvoiceRowPayload
    {
        [Description("Kontonummer. Måste vara mellan 1000 och 9999.")]
        public int? AccountNumber { get; init; }

        [Description("Artikelnummer.")]
        public string? ArticleNumber { get; init; }

        [Description("Kostnadsställe.")]
        public string? CostCenter { get; init; }

        [Description("Levererat antal.")]
        public string? DeliveredQuantity { get; init; }

        [Description("Beskrivning av raden.")]
        public string? Description { get; init; }

        [Description("Rabattbelopp eller rabattprocent.")]
        public double? Discount { get; init; }

        [Description("Typ av rabatt.")]
        public DiscountType? DiscountType { get; init; }

        [Description("Anger om raden avser hushållsarbete (ROT/RUT/Grön teknik).")]
        public bool? HouseWork { get; init; }

        [Description("Antal timmar att rapportera för hushållsarbete. Max 999.")]
        public int? HouseWorkHoursToReport { get; init; }

        [Description("Typ av hushållsarbete.")]
        public HouseWorkType? HouseWorkType { get; init; }

        [Description("Pris per enhet.")]
        public double? Price { get; init; }

        [Description("Projektkod.")]
        public string? Project { get; init; }

        [Description("Unikt ID för raden.")]
        public int? RowId { get; init; }

        [Description("Lagerplatskod.")]
        public string? StockPointCode { get; init; }

        [Description("Enhet (t.ex. 'st', 'tim').")]
        public string? Unit { get; init; }

        [Description("Momssats i procent.")]
        public int? VAT { get; init; }

        [Description("Momskod.")]
        public string? VATCode { get; init; }
    }

    [Description("Representerar en komplett kundfaktura med all tillhörande information.")]
    public record Invoice
    {
        [Description("Bokföringsmetod.")]
        public AccountingMethod? AccountingMethod { get; init; }

        [Description("Adressrad 1.")]
        public string? Address1 { get; init; }

        [Description("Adressrad 2.")]
        public string? Address2 { get; init; }

        [Description("Administrationsavgift.")]
        public double? AdministrationFee { get; init; }

        [Description("Moms för administrationsavgiften.")]
        public double? AdministrationFeeVAT { get; init; }

        [Description("Saldo.")]
        public double? Balance { get; init; }

        [Description("Underlag för skattereduktion.")]
        public double? BasisTaxReduction { get; init; }

        [Description("Anger om fakturan är bokförd.")]
        public bool? Booked { get; init; }

        [Description("Anger om fakturan är makulerad.")]
        public bool? Cancelled { get; init; }

        [Description("Stad. Max 1024 tecken.")]
        public string? City { get; init; }

        [Description("Kommentarer. Max 1024 tecken.")]
        public string? Comments { get; init; }

        [Description("Referens till kontrakt.")]
        public string? ContractReference { get; init; }

        [Description("Täckningsbidrag i procent.")]
        public double? ContributionPercent { get; init; }

        [Description("Täckningsbidrag i värde.")]
        public double? ContributionValue { get; init; }

        [Description("Kostnadsställe.")]
        public string? CostCenter { get; init; }

        [Description("Land.")]
        public string? Country { get; init; }

        [Description("Anger om det är en kreditfaktura.")]
        public string? Credit { get; init; }

        [Description("Referens till kreditfaktura.")]
        public string? CreditInvoiceReference { get; init; }

        [Description("Valuta.")]
        public string? Currency { get; init; }

        [Description("Valutakurs.")]
        public double? CurrencyRate { get; init; }

        [Description("Valutaenhet.")]
        public int? CurrencyUnit { get; init; }

        [Description("Kundens namn.")]
        public string? CustomerName { get; init; }

        [Description("Kundnummer. Detta fält är obligatoriskt.")]
        public required string CustomerNumber { get; init; }

        [Description("Leveransadress 1. Max 1024 tecken.")]
        public string? DeliveryAddress1 { get; init; }

        [Description("Leveransadress 2. Max 1024 tecken.")]
        public string? DeliveryAddress2 { get; init; }

        [Description("Leveransstad. Max 1024 tecken.")]
        public string? DeliveryCity { get; init; }

        [Description("Leveransland.")]
        public string? DeliveryCountry { get; init; }

        [Description("Leveransdatum.")]
        public DateTime? DeliveryDate { get; init; }

        [Description("Leveransnamn. Max 1024 tecken.")]
        public string? DeliveryName { get; init; }

        [Description("Postnummer för leverans. Max 1024 tecken.")]
        public string? DeliveryZipCode { get; init; }

        [Description("Fakturanummer.")]
        public string? DocumentNumber { get; init; }

        [Description("Förfallodatum.")]
        public DateTime? DueDate { get; init; }

        [Description("EDI-information för fakturan.")]
        public EDIInformation? EDIInformation { get; init; }

        [Description("Anger om fakturan ska med i EU-kvartalsrapporten.")]
        public bool? EUQuarterlyReport { get; init; }

        [Description("E-postinformation för utskick.")]
        public EmailInformation? EmailInformation { get; init; }

        [Description("Extern fakturareferens 1. Max 80 tecken.")]
        public string? ExternalInvoiceReference1 { get; init; }

        [Description("Extern fakturareferens 2. Max 80 tecken.")]
        public string? ExternalInvoiceReference2 { get; init; }

        [Description("Slutbetalningsdatum.")]
        public DateTime? FinalPayDate { get; init; }

        [Description("Fraktavgift.")]
        public double? Freight { get; init; }

        [Description("Moms för fraktavgiften.")]
        public double? FreightVAT { get; init; }

        [Description("Bruttobelopp.")]
        public double? Gross { get; init; }

        [Description("Anger om fakturan inkluderar hushållsarbete.")]
        public bool? HouseWork { get; init; }

        [Description("Fakturadatum.")]
        public DateTime? InvoiceDate { get; init; }

        [Description("Slutdatum för fakturaperioden.")]
        public DateTime? InvoicePeriodEnd { get; init; }

        [Description("Referens för fakturaperioden.")]
        public string? InvoicePeriodReference { get; init; }

        [Description("Startdatum för fakturaperioden.")]
        public DateTime? InvoicePeriodStart { get; init; }

        [Description("Lista med fakturarader.")]
        public IReadOnlyList<InvoiceRow>? InvoiceRows { get; init; }

        [Description("Typ av faktura.")]
        public InvoiceType? InvoiceType { get; init; }

        [Description("Lista med etiketter (labels) för fakturan.")]
        public IReadOnlyList<Label>? Labels { get; init; }

        [Description("Språk för fakturan.")]
        public Language? Language { get; init; }

        [Description("Datum för senaste påminnelse.")]
        public DateTime? LastRemindDate { get; init; }

        [Description("Nettobelopp.")]
        public double? Net { get; init; }

        [Description("Anger om fakturan är ej färdigställd.")]
        public bool? NotCompleted { get; init; }

        [Description("Anger om fakturan är kopplad till Nox Finans.")]
        public bool? NoxFinans { get; init; }

        [Description("OCR-nummer.")]
        public string? OCR { get; init; }

        [Description("Referens till offert.")]
        public string? OfferReference { get; init; }

        [Description("Referens till order.")]
        public string? OrderReference { get; init; }

        [Description("Organisationsnummer.")]
        public string? OrganisationNumber { get; init; }

        [Description("Vår referens. Max 50 tecken.")]
        public string? OurReference { get; init; }

        [Description("Utleveransdatum.")]
        public DateTime? OutboundDate { get; init; }

        [Description("Betalningssätt.")]
        public PaymentWay? PaymentWay { get; init; }

        [Description("Telefonnummer 1. Max 1024 tecken.")]
        public string? Phone1 { get; init; }

        [Description("Telefonnummer 2. Max 1024 tecken.")]
        public string? Phone2 { get; init; }

        [Description("Prislista.")]
        public string? PriceList { get; init; }

        [Description("Utskriftsmall.")]
        public string? PrintTemplate { get; init; }

        [Description("Projekt.")]
        public string? Project { get; init; }

        [Description("Anmärkningar. Max 1024 tecken.")]
        public string? Remarks { get; init; }

        [Description("Antal påminnelser skickade.")]
        public int? Reminders { get; init; }

        [Description("Öresavrundning.")]
        public double? RoundOff { get; init; }

        [Description("Anger om fakturan är skickad.")]
        public bool? Sent { get; init; }

        [Description("Beviljad skattereduktion.")]
        public int? TaxReduction { get; init; }

        [Description("Typ av skattereduktion.")]
        public TaxReductionType? TaxReductionType { get; init; }

        [Description("Leveransvillkor.")]
        public string? TermsOfDelivery { get; init; }

        [Description("Betalningsvillkor.")]
        public string? TermsOfPayment { get; init; }

        [Description("Referens till tidbasis.")]
        public int? TimeBasisReference { get; init; }

        [Description("Totalbelopp.")]
        public double? Total { get; init; }

        [Description("Totalt att betala.")]
        public double? TotalToPay { get; init; }

        [Description("Total moms.")]
        public double? TotalVAT { get; init; }

        [Description("URL till resursen.")]
        [JsonPropertyName("@url")]
        public string? Url { get; init; }

        [Description("URL till skattereduktionslistan.")]
        [JsonPropertyName("@urlTaxReductionList")]
        public string? UrlTaxReductionList { get; init; }

        [Description("Anger om moms är inkluderat i priset.")]
        public bool? VATIncluded { get; init; }

        [Description("Verifikationsnummer.")]
        public int? VoucherNumber { get; init; }

        [Description("Verifikationsserie.")]
        public string? VoucherSeries { get; init; }

        [Description("Verifikationsår.")]
        public int? VoucherYear { get; init; }

        [Description("Anger om lagret är redo.")]
        public bool? WarehouseReady { get; init; }

        [Description("Leveranssätt.")]
        public string? WayOfDelivery { get; init; }

        [Description("Er ordernummer. Max 75 tecken.")]
        public string? YourOrderNumber { get; init; }

        [Description("Er referens. Max 50 tecken.")]
        public string? YourReference { get; init; }

        [Description("Postnummer. Max 1024 tecken.")]
        public string? ZipCode { get; init; }
    }

    [Description("Representerar en enskild fakturarad med artikelinformation, pris och antal.")]
    public record InvoiceRow
    {
        [Description("Kontonummer. Måste vara mellan 1000 och 9999.")]
        public int? AccountNumber { get; init; }

        [Description("Artikelnummer.")]
        public string? ArticleNumber { get; init; }

        [Description("Täckningsbidrag i procent.")]
        public string? ContributionPercent { get; init; }

        [Description("Täckningsbidrag i värde.")]
        public string? ContributionValue { get; init; }

        [Description("Kostnadsställe.")]
        public string? CostCenter { get; init; }

        [Description("Levererat antal.")]
        public string? DeliveredQuantity { get; init; }

        [Description("Beskrivning av raden. Max 255 tecken.")]
        public string? Description { get; init; }

        [Description("Rabattbelopp eller rabattprocent.")]
        public double? Discount { get; init; }

        [Description("Typ av rabatt.")]
        public DiscountType? DiscountType { get; init; }

        [Description("Anger om raden avser hushållsarbete (ROT/RUT/Grön teknik).")]
        public bool? HouseWork { get; init; }

        [Description("Antal timmar att rapportera för hushållsarbete. Max 999.")]
        public int? HouseWorkHoursToReport { get; init; }

        [Description("Typ av hushållsarbete.")]
        public HouseWorkType? HouseWorkType { get; init; }

        [Description("Pris per enhet.")]
        public double? Price { get; init; }

        [Description("Pris per enhet exklusive moms.")]
        public double? PriceExcludingVAT { get; init; }

        [Description("Projektkod.")]
        public string? Project { get; init; }

        [Description("Unikt ID för raden.")]
        public int? RowId { get; init; }

        [Description("Lagerplatskod.")]
        public string? StockPointCode { get; init; }

        [Description("Totalt belopp för raden.")]
        public double? Total { get; init; }

        [Description("Totalt belopp för raden exklusive moms.")]
        public double? TotalExcludingVAT { get; init; }

        [Description("Enhet (t.ex. 'st', 'tim').")]
        public string? Unit { get; init; }

        [Description("Momssats i procent.")]
        public int? VAT { get; init; }

        [Description("Momskod.")]
        public string? VATCode { get; init; }
    }

    [Description("Representerar en etikett (label).")]
    public record Label
    {
        public int? Id { get; set; }
    }

    [Description("Representerar en enskild kundfaktura i en lista, med sammanfattande information.")]
    public record InvoiceListItem
    {
        [Description("Återstående saldo att betala.")]
        public double? Balance { get; init; }

        [Description("Anger om fakturan är bokförd.")]
        public bool? Booked { get; init; }

        [Description("Anger om fakturan är makulerad.")]
        public bool? Cancelled { get; init; }

        [Description("Kostnadsställe.")]
        public string? CostCenter { get; init; }

        [Description("Valuta.")]
        public string? Currency { get; init; }

        [Description("Valutakurs.")]
        [JsonConverter(typeof(StringToNullableDoubleConverter))]
        public double? CurrencyRate { get; init; }

        [Description("Valutaenhet.")]
        public double? CurrencyUnit { get; init; }

        [Description("Kundens namn.")]
        public string? CustomerName { get; init; }

        [Description("Kundnummer.")]
        public string? CustomerNumber { get; init; }

        [Description("Fakturanummer.")]
        public string? DocumentNumber { get; init; }

        [Description("Förfallodatum.")]
        public DateTime? DueDate { get; init; }

        [Description("Extern fakturareferens 1.")]
        public string? ExternalInvoiceReference1 { get; init; }

        [Description("Extern fakturareferens 2.")]
        public string? ExternalInvoiceReference2 { get; init; }

        [Description("Slutbetalningsdatum.")]
        public DateTime? FinalPayDate { get; init; }

        [Description("Fakturadatum.")]
        public DateTime? InvoiceDate { get; init; }

        [Description("Typ av faktura.")]
        public InvoiceType? InvoiceType { get; init; }

        [Description("Anger om fakturan är kopplad till Nox Finans.")]
        public bool? NoxFinans { get; init; }

        [Description("OCR-nummer.")]
        public string? OCR { get; init; }

        [Description("Projektkod.")]
        public string? Project { get; init; }

        [Description("Anger om fakturan är skickad.")]
        public bool? Sent { get; init; }

        [Description("Betalningsvillkor.")]
        public string? TermsOfPayment { get; init; }

        [Description("Totalt belopp för fakturan.")]
        public double? Total { get; init; }

        [Description("URL till resursen.")]
        [JsonPropertyName("@url")]
        public string? Url { get; init; }

        [Description("Verifikationsnummer.")]
        public int? VoucherNumber { get; init; }

        [Description("Verifikationsserie.")]
        public string? VoucherSeries { get; init; }

        [Description("Verifikationsår.")]
        public int? VoucherYear { get; init; }

        [Description("Leveranssätt.")]
        public string? WayOfDelivery { get; init; }
    }
}
