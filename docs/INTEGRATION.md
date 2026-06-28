# INTEGRATION — Översikt & Setup

Senast uppdaterad: 2025-10-31 (DocumentInterpreter → Fortnox intelligent workflow)

## Fortnox (ADR-0003, ADR-0026)
- OAuth2 med separata credentials för sandbox/prod.
- `externalIdempotencyKey` på alla skrivande anrop.
- Bilagor laddas alltid upp och länkas till verifikationer.
- Tester: sandbox i nightly, smoke read-only i prod.

### FortnoxPlugin API-funktioner
**Kunder & Leverantörer:**
- `GetCustomersAsync()` - Hämta kundlista
- `CreateCustomerAsync(CustomerRequest)` - Skapa ny kund
- `GetSuppliersAsync()` - Hämta leverantörslista
- `CreateSupplierAsync(SupplierRequest)` - Skapa ny leverantör

**Fakturor:**
- `GetInvoiceAsync(documentNumber)` - Hämta kundfaktura
- `CreateInvoiceAsync(InvoiceRequest)` - Skapa kundfaktura
- `BookkeepInvoiceAsync(documentNumber)` - Bokför kundfaktura
- `SearchInvoicesAsync(filter, ...)` - Sök fakturor
- `PreviewInvoiceAsync(documentNumber)` - Förhandsgranska PDF
- `SendInvoiceAsEmailAsync(documentNumber)` - Skicka via email

**Leverantörsfakturor:**
- `GetSupplierInvoicesAsync()` - Hämta leverantörsfakturor
- `CreateSupplierInvoiceAsync(SupplierInvoiceRequest)` - Skapa leverantörsfaktura

**Kontoplan & Export:**
- `GetAccountsAsync()` - Hämta kontoplan
- `GetSIE4Async(financialYear)` - Hämta SIE4-fil för bokföringsexport

## BankID / E-sign (ADR-0007, ADR-0048)
- Primärt BankID, PAdES-LTV/LTA för signerade PDF:er.
- Fas 1 via Verified/webhooks, fas 2 direkt BankID.
- Signerade dokument arkiveras under `/mnt/nfs/archives/esign/`.

## Peppol / E-faktura (ADR-0044)
- Peppol BIS Billing 3.0; validering med XSD.
- Gateway-plugin (`PeppolGatewayPlugin`), certifikat i KeyVault.
- Fallback: EasyInvoice/PDF/e-post.

## Mail / Notifieringar (ADR-0014, ADR-0039)
- SMTP/Mailjet för utgående; IMAP/Gmail för ingestion.
- Virus-scan (ClamAV) före OCR.
- Multi-channel failover enligt ADR-0032.

## Bank & Betalningar (ADR-0027)
- SEB API för saldo/transaktioner.
- Bankgiro (BGMAX/LB) import/export med schemalagda jobb.
- Matchning mot verifikationer i Fortnox.

## OCR & Dokument (ADR-0019, ADR-0045)
- Azure Document Intelligence primärt; Tesseract fallback.
- AI-extraktion till `FinancialDocumentDto`.
- DLQ + manuell review i WebChat vid osäkerhet.

### DocumentInterpreterPlugin → Fortnox Workflow
**Automatisk leverantörsfaktura-hantering:**

1. **Dokumenttolkning** (DocumentInterpreterPlugin)
   - Använder InvoiceChargesParserService för moms, avgifter, frakt
   - Extraherar fakturarader (Lines) med beskrivning, pris, moms
   - Validerar och loggar all data före returnering

2. **Intelligent Fortnox-data** (CreateFortnoxSupplierInvoiceData)
   - Skapar komplett `supplierInvoiceRows` med intelligenta kontonummer
   - Automatisk kontomappning baserad på beskrivning:
     * Software/SaaS → 6540 (IT-tjänster)
     * Kontorsmaterial → 6100
     * Marknadsföring → 6110
     * Resor/hotell → 6250
     * Konsulttjänster → 5010
     * Telekom → 6230
     * Standard → 4010 (Varuinköp)
   - Inkluderar organisationsnummer för leverantörssökning

3. **AI Workflow** (automatisk via _workflow-metadata)
   - Steg 1: `FortnoxPlugin.GetSuppliersAsync` - Sök leverantör
   - Steg 2: Om saknas → `CompanyCheckupPlugin.LookupCompanyAsync` + `FortnoxPlugin.CreateSupplierAsync`
   - Steg 3: Fyll i `supplierNumber`
   - Steg 4: Granska föreslagna kontonummer (redan ifyllda)
   - Steg 5: `FortnoxPlugin.CreateSupplierInvoiceAsync`

**Resultat:** Komplett, validerad fortnoxData med intelligenta defaults som AI:n kan använda direkt.

## Observability & Drift (ADR-0017, ADR-0023, ADR-0050)
- OTEL + Prometheus + Grafana; Netdata globalt.
- Docker Swarm primärt; ACA för hybrid/fallback.
- Backup/restore enligt ADR-0040.

> Alla hemligheter hanteras via miljövariabler/KeyVault. Se respektive plugin-README för exakta env-nycklar.
