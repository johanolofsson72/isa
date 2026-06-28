# AccountingAgent - Integration Tests & System Demonstration

Detta är en omfattande testsvit som demonstrerar alla systemets kapaciteter för svenska redovisningsbyråer. Testerna använder **verklig SIE4-data från Demo Ekonomi AB** för att visa hur systemet integrerar med Fortnox, bearbetar finansiell data och genererar professionella rapporter med AI-driven orchestration.

## 🎯 Vad Testerna Demonstrerar

### 📊 Real SIE4 Data Processing
- ✅ Bearbetar verklig SIE4-data från Demo Ekonomi AB (556677-8899)
- ✅ Hanterar svenska BAS-kontoplaner och transaktioner
- ✅ Validerar och rensar finansiell data automatiskt
- ✅ Beräknar nyckeltal och finansiella mått från verklig data

### 🤖 ML-Enhanced Financial Analysis
- ✅ Maskininlärning för finansiell prognostisering
- ✅ Trendanalys och säsongsmönster
- ✅ Anomalidetektering i transaktioner
- ✅ Riskbedömning och finansiell hälsoanalys
- ✅ Avancerad datafiltrering och validering

### 📈 Advanced Excel Generation
- ✅ Multi-sheet rapporter med professionell styling
- ✅ Svenska lokaliseringar (valuta, datum, språk)
- ✅ Interaktiva pivottabeller och diagram
- ✅ Villkorlig formatering för finansiella regler
- ✅ Navigering mellan arbetsblad

### 🧠 AI-Driven Orchestration
- ✅ Naturlig språkbearbetning på svenska
- ✅ Kontextuell rapportgenerering baserat på målgrupp
- ✅ Komplexa affärsfrågor och intelligenta svar
- ✅ Anpassning av rapportstil efter användare
- ✅ Multi-steg arbetsflöden

### User Story 3: Advanced Visualization
- ✅ Dynamiska diagram och grafer
- ✅ Interaktiva pivot-tabeller
- ✅ Svenska lokaliserade visualiseringar
- ✅ Excel-rendering med professionell formatering

### User Story 4: Multi-Sheet Professional Reports
- ✅ Omfattande multi-sheet rapporter
- ✅ Professionell e-postleverans
- ✅ Rapportarkivering och versionshantering
- ✅ BAS-kompatibla redovisningsrapporter

### User Story 5: AI Agent Orchestration
- ✅ Naturlig språkförståelse (svenska/engelska)
- ✅ Automatisk rapportschemaläggning
- ✅ Prestationsoptimering för stora datamängder
- ✅ Executive dashboards och ledningsrapporter
- ✅ Intelligent felhantering och återhämtning

## 🚀 Köra Testerna

### Förutsättningar
- .NET 8.0 SDK
- Visual Studio 2022 eller VS Code
- Verklig SIE4-data från Demo Ekonomi AB (inkluderat i `/SIE4` katalogen)

### Snabbstart - Kör Komplett Demonstration
```bash
# Navigera till testmappen
cd tests/AnalysisPlugin.IntegrationTests

# Bygg projektet
dotnet build

# Kör komplett kunddemonstration (rekommenderat)
dotnet test --filter "ComprehensiveTestSuite" --logger "console;verbosity=detailed"

# Kör specifika testgrupper
dotnet test --filter "RealSIE4DataTests" --logger "console;verbosity=detailed"
dotnet test --filter "MLAnalysisTests" --logger "console;verbosity=detailed"
dotnet test --filter "ExcelGenerationTests" --logger "console;verbosity=detailed"
dotnet test --filter "AIOrchestrationTests" --logger "console;verbosity=detailed"

# Kör alla tester
dotnet test --logger "console;verbosity=detailed"
```

### Testdata
Testerna använder verklig SIE4-data från:
- **Företag**: Demo Ekonomi AB
- **Organisationsnummer**: 556677-8899
- **Dataperiod**: 2024
- **Filformat**: SIE4 (Standard Import Export format)
- **Källa**: Fortnox redovisningssystem

