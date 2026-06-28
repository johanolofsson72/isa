# INSTRUCTIONS (Generella)
## Syfte
Standardiserade arbetssätt för alla repos med denna struktur.

## Struktur
- /.augment → AI-prompter/regler/status
- /docs → instruktioner, integrationer, ADRs, guider
- /src, /tests → reserverade för kod och tester
- /config, /scripts → konfig/automation

## Feature-baserad struktur (måste följas)
All ny kod organiseras per feature (inte globala mappar som Models/Services/Controllers utan Feature/Services, Feature/Entities, Features/Dtos etc).

/<FeatureName>
├── Dtos/
├── Entities/
├── Services/
└── Endpoints/

/Projects
├── Dtos/ProjectCreateDto.cs
├── Entities/Project.cs
├── Services/ProjectService.cs
└── Endpoints/CreateProjectEndpoint.cs

- All logik som hör till featuren ligger i dess mapp.
- Fördelar: bättre upptäckbarhet, lägre koppling, parallellt arbete utan konflikter.

## Repo & testlayout
- Produktionskod: `src/<Name>`
- Testkod (obligatoriskt): `tests/<Name>.Tests` (matcha mappstruktur)
- Testnamn: `Method_Should<Expect>_When<Condition>()`
- Testprojekt refererar produktionsprojektet.

## Build-kvalitet
- `/warnaserror`, `Nullable enable`, `AnalysisLevel latest`, deterministisk build.
- Inga TODO/FIXME i main. Inga hemligheter i kod eller loggar.

## Dokumentuppdatering (SSOT)
- Arkitekturbeslut → ADR i `/docs/adr-xxxx.md`.
- Endpoints/konfig/externa beroenden → `/docs/INTEGRATION.md`.
- Kodagenten ansvarar för att uppdatera ovanstående vid förändringar.

## Process
- Arkitekturbeslut → ADR i /docs (adr-xxxx.md).
- Integration/konfig → /docs/INTEGRATION.md.
- Produktkrav → /docs/prd.md.

## CI/CD (generellt)
- Steg: Build → Test → Lint → (ev. SCA) → Deploy.
- Artefakter: versionshanteras med semver (placeholder).

### STATUS
- Kommandot **"status"** ska alltid besvaras enligt system-promptens format.
- **/.augment/state.json** är sanningen för aktuell fas/resultat.

## Run (Aspire .NET 9 — standard)
- **Standardkörning (lokalt):**
dotnet run --project src/AppHost/AppHost.csproj
- **Kontinuerlig körning (watch):**
dotnet watch --project src/AppHost/AppHost.csproj
- **Status** ska alltid beskriva fasen för **AppHost** (restore/build/run) och kommande steg.

## Logs (konvention)
- **Primär källa:** Aspire Dashboard (om aktiv). Instrumenterad körning visar per-tjänst-loggar i dashboarden.
- **Sekundär källa (fil):** alla tjänster skriver (utöver console) till:
./logs/<ProjectName>/yyyyMMdd.log
- **Miljövariabler (rekommenderad konvention):**
- `LOG_ROOT=./logs`  ⇒ basmapp för fil-loggar
- (valfritt) `ASPIRE_DASHBOARD_URL` ⇒ om ni sätter en fast URL i ert projekt
- Vid fel eller oklarhet ska agenten:
1) Läs AppHost-output.
2) Peka på respektive tjänsts loggfil enligt `LOG_ROOT`.

## Struktur (måste följas)
- Kod: `/src/<Name>/...`
- Tester: `/tests/<Name>.Tests/...` (matcha mappstruktur mot `/src/<Name>`)
- Dokument: `/docs/**` (guider i `/docs/guides/**`)
- **Roten:** endast `README.md`, `LICENSE`, `CONTRIBUTING.md`, `.editorconfig`, `NuGet.config`, `Directory.*.props`, `.gitignore`.

## Förbjudet
- Testfiler i roten eller i `/src`.
- Nya dokument i roten (utom undantagen ovan).

## Konsekvens
- CI blockerar PR om regler bryts (se validate-structure).

## Docker-säkerhet
- Volymer = data → får aldrig tas bort av AI utan uttryckligt godkännande.
- Standard är att alltid visa **icke-destruktiva kommandon** (inspect, ls, logs).
- Rensning (containers/images/volymer) kräver alltid:
  1. En uttrycklig fråga till användaren
  2. Bekräftat "ja"

# INSTRUCTIONS — AccountingAgent

Senast uppdaterad: 2025-10-31

## Principer
- Följ ADR:er (se `docs/ADR_INDEX.md`). Om kod strider mot ADR → uppdatera antingen koden eller skapa ny ADR.
- Inga build-varningar: warnings behandlas som errors.
- All ny kod kräver tester (xUnit + FakeItEasy).
- Feature-baserad struktur: en mapp per domän (Customers, Invoices, Reports, …).
- Alla externa anrop ska vara idempotenta (ADR-0004, ADR-0025).

## Flöde för förändringar
1. Skapa issue → koppla till ADR/PRD.
2. Skapa branch enligt konvention: `feat/<area>-<short-desc>` eller `fix/...`.
3. Skriv/uppdatera tester.
4. Uppdatera docs vid behov:
   - Nya beslut → `docs/adr-xxxx.md`.
   - Nya processer → `docs/INSTRUCTIONS.md`.
   - Nya integrationer → `docs/INTEGRATION.md`.
   - Nya/ändrade plugins → `docs/PLUGINS.md`.
5. PR kräver grön CI (build, test, lint) och ingen varning.

## Kodstandard
- .NET 9, nullable enabled, `async` end-to-end.
- Dependency Injection för alla services.
- Konfiguration via `IOptions<>`, **inga** hemligheter i repo.
- Logging: strukturerad JSON med `traceId`/`spanId`.
- Observability: OTEL-instrumentering obligatorisk (ADR-0017).

### DocumentInterpreterPlugin → Fortnox
**Obligatoriska regler:**
1. **InvoiceChargesParserService** körs ALLTID för leverantörsfakturor (moms, avgifter, frakt)
2. **CreateFortnoxSupplierInvoiceData** skapar komplett fortnoxData med:
   - `supplierInvoiceRows` (REQUIRED av Fortnox API)
   - Intelligenta kontonummer baserat på beskrivning (SuggestAccountNumber)
   - Organisationsnummer för leverantörssökning
   - Workflow-metadata för AI:n (_workflow)
3. **ValidateAndLogFortnoxData** validerar och loggar all data före returnering
4. **AI-workflow** följer automatiskt stegen i _workflow-metadata:
   - Sök leverantör → Skapa om saknas → Fyll supplierNumber → Granska konton → Skapa faktura

## Teststandard
- xUnit + FakeItEasy (ADR-0011).
- Minst ett test per publik metod.
- Integrations-/nightlytester för externa API:er.
- `scripts/validate-test-layout.sh` körs i CI.

## UX & dialog
- Dialogdrivet (ADR-0005, ADR-0042): **bekräfta → exekvera → återkoppla → nästa steg**.
- Vid osäker match: redovisa val + bekräftelse (ADR-0047).
- Visa “Ångra”/kompensation där möjligt (ADR-0046).

## Säkerhet & data
- Hemligheter i KeyVault/env, aldrig i repo (ADR-0013).
- GDPR & retention enligt ADR-0036/0052.
- Arkivering & checksummor enligt ADR-0038.
