# 🎯 Report Generation Workflow Test

## Översikt

Detta E2E-test validerar den kompletta rapportgenereringsprocessen genom att:
1. Starta AppHost automatiskt
2. Navigera till WebChat med Playwright
3. Skicka 5 olika rapportförfrågningar sekventiellt
4. Validera att varje rapport skapas exakt enligt specifikation
5. Kontrollera att mail skickas till Anna Andersson
6. Verifiera att rapporter sparas i korrekt mapp
7. Validera att rapportdata stämmer exakt med underlag

## 🔧 Testfunktioner

### **Automatisk AppHost-hantering**
- Kontrollerar om AppHost redan körs
- Startar ny AppHost-instans om behövs
- Väntar på att systemet ska vara redo
- Stänger AppHost efter testet

### **Isolerade Browser-sessioner**
- Varje rapport testas i helt isolerad kontext
- Förhindrar konversationshistorik-kontaminering
- Säkerställer att varje rapport är unik
- Tar skärmdumpar för dokumentation

### **Exakt Datavalidering**
- Öppnar och parsar genererade Excel-filer
- Kontrollerar rapportstruktur (startsida + dataflikar)
- Validerar att obligatoriska element finns
- Jämför mot källdata (SIE4) för exakthet
- Säkerställer att inga siffror skiljer sig

## 📊 Testfall

### 1. **Utgiftsrapport 2023**
```
"Skapa en professionell utgiftsrapport för 2023 med prydlig startsida och sedan sidor för diagram och pivot-tabeller och skicka till Anna Andersson"
```
**Validering:**
- ✅ Startsida som första flik
- ✅ Diagram för utgiftsfördelning
- ✅ Pivot-tabeller för kostnadsanalys
- ✅ Data endast för 2023

### 2. **Projektrapport 2024**
```
"Skapa en professionell rapport för 2024 med prydlig startsida och sedan sidor där du kan se samtliga projekt och som summerar intäkter och kostnader och som går att filtrera på datum och skicka till Anna Andersson"
```
**Validering:**
- ✅ Startsida som första flik
- ✅ Projektöversikt med alla projekt
- ✅ Intäkts- och kostnadssammanfattning
- ✅ Datumfiltreringsmöjlighet
- ✅ Data endast för 2024

### 3. **5-års Kvartalsrapport**
```
"Skapa en professionell rapport med prydlig startsida och sedan sidor för pivot som visar omsättning, varukostnad, övriga externa kostnader, personalkostnader, avskrivningar, finansiella kostnader uppdelat på kvartal för de senaste 5 åren och skicka till Anna Andersson"
```
**Validering:**
- ✅ Startsida som första flik
- ✅ Kvartalsuppdelning för 5 år
- ✅ Alla specificerade kostnadskategorier
- ✅ Pivot-tabellformat

### 4. **Veckorapport 2022**
```
"Skapa en professionell rapport för 2022 med prydlig startsida och sedan sidor som visar omsättning, varukostnad, bruttovinst i SEK, bruttovinst i % för fg vecka och skicka till Anna Andersson"
```
**Validering:**
- ✅ Startsida som första flik
- ✅ Veckoanalys för föregående vecka
- ✅ Bruttovinst i både SEK och procent
- ✅ Data endast för 2022

### 5. **Framtidsrapport 2026**
```
"Skapa en professionell analys/framtidsrapport för 2026 med prydlig startsida och sedan sidor som visar omsättning, varukostnad, bruttovinst i SEK och skicka till Anna Andersson"
```
**Validering:**
- ✅ Startsida som första flik
- ✅ Framtidsanalys och prognoser
- ✅ Projektioner för 2026
- ✅ Omsättning, kostnader och bruttovinst

## 🚀 Köra Testet

### Förutsättningar
- **Projektmapp:** `/path/to/accountingagent`
- **Reports-mapp:** `/path/to/accountingagent/analysis_reports/`
- **Playwright:** Installerat och konfigurerat

### Alternativ 1: Test Runner Script
```bash
./tests/E2E/run-report-workflow-test.sh
```

### Alternativ 2: Direkt dotnet test
```bash
dotnet test tests/E2E --filter "ReportGenerationWorkflow_ShouldCreateExactReportsWithCorrectData" --logger "console;verbosity=detailed"
```

### Förväntad Tid
- **Total tid:** 15-20 minuter
- **Per rapport:** 3-4 minuter
- **Inkluderar:** AI-bearbetning, filgenerering, validering

## ✅ Framgångskriterier

### **Kritiska Krav (MÅSTE uppfyllas)**
- **80% framgångsgrad:** Minst 4 av 5 rapporter måste lyckas
- **Exakt datamatchning:** 80% av rapporterna måste ha korrekt data
- **Mail-leverans:** Bekräftelse att mail skickats till Anna Andersson
- **Filskapande:** Rapporter måste finnas i korrekt mapp
- **Strukturvalidering:** Startsida + dataflikar enligt förfrågan

### **Kvalitetsmått**
- **Innehållsuniqueness:** Varje rapport innehåller endast relevant data
- **Strukturkompatibilitet:** Matchar begärd format exakt
- **Dataintegritet:** Alla belopp och datum är korrekta
- **Professionell kvalitet:** Rapporter lämpliga för affärsbruk

## 🔍 Validering

### **Strukturvalidering**
- Kontrollerar att startsida finns som första flik
- Verifierar minsta antal flikar (minst 2)
- Säkerställer att dataflikar har substantiellt innehåll

### **Innehållsvalidering**
- Kontrollerar att alla obligatoriska element finns
- Validerar årspecifik data
- Säkerställer att finansiell data är rimlig
- Jämför mot källdata för exakthet

### **Kritisk Datajämförelse**
- **INGEN DIFF TILLÅTEN** mellan förfrågan och resultat
- Alla siffror måste stämma exakt med SIE4-underlag
- Datum och kontokoder måste vara korrekta
- Belopp måste vara exakta (inga avrundningsfel)

## 📁 Testartifakter

### **Genererade Filer**
- **Skärmdumpar:** `rapport-*.png`
- **Excel-rapporter:** `/analysis_reports/*.xlsx`
- **Detaljerade loggar:** Konsolutskrift

### **Felsökning**
- **Skärmdumpar:** Visuell bekräftelse av UI-interaktioner
- **Excel-filer:** Manuell inspektion av rapportinnehåll
- **Loggar:** Steg-för-steg valideringsresultat
- **Felmeddelanden:** Specifika anledningar till misslyckanden

Detta test säkerställer att rapportgenereringssystemet fungerar end-to-end med verkliga användarscenarier och validerar både teknisk funktionalitet och affärskrav med **NOLL TOLERANS** för datafel.
