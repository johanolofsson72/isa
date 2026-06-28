# PLUGINS — Lista & Status

Senast uppdaterad: 2025-09-11

## Kärnplugins (aktiva)
- **FortnoxPlugin** — verifikationer, kunder, lev-/kundfakturor (ADR-0003, ADR-0026).
- **DocumentInterpreterPlugin** — OCR+AI tolkning (ADR-0019, ADR-0045).
- **CompanyCheckupPlugin** — företagsdata för onboarding (ADR-0033).
- **MailPlugin** — utgående notifieringar + inkommande ingestion (ADR-0039).
- **SmsPlugin / WhatsAppPlugin / SlackPlugin / MicrosoftOfficePlugin** — notifieringar (ADR-0014, ADR-0032, ADR-0037).
- **GoogleWorkspacePlugin / DropboxPlugin** — filhämtning (ADR-0031).
- **PeppolGatewayPlugin** — e-fakturor (ADR-0044).

## Planerade / i fas 2–3
- **SEBPlugin** — banktransaktioner (ADR-0027).
- **BankgiroPlugin** — in-/utbetalningar (ADR-0027).
- **SkatteverketPlugin / SCBPlugin** — myndighetsrapporter (ADR-0028).
- **ClientPortalPlugin** — kundportal (ADR-0021, ADR-0048).

## Lifecycle & Discovery
- Dynamic/Database/Hybrid discovery (ADR-0018).
- Plugin state per tenant (ADR-0043).
- Marketplace & distribution (ADR-0053).

> För varje plugin: `PluginFactory`, `Plugin.cs`, `README.md`, env-keys, testprojekt.
