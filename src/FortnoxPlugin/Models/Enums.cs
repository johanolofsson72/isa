using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fortnox
{ 
    public enum InvoiceType
    {
        [Description("Faktura")]
        INVOICE,
        [Description("Avtalsfaktura")]
        AGREEMENTINVOICE,
        [Description("Räntefaktura")]
        INTRESTINVOICE,
        [Description("Samlingsfaktura")]
        SUMMARYINVOICE,
        [Description("Kontantfaktura")]
        CASHINVOICE
    }

    public enum Language
    {
        [Description("Svenska")]
        SV,
        [Description("Engelska")]
        EN
    }

    public enum PaymentWay
    {
        [Description("Kontantbetalning")]
        CASH,
        [Description("Kortbetalning")]
        CARD,
        [Description("Autogiro")]
        AG
    }

    public enum TaxReductionType
    {
        [Description("ingen")]
        none,
        [Description("ROT")]
        rot,
        [Description("RUT")]
        rut,
        [Description("Grön teknik")]
        green
    }

    public enum DiscountType
    {
        [Description("Belopp")]
        AMOUNT,
        [Description("Procent")]
        PERCENT
    }

    public enum HouseWorkType
    {
        [Description("Byggnadsarbete")]
        CONSTRUCTION,
        [Description("Elarbete")]
        ELECTRICITY,
        [Description("Glas- och metallarbete")]
        GLASSMETALWORK,
        [Description("Mark- och dräneringsarbe")]
        GROUNDDRAINAGEWORK,
        [Description("Murararbete")]
        MASONRY,
        [Description("Väggmålning och tapetsering")]
        PAINTINGWALLPAPERING,
        [Description("VVS-arbete")]
        HVAC,
        [Description("Reparation av större apparater")]
        MAJORAPPLIANCEREPAIR,
        [Description("Flyttjänster")]
        MOVINGSERVICES,
        [Description("IT-tjänster")]
        ITSERVICES,
        [Description("Städning")]
        CLEANING,
        [Description("Textiler och kläder")]
        TEXTILECLOTHING,
        [Description("SDnöröjning")]
        SNOWPLOWING,
        [Description("Trädgrådsarbete")]
        GARDENING,
        [Description("Barnpassning")]
        BABYSITTING,
        [Description("Annan vård")]
        OTHERCARE,
        [Description("Andra kostnader")]
        OTHERCOSTS,
        [Description("Solceller")]
        SOLARCELLS,
        [Description("Lagring av egenproducerad el")]
        STORAGESELFPRODUCEDELECTRICITY,
        [Description("Montering av laddstation")]
        CHARGINGSTATIONELECTRICVEHICLE,
        [Description("Underhåll av hemmet")]
        HOMEMAINTENANCE,
        [Description("Möblering")]
        FURNISHING,
        [Description("Transporttjänster")]
        TRANSPORTATIONSERVICES,
        [Description("Tvättning och skötsel av kläder")]
        WASHINGANDCAREOFCLOTHING
    }

    public enum SalesType
    {
        STOCK,
        SERVICE
    }

    public enum VATType
    {
        NORMAL,
        EUINTERNAL,
        REVERSE
    }

    public enum Code
    {
        TOT,
        VAT,
        FRT,
        AFE,
        ROV,
        CND,
        CNC,
        PRD,
        PRC,
        SRD,
        SRC,
        PRE,
        GWB,
        ACC
    }

    [Description("Olika sätt att filtrera en fakturasökning.")]
    public enum InvoiceFilter
    {
        [Description("Alla fakturor")]
        All,
        [Description("Avbrutna fakturor")]
        Cancelled,
        [Description("Betalda fakturor")]
        Fullypaid,
        [Description("Obetalda fakturor")]
        Unpaid,
        [Description("Obetalda och förfallna fakturor")]
        Unpaidoverdue,
        [Description("Obokade fakturor")]
        Unbooked
    }

    [Description("Bokföringsmetoder")]
    public enum AccountingMethod
    {
        [Description("Periodisering")]
        ACCRUAL,
        [Description("Kontant")]
        CASH
    }
}