### Visual Studio
1. Öppna solution i Visual Studio
2. Bygg projektet
3. Kör testerna via Test Explorer
4. Eller kör Program.cs för fullständig demonstration

## 📊 Testresultat

Testerna genererar detaljerade loggar som visar:

```
🚀 STARTING COMPREHENSIVE SYSTEM DEMONSTRATION
================================================================================

🧪 RUNNING TEST SUITE: User Story Demonstrations
--------------------------------------------------------------------------------
🎯 DEMO: User Story 1 - Swedish Accounting Firm processes SIE4 data
📄 Received SIE4 data from client's Fortnox system (2,847 characters)
✅ SIE4 Processing Results:
   • Company: Test AB (Org: 5566778899)
   • Accounts processed: 45
   • Transactions processed: 8
   • Financial Year: 2024-01-01 - 2024-12-31
💰 Key Financial Metrics:
   • Total Revenue: 170,000.00 SEK
   • Total Expenses: 298,275.00 SEK
   • Net Profit: -128,275.00 SEK
   • Profit Margin: -75.46%
🏛️ BAS Account Compliance: 3 BAS-compliant accounts found
```

## 🎯 Kunddemonstration

Testerna är designade för att visa kunder systemets kapaciteter:

### Scenario 1: Redovisningsbyrå Mottar SIE4-data
```csharp
// Simulerar mottagning av SIE4-data från Fortnox
var sie4Data = await fortnoxPlugin.GetSIEAsync(2024);
var result = await sie4Processor.ProcessSIE4DataAsync(sie4Data);
```

### Scenario 2: AI Förstår Naturligt Språk
```csharp
// Kund ringer och säger:
var request = "Kan ni skapa en månadsrapport för mars med prognoser?";
var result = await orchestrationService.OrchestratReportGenerationAsync(request);
```

### Scenario 3: Automatisk Rapportgenerering
```csharp
// Systemet genererar automatiskt professionella rapporter
var report = await reportEngine.GenerateMultiSheetReportAsync(request);
// Skickar via e-post och arkiverar
```

## 📁 Testdata

### SIE4 Testfil
- `TestData/sample-sie4.se` - Komplett SIE4-fil med svenska BAS-konton
- Innehåller realistiska transaktioner och kontoplaner
- Följer svensk redovisningsstandard

### Mock Services
- `MockEmailService` - Simulerar e-postleverans
- `MockFileStorageService` - Simulerar fillagring
- `TestDataProvider` - Genererar testdata

## 🔧 Konfiguration

### appsettings.Test.json
```json
{
  "AnalysisPlugin": {
    "EnableMLForecasting": true,
    "DefaultLanguage": "sv-SE",
    "EnableBASGrouping": true,
    "EnableAIOrchestration": true
  },
  "TestData": {
    "UseMockData": true,
    "TestCompanyName": "Test AB",
    "TestFinancialYear": 2024
  }
}
```

## 📈 Prestanda

Testerna inkluderar prestandatester för:
- Stora datamängder (10,000+ transaktioner)
- Parallell bearbetning
- Minnesoptimering
- Caching-effektivitet

## 🐛 Felsökning

### Vanliga Problem
1. **SIE4 Parsing Fel**: Kontrollera att testfilen har korrekt format
2. **ML Model Fel**: Säkerställ att tillräckligt med historisk data finns
3. **Excel Generation Fel**: Kontrollera att ClosedXML är korrekt installerat

### Loggar
Alla tester genererar detaljerade loggar som hjälper med felsökning:
```
[INFO] SIE4 processing completed: 45 accounts, 8 transactions
[INFO] Financial metrics calculated: Revenue=170,000 SEK
[ERROR] ML forecast failed: Insufficient historical data
```

## 🎉 Framgång

När alla tester passerar ser du:
```
🎯 ALL TESTS PASSED - SYSTEM IS READY FOR PRODUCTION! 🎯
```

Detta bekräftar att systemet är redo för produktionsanvändning hos svenska redovisningsbyråer.

## 📞 Support

För frågor om testerna eller systemet, kontakta utvecklingsteamet.
