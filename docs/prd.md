# PRD — Example Accounting Agent (ISA)  
*Updated:* 2025-09-11 (Europe/Madrid)

## Syfte
Bygga en AI-driven bokföringsagent (“ISA”) för Example AB som kan tolka underlag (mejl, PDF, bilder), fatta beslut om dokumenttyp (kvitto, leverantörsfaktura, kundfaktura m.m.), och automatiskt skapa verifikationer i Fortnox samt underhålla bilagor. Systemet ska köras i Docker Swarm (demo-cluster) och exekveras via chat-kommandon eller schemalagda triggers.

## Mål
- Automatisera leverantörsreskontra, kundreskontra, momsrapport och kvartalsrapporter.  
- Full spårbarhet: varje verifikation ska länkas till underlag.  
- Hybrid minnessystem för Isa: korttids- + långtidsminne.  
- E-signering via BankID + PAdES-LTV/LTA.  
- Plugin-arkitektur med stöd för svenska bokföringsregler.  
- WebChat med uppladdning, status och notifieringar (mail, sms, Slack, WhatsApp).  
- CI/CD: GitHub Actions → lokalt registry (registry.example.com:5000) → Swarm deploy.  

## Användarresor
- Som revisor vill jag kunna se **alla verifikationer och bilagor** i Fortnox.  
- Som redovisningsassistent vill jag att **inkomna kvitton via mail** tolkas och bokförs automatiskt.  
- Som kundansvarig vill jag få **påminnelse via SMS/Slack** när en faktura är försenad.  
- Som systemansvarig vill jag att **Isa’s minne** ska kunna återkalla tidigare dialoger och dokument.

## Omfattning (MVP – Fas 1)
- Dokumenttolkning (DocumentInterpreterPlugin) → klassificering av kvitton, lev.fakturor, kundfakturor.  
- FortnoxPlugin (fas 1): bokföring, verifikationer, leverantörs- & kundfakturor.  
- Rapportgeneratorer: resultat, balans, moms, verifikationslista.  
- WebChat med uppladdning & plugin-hantering.  
- Mail/Dropbox/Google Workspace integration för insamling av underlag.  
- SmsPlugin & WhatsAppPlugin för notifieringar.

## Fas 2
- Orderhantering i FortnoxPlugin.  
- Bankkopplingar (SEBPlugin, BankgiroPlugin).  
- WorkflowScheduler med Quartz.  
- Export till Excel/PowerBI.  
- Automatiska deklarationsunderlag.

## Fas 3
- Avancerad analys (kassaflöde, budgetavstämning, årsrapporter).  
- Integration med Skatteverket & SCB.  
- Klientportal för fildelning och status.  
- E-sign workflow direkt i WebChat.

## Tekniska krav
- .NET 9 / Aspire 9.2 (AppHost + ServiceDefaults).  
- Blazor Server (WebChat).  
- MCP (Model Context Protocol) – dynamisk plugin-exponering.  
- Hybrid memory: SQLite (korttidsminne), Postgres/pgvector (långtidsminne).  
- Observability: OpenTelemetry, Netdata, Grafana, Prometheus.  
- CI/CD: build/test → push → deploy, inga varningar tillåtna.  

## Plugins
- ✅ SmsPlugin – Twilio integration.  
- ✅ WhatsAppPlugin – Twilio WhatsApp.  
- ✅ MicrosoftOfficePlugin – Teams, OneDrive, M365.  
- ✅ DropboxPlugin – lagring.  
- ✅ GoogleWorkspacePlugin – mail, drive, calendar.  
- ✅ DocumentInterpreterPlugin – OCR/klassificering.  
- ✅ CompanyCheckupPlugin – bolagsdata.  
- ✅ DateTimePlugin – tidsfunktioner.  
- ✅ SlackPlugin – meddelanden.  
- ✅ OpenCagePlugin / OpenMeteoPlugin – hjälpverktyg.  
- ❌ FortnoxPlugin (pågående, fas 1).  
- ❌ SEBPlugin, BankgiroPlugin, SkatteverketPlugin, SCBPlugin (ej påbörjade).  

## Framgångskriterier
- <1% manuella fel i bokförda verifikationer över 30 dagar.  
- <5 min från mottaget dokument till postad verifikation.  
- 100% underlag spårbara.  
- Alla kritiska rapporter (P&L, balans, moms) kan genereras automatiskt.

## Risker
- Dokumenttolkning kan missa → fallback till manuell review i kö.  
- Fortnox API rate-limits → backoff + retry.  
- Minnesdatabas växer → kräver underhållsservice.  
- Säkerhet kring e-sign → BankID och PAdES måste vara korrekt implementerat.

## Användarresor

### A — Kunduppslag och registrering
**Scenario:** Ekonomiansvarig vill snabbt lägga till en ny kund.  
1. **A1** Användare: *“Hej Isa, kolla upp Example AB och lägg in som kund i Fortnox.”*  
2. **A2** Isa: *“Jag kommer slå upp Example AB och lägga till som kund i Fortnox, stämmer det?”*  
3. **A3** Användare: *“Ja, kör.”*  
4. **A4** Isa exekverar → skapar kund i Fortnox.  
5. **A5** Isa: *“✅ Kund skapad i Fortnox (Kundnr 12345). Vill du skapa en offert eller faktura direkt?”*  
6. **A6** Användare: *“Nej tack, det var allt.”*

---

### B — Fakturering
**Scenario:** Ekonomiansvarig vill skapa och skicka en faktura.  
1. **B1** Användare: *“Hej Isa, fakturera 10 timmar systemutveckling till RFLX AB.”*  
2. **B2** Isa: *“Jag kommer skapa en faktura på 10 timmar á 1 200 SEK till RFLX AB, stämmer det?”*  
3. **B3** Användare: *“Ja, gör det.”*  
4. **B4** Isa exekverar → faktura skapas i Fortnox.  
5. **B5** Isa: *“✅ Faktura #10023 skapad. Vill du att jag skickar den nu?”*  
6. **B6** Användare: *“Ja, skicka fakturan.”*  
7. **B7** Isa exekverar → skickar fakturan.  
8. **B8** Isa: *“📨 Faktura #10023 skickad till RFLX AB.”*

---

### C — Leverantörsfaktura
**Scenario:** Ekonomiansvarig laddar upp en leverantörsfaktura.  
1. **C1** Användare: *“Hej Isa, bokför denna leverantörsfaktura jag har laddat upp.”*  
2. **C2** Isa: *“Jag kommer tolka och bokföra denna leverantörsfaktura i Fortnox, stämmer det?”*  
3. **C3** Användare: *“Ja, gör det.”*  
4. **C4** Isa exekverar → tolkar dokument, bokför i Fortnox, laddar upp bilaga.  
5. **C5** Isa: *“✅ Leverantörsfaktura bokförd i Fortnox (Ver. V-7789). Bilaga länkad. Vill du se verifikationen nu?”*  
6. **C6** Användare: *“Nej tack, det var allt.”*

---

### Principer
- Isa **bekräftar alltid** innan exekvering.  
- Efter lyckad körning ges **kort status + förslag på nästa steg**.  
- Fel leder till dialog: Isa ger förslag på alternativ.  
- Dialoghistoriken sparas för spårbarhet.  
