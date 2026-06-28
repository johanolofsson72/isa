#!/bin/bash

# 🎯 Report Generation Workflow Test Runner
# Kör komplett E2E-test för rapportgenerering med AppHost och WebChat

echo "🎯 REPORT GENERATION WORKFLOW TEST"
echo "=================================="
echo ""

# Kontrollera att vi är i rätt mapp
if [ ! -f "src/AppHost/AppHost.csproj" ]; then
    echo "❌ FEL: Kör detta skript från projektets rotmapp"
    echo "   cd /path/to/accountingagent"
    echo "   ./tests/E2E/run-report-workflow-test.sh"
    exit 1
fi

# Säkerställ att reports-mappen finns
REPORTS_DIR="/path/to/accountingagent/analysis_reports"
echo "📁 Säkerställer att reports-mappen finns: $REPORTS_DIR"
mkdir -p "$REPORTS_DIR"

# Rensa gamla testfiler
echo "🧹 Rensar gamla testfiler..."
rm -f rapport-*.png
rm -f comprehensive-test-*.png

# Kontrollera om AppHost redan körs
echo "🔍 Kontrollerar om AppHost redan körs..."
if curl -s -k https://localhost:7041 > /dev/null 2>&1; then
    echo "✅ AppHost körs redan"
    APPHOST_RUNNING=true
else
    echo "⚠️ AppHost körs inte - testet kommer starta en egen instans"
    APPHOST_RUNNING=false
fi

echo ""
echo "🚀 Startar Fokuserad Report Generation Test..."
echo "📊 Testar 1 rapport: Utgiftsrapport 2023"
echo "⏱️ Förväntad tid: 3-5 minuter"
echo "🔍 Rapporten valideras mot exakta krav"
echo "⚡ Stegvis testning - övriga rapporter inaktiverade"
echo ""

# Kör testet
dotnet test tests/E2E --filter "ReportGenerationWorkflow_ShouldCreateExactReportsWithCorrectData" --logger "console;verbosity=detailed"

TEST_RESULT=$?

echo ""
echo "📋 TEST SLUTFÖRT"
echo "================"

if [ $TEST_RESULT -eq 0 ]; then
    echo "🎉 FRAMGÅNG: Utgiftsrapporten fungerar perfekt!"
    echo ""
    echo "📁 Genererade filer:"
    echo "   Skärmdump: rapport-1-utgiftsrapport-2023.png"
    echo "   Rapport: $REPORTS_DIR/*.xlsx"
    echo ""
    echo "🔍 Kontrollera testutskriften ovan för detaljerade valideringsresultat."
    echo ""
    echo "✅ KRITISKA VALIDERINGAR PASSERADE:"
    echo "   📧 Mail skickat till Anna Andersson"
    echo "   📁 Utgiftsrapport skapad i korrekt mapp"
    echo "   🏗️ Rapportstruktur korrekt (startsida + diagram + pivot)"
    echo "   📊 Utgiftsdata för 2023 stämmer exakt"
    echo "   🔍 Exakt datamatchning mot SIE4-underlag"
    echo ""
    echo "🚀 NÄSTA STEG: Aktivera rapport 2 (Projektrapport 2024)"
else
    echo "❌ MISSLYCKANDE: Utgiftsrapporten fungerar inte korrekt!"
    echo ""
    echo "🔧 Felsökningssteg för Utgiftsrapport 2023:"
    echo "1. Kontrollera att AppHost körs korrekt"
    echo "2. Verifiera att WebChat är tillgänglig på https://localhost:7041"
    echo "3. Kontrollera detaljerad testutskrift ovan för specifika fel"
    echo "4. Granska skärmdump: rapport-1-utgiftsrapport-2023.png"
    echo "5. Kontrollera att AnalysisPlugin hanterar 'expenses' korrekt"
    echo ""
    echo "📊 KRITISKA OMRÅDEN FÖR UTGIFTSRAPPORT:"
    echo "   📧 Mail till Anna Andersson"
    echo "   📁 Excel-fil i reports-mappen"
    echo "   🏗️ Startsida som första flik"
    echo "   📊 Diagram för utgiftsfördelning"
    echo "   📋 Pivot-tabeller för kostnadsanalys"
    echo "   🗓️ Endast data för 2023"
fi

echo ""
echo "📊 Testartifakter:"
echo "   Skärmdumpar: $(pwd)/"
echo "   Rapporter: $REPORTS_DIR/"
echo "   Loggar: Se konsolutskrift ovan"
echo ""

if [ $TEST_RESULT -eq 0 ]; then
    echo "🏆 UTGIFTSRAPPORT KVALITETSKONTROLL GODKÄND"
    echo "   Utgiftsrapport 2023 skapad exakt som begärt"
    echo "   Ingen diff mellan förfrågan och resultat"
    echo "   Redo att aktivera nästa rapport (Projektrapport 2024)"
else
    echo "⚠️ UTGIFTSRAPPORT KVALITETSKONTROLL MISSLYCKAD"
    echo "   Utgiftsrapporten stämmer inte med förfrågan"
    echo "   Måste fixas innan vi går vidare till nästa rapport"
fi

exit $TEST_RESULT
