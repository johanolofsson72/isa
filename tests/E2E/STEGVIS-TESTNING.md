# 🎯 Stegvis Rapportgenerering Testning

## Nuvarande Status: Fokus på Rapport 1

### ✅ **AKTIVERAD RAPPORT:**
**1. Utgiftsrapport 2023**
```
"Skapa en professionell utgiftsrapport för 2023 med prydlig startsida och sedan sidor för diagram och pivot-tabeller och skicka till Anna Andersson"
```

**Validering:**
- ✅ Startsida som första flik
- ✅ Diagram för utgiftsfördelning  
- ✅ Pivot-tabeller för kostnadsanalys
- ✅ Endast data för 2023
- ✅ Mail till Anna Andersson
- ✅ Fil sparad i `/analysis_reports/`

### 🚫 **INAKTIVERADE RAPPORTER:**
2. Projektrapport 2024 (väntar)
3. 5-års Kvartalsrapport (väntar)
4. Veckorapport 2022 (väntar)
5. Framtidsrapport 2026 (väntar)

## 🚀 Köra Testet

```bash
./tests/E2E/run-report-workflow-test.sh
```

**Förväntad tid:** 3-5 minuter  
**Krav för framgång:** 100% (utgiftsrapporten måste fungera perfekt)

## 📊 När Rapport 1 Fungerar

När utgiftsrapporten passerar alla tester:

### **Steg 1: Aktivera Rapport 2**
I `tests/E2E/ReportGenerationWorkflowTest.cs`, ta bort kommentarerna runt:
```csharp
,new ReportRequest
{
    Id = 2,
    Name = "Projektrapport 2024",
    Text = "Skapa en professionell rapport för 2024 med prydlig startsida och sedan sidor där du kan se samtliga projekt och som summerar intäkter och kostnader och som går att filtrera på datum och skicka till Anna Andersson",
    ExpectedYear = 2024,
    RequiredElements = new[] { "startsida", "projekt", "intäkter", "kostnader", "filtrera", "datum" },
    ExpectedSheets = new[] { "Startsida", "Projekt", "Intäkter och Kostnader" },
    ReportType = "projects"
}
```

### **Steg 2: Uppdatera Testbeskrivning**
Ändra från "1 rapport" till "2 rapporter" i testutskrifterna.

### **Steg 3: Kör Test Igen**
```bash
./tests/E2E/run-report-workflow-test.sh
```

### **Steg 4: Repetera för Rapport 3, 4, 5**
Aktivera en rapport i taget tills alla fungerar.

## 🔍 Felsökning per Rapport

### **Rapport 1: Utgiftsrapport 2023**
**Vanliga problem:**
- AI använder inte `reportType='expenses'` korrekt
- Startsida saknas som första flik
- Diagram för utgifter genereras inte
- Pivot-tabeller för kostnader saknas
- Data från fel år inkluderas

**Kontrollera:**
- AnalysisPlugin anropas med rätt parametrar
- CreateFinancialReportAsync får `reportType='expenses'`
- AddFrontPageAsync anropas för startsida
- AddChartAsync anropas med `chartType='expense_breakdown'`
- AddPivotTableAsync anropas med `pivotType='account_breakdown'`

### **Rapport 2: Projektrapport 2024** (när aktiverad)
**Förväntade problem:**
- AI kanske inte förstår "projekt" → `reportType='projects'`
- Filtreringsmöjlighet kanske inte implementeras
- Intäkts/kostnadssammanfattning kanske saknas

### **Rapport 3-5:** (när aktiverade)
Liknande mönster - kontrollera att AI använder rätt parametrar för varje rapporttyp.

## 📈 Framstegsspårning

- [ ] **Rapport 1:** Utgiftsrapport 2023 ✅ (FOKUS NU)
- [ ] **Rapport 2:** Projektrapport 2024 (nästa)
- [ ] **Rapport 3:** 5-års Kvartalsrapport (sedan)
- [ ] **Rapport 4:** Veckorapport 2022 (sedan)
- [ ] **Rapport 5:** Framtidsrapport 2026 (sist)

## 🎯 Mål

**Slutmål:** Alla 5 rapporter fungerar perfekt med 100% framgång och exakt datamatchning.

**Strategi:** En rapport i taget, fixa alla problem innan vi går vidare till nästa.

**Kvalitetskrav:** Varje rapport måste ha:
- Korrekt struktur (startsida + dataflikar)
- Exakt innehåll enligt förfrågan
- Rätt data för rätt tidsperiod
- Mail till Anna Andersson
- Fil sparad i korrekt mapp
