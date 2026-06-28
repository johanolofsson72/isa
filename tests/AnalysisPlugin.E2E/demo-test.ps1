#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Demo av AnalysisPlugin E2E-tester

.DESCRIPTION
    Detta script demonstrerar E2E-testerna genom att:
    1. Starta AppHost med Agent och MailPlugin
    2. Köra ett enkelt naturligt språktest
    3. Visa resultatet
#>

# Färger för output
$Green = "`e[32m"
$Red = "`e[31m"
$Yellow = "`e[33m"
$Blue = "`e[34m"
$Reset = "`e[0m"

function Write-ColorOutput {
    param([string]$Message, [string]$Color = $Reset)
    Write-Host "$Color$Message$Reset"
}

Write-ColorOutput "🎯 AnalysisPlugin E2E Test Demo" $Blue
Write-ColorOutput "================================" $Blue

# Kontrollera att vi är i rätt katalog
if (-not (Test-Path "AnalysisPlugin.E2E.csproj")) {
    Write-ColorOutput "❌ Fel katalog! Kör scriptet från tests/AnalysisPlugin.E2E/" $Red
    exit 1
}

Write-ColorOutput "`n📋 Detta demo kommer att:" $Blue
Write-ColorOutput "  1. Starta AppHost med Agent och MailPlugin" $Yellow
Write-ColorOutput "  2. Köra naturligt språktest från specifikationen" $Yellow
Write-ColorOutput "  3. Verifiera att AI-agenten svarar korrekt" $Yellow
Write-ColorOutput "  4. Kontrollera att MailPlugin integreras för e-postleverans" $Yellow

Write-ColorOutput "`n🔍 Naturligt språktest som kommer köras:" $Blue
Write-ColorOutput "  'Jag vill att du skapar en otroligt snygg ekonomirapport för 2024" $Green
Write-ColorOutput "   och skickar till demo@example.com som kan se samtliga projekt och som" $Green
Write-ColorOutput "   summerar intäkter och kostnader...'" $Green

Write-ColorOutput "`n⚠️  Viktigt: AppHost måste vara konfigurerat med:" $Yellow
Write-ColorOutput "  - Agent för AI-orkestrering" $Yellow
Write-ColorOutput "  - MailPlugin för e-postleverans" $Yellow
Write-ColorOutput "  - AnalysisPlugin för rapportgenerering" $Yellow

Write-ColorOutput "`nTryck Enter för att fortsätta eller Ctrl+C för att avbryta..." $Blue
Read-Host

try {
    # Bygg testprojektet
    Write-ColorOutput "`n🔨 Bygger testprojekt..." $Blue
    $buildResult = dotnet build --configuration Release --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "❌ Build misslyckades" $Red
        exit 1
    }
    Write-ColorOutput "✅ Build slutförd" $Green

    # Kör ett specifikt test som demo
    Write-ColorOutput "`n🧪 Kör demo-test..." $Blue
    Write-ColorOutput "Filter: NaturalLanguageRequest_ComprehensiveReport2024_GeneratesAndEmailsReport" $Yellow
    
    $testArgs = @(
        "test", 
        "--configuration", "Release",
        "--filter", "NaturalLanguageRequest_ComprehensiveReport2024_GeneratesAndEmailsReport",
        "--logger", "console;verbosity=normal",
        "--verbosity", "normal"
    )

    $testStartTime = Get-Date
    & dotnet @testArgs
    $testExitCode = $LASTEXITCODE
    $testEndTime = Get-Date
    $testDuration = $testEndTime - $testStartTime

    Write-ColorOutput "`n📊 Demo-resultat:" $Blue
    Write-ColorOutput "⏱️  Testtid: $($testDuration.ToString('mm\:ss'))" $Blue
    
    if ($testExitCode -eq 0) {
        Write-ColorOutput "✅ Demo-test lyckades!" $Green
        Write-ColorOutput "`n🎉 Framgångsrikt demonstrerat:" $Green
        Write-ColorOutput "  ✅ Naturlig språkförfrågan fungerar" $Green
        Write-ColorOutput "  ✅ AI-agent svarar korrekt" $Green
        Write-ColorOutput "  ✅ MailPlugin-integration verifierad" $Green
        Write-ColorOutput "  ✅ Rapportgenerering slutförd" $Green
    }
    else {
        Write-ColorOutput "❌ Demo-test misslyckades (exit code: $testExitCode)" $Red
        Write-ColorOutput "`n🔍 Möjliga orsaker:" $Yellow
        Write-ColorOutput "  - AppHost körs inte" $Yellow
        Write-ColorOutput "  - Agent inte konfigurerat" $Yellow
        Write-ColorOutput "  - MailPlugin inte tillgängligt" $Yellow
        Write-ColorOutput "  - AnalysisPlugin inte registrerat" $Yellow
    }

    # Visa genererade filer
    Write-ColorOutput "`n📁 Genererade filer:" $Blue
    
    $traceFiles = Get-ChildItem -Path "." -Filter "trace-*.zip" -ErrorAction SilentlyContinue
    if ($traceFiles) {
        Write-ColorOutput "🔍 Playwright-spårningar:" $Blue
        $traceFiles | ForEach-Object { Write-ColorOutput "  - $($_.Name)" $Yellow }
    }
    
    $screenshotFiles = Get-ChildItem -Path "." -Filter "error-*.png" -ErrorAction SilentlyContinue
    if ($screenshotFiles) {
        Write-ColorOutput "📸 Skärmdumpar:" $Blue
        $screenshotFiles | ForEach-Object { Write-ColorOutput "  - $($_.Name)" $Red }
    }

    Write-ColorOutput "`n💡 För att köra alla tester:" $Blue
    Write-ColorOutput "  .\run-e2e-tests.ps1" $Yellow

    exit $testExitCode
}
catch {
    Write-ColorOutput "❌ Oväntat fel: $($_.Exception.Message)" $Red
    exit 1
}
finally {
    Write-ColorOutput "`n🏁 Demo avslutat" $Blue
}
