# AnalysisPlugin E2E Tests

End-to-end tester för AnalysisPlugin som testar hela flödet från naturliga språkförfrågningar via WebChat genom AppHost med Agent och MailPlugin.

## Översikt

Dessa tester verifierar:
- ✅ Naturliga språkförfrågningar från specifikationen fungerar
- ✅ AI-agenten orkestrerar korrekt mellan plugins
- ✅ Rapporter genereras med professionell formatering
- ✅ MailPlugin integreras korrekt för e-postleverans
- ✅ Excel-filer genereras med korrekt finansiell data

## Förutsättningar

### 1. AppHost måste köra
```bash
# Starta AppHost i en separat terminal
cd src/AppHost
dotnet run
```

### 2. MailPlugin-konfiguration
Säkerställ att AppHost är konfigurerat med MailPlugin för e-postleverans. E-postverifiering sker genom AI-agentens bekräftelser, inte direkt IMAP-kontroll.

### 3. Playwright-webbläsare
Testerna installerar automatiskt Playwright-webbläsare, men du kan också installera dem manuellt:

```bash
dotnet tool install --global Microsoft.Playwright.CLI
playwright install
```

## Köra testerna

### Alla E2E-tester
```bash
cd tests/AnalysisPlugin.E2E
dotnet test
```

### Endast naturliga språktester
```bash
dotnet test --filter "Category=NaturalLanguage"
```

### Endast API-tester
```bash
dotnet test --filter "Category=API"
```

### Specifik test
```bash
dotnet test --filter "NaturalLanguageRequest_ComprehensiveReport2024_GeneratesAndEmailsReport"
```

## Testscenarier

### 1. Naturliga språkförfrågningar (WebChat)

Testar de exakta exemplen från `specs/001-analysis-plugin/spec.md`:

#### Test 1: Omfattande rapport 2024
```
"Jag vill att du skapar en otroligt snygg ekonomirapport för 2024 och skickar till demo@example.com som kan se samtliga projekt och som summerar intäkter och kostnader och som går att filtrera på datum, snygg startsida, diagram på sida två och interagerbar tabell på sidan tre."
```

#### Test 2: Femårs pivotrapport
```
"Jag vill att du skapar en otroligt snygg ekonomirapport för 2020, 2021, 2022, 2023 och 2024 och skickar till demo@example.com som kan visa en pivot som visar omsättning, varukostnad, övriga externa kostnader, personalkostnader, avskrivningar, finansiella kostnader uppdelat på kvartal för de senaste 5 åren, snygg startsida, en sida för varje år samt en sida för pivoten."
```

#### Test 3: Veckovis bruttovinstrapport
```
"Jag vill att du skapar en otroligt snygg ekonomirapport för 2024 och skickar till demo@example.com som visar omsättning, varukostnad, bruttovinst i SEK, bruttovinst i % för fg vecka, snygg startsida, och en sida för diagrammet."
```

### 2. API-tester

Testar direkta API-anrop för att verifiera:
- Rapport-jobb skapas korrekt
- Komponenter läggs till framgångsrikt
- Excel-filer genereras med korrekt innehåll
- E-post skickas till rätt mottagare

## Testdata

Testerna använder realistisk SIE4-data från `test-data/sample-financial-data.sie` som innehåller:
- Demo Ekonomi AB:s finansiella data
- Tre projekt (Alpha, Beta, Gamma)
- Transaktioner för hela 2024 uppdelade på kvartal
- BAS-kontoplaner enligt svenska redovisningsstandarder

## Felsökning

### AppHost inte tillgänglig
```
Error: AppHost är inte tillgänglig på https://localhost:7001
```
**Lösning**: Säkerställ att AppHost körs i en separat terminal.

### AI-agent svarar inte som förväntat
```
Error: AI-agenten ska bekräfta att e-post skickats till demo@example.com
```
**Lösningar**:
1. Kontrollera att MailPlugin är korrekt konfigurerat i AppHost
2. Verifiera att AI-agenten har tillgång till AnalysisPlugin
3. Kontrollera AppHost-loggar för e-postleveransfel

### Playwright-fel
```
Error: Browser not found
```
**Lösning**: 
```bash
playwright install
```

### WebChat-inloggning misslyckas
```
Error: Login required but credentials not working
```
**Lösning**: Uppdatera användaruppgifter i `appsettings.json`:
```json
{
  "E2ETests": {
    "TestUser": {
      "Username": "isa72",
      "Password": "correct-password"
    }
  }
}
```

## Testresultat

Testerna genererar:
- **Playwright-spårningar**: `trace-{testname}-{timestamp}.zip`
- **Skärmdumpar**: `error-{testname}-{timestamp}.png` (vid fel)
- **Testrapporter**: Sparas i `./test-reports/`

## Kontinuerlig integration

För CI/CD-pipelines, säkerställ att:
1. AppHost startas innan testerna
2. E-postkonfiguration är korrekt inställd
3. Playwright-webbläsare är installerade
4. Testmiljön har tillgång till IMAP-server

Exempel GitHub Actions:
```yaml
- name: Start AppHost
  run: |
    cd src/AppHost
    dotnet run &
    sleep 30

- name: Run E2E Tests
  run: |
    cd tests/AnalysisPlugin.E2E
    dotnet test --logger trx --results-directory TestResults
```
