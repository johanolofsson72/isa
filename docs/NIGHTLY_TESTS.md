# NIGHTLY TESTS — Plan & Schema

Senast uppdaterad: 2025-09-11

## Schema (Quartz)
- Start: varje natt 02:00 CET
- Ordning:
  1) Healthcheck endpoints
  2) Fortnox sandbox: read-only & provpost (idempotent)
  3) OCR: kör testbatch (Azure DI + Tesseract fallback)
  4) Notifieringar: skicka testmail/SMS (loopback)
  5) Rapporter: generera P&L och balans (export PDF/CSV)
  6) Backup: provkör “dry-run”
  7) Kostnadsrapport: generera och maila till systemansvarig

## Testfall (urval)
- **Fortnox**: hämta kundlista, skapa/annullera testfaktura (sandbox).
- **OCR**: extrahera referensbelopp, moms, datum från testfakturor.
- **Notifiering**: idempotensnyckel blockar dubblett.
- **Rapporter**: P&L faller tillbaka till källverifikationer.
- **Backup**: checksumma och storlek > 0; logg i `JobHistory`.

## Felhantering
- Retry m. backoff, max 5 försök (ADR-0025)
- DLQ för permanenta fel
- Larm till systemansvarig (multi-channel, ADR-0032/0037)

## Output
- Rapportpaket till `/mnt/nfs/reports/nightly/<YYYY-MM-DD>/`
- E-postsummering till systemansvarig
- Grafana-panel “Nightly Overview” uppdateras (ADR-0017)
