#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Kör AnalysisPlugin E2E-tester med automatisk AppHost-start

.DESCRIPTION
    Detta script:
    1. Startar AppHost i bakgrunden (med Agent och MailPlugin)
    2. Väntar på att AppHost blir tillgänglig
    3. Kör E2E-testerna som testar naturliga språkförfrågningar
    4. Stoppar AppHost efter testerna
    5. Visar testresultat och genererade filer

.PARAMETER TestFilter
    Filter för vilka tester som ska köras (valfritt)

.PARAMETER SkipAppHost
    Hoppa över att starta AppHost (om den redan körs)

.PARAMETER Verbose
    Visa detaljerad loggning

.EXAMPLE
    .\run-e2e-tests.ps1
    Kör alla E2E-tester

.EXAMPLE
    .\run-e2e-tests.ps1 -TestFilter "NaturalLanguage"
    Kör endast naturliga språktester

.EXAMPLE
    .\run-e2e-tests.ps1 -SkipAppHost
    Kör tester utan att starta AppHost (förutsätter att den redan körs)
#>

param(
    [string]$TestFilter = "",
    [switch]$SkipAppHost = $false,
    [switch]$Verbose = $false
)

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

function Test-AppHostRunning {
    param([string]$Url)
    
    try {
        $response = Invoke-WebRequest -Uri "$Url/health" -TimeoutSec 5 -UseBasicParsing
        return $response.StatusCode -eq 200
    }
    catch {
        return $false
    }
}

function Wait-ForAppHost {
    param([string]$Url, [int]$MaxAttempts = 20)
    
    Write-ColorOutput "🔍 Väntar på att AppHost blir tillgänglig på $Url..." $Blue
    
    for ($i = 1; $i -le $MaxAttempts; $i++) {
        if (Test-AppHostRunning -Url $Url) {
            Write-ColorOutput "✅ AppHost är tillgänglig!" $Green
            return $true
        }
        
        Write-ColorOutput "⏳ Försök $i/$MaxAttempts - väntar 3 sekunder..." $Yellow
        Start-Sleep -Seconds 3
    }
    
    Write-ColorOutput "❌ AppHost blev inte tillgänglig efter $MaxAttempts försök" $Red
    return $false
}

# Huvudscript
Write-ColorOutput "🚀 Startar AnalysisPlugin E2E-tester..." $Blue
Write-ColorOutput "📁 Arbetskatalog: $(Get-Location)" $Blue

# Kontrollera att vi är i rätt katalog
if (-not (Test-Path "AnalysisPlugin.E2E.csproj")) {
    Write-ColorOutput "❌ Fel katalog! Kör scriptet från tests/AnalysisPlugin.E2E/" $Red
    exit 1
}

# Konfigurera AppHost
$AppHostPath = "..\..\src\AppHost"
$AppHostUrl = "https://localhost:7001"
$AppHostProcess = $null

try {
    # Starta AppHost om det inte ska hoppas över
    if (-not $SkipAppHost) {
        if (Test-AppHostRunning -Url $AppHostUrl) {
            Write-ColorOutput "✅ AppHost körs redan på $AppHostUrl" $Green
        }
        else {
            Write-ColorOutput "🚀 Startar AppHost..." $Blue
            
            if (-not (Test-Path $AppHostPath)) {
                Write-ColorOutput "❌ AppHost-katalog hittades inte: $AppHostPath" $Red
                exit 1
            }
            
            # Starta AppHost i bakgrunden
            $AppHostProcess = Start-Process -FilePath "dotnet" -ArgumentList "run" -WorkingDirectory $AppHostPath -PassThru -WindowStyle Hidden
            
            # Vänta på att AppHost blir tillgänglig
            if (-not (Wait-ForAppHost -Url $AppHostUrl)) {
                Write-ColorOutput "❌ Kunde inte starta AppHost" $Red
                exit 1
            }
        }
    }
    else {
        Write-ColorOutput "⏭️ Hoppar över AppHost-start (--SkipAppHost angiven)" $Yellow
        
        if (-not (Test-AppHostRunning -Url $AppHostUrl)) {
            Write-ColorOutput "❌ AppHost körs inte på $AppHostUrl. Starta den manuellt eller ta bort --SkipAppHost" $Red
            exit 1
        }
    }

    # Bygg testprojektet
    Write-ColorOutput "🔨 Bygger testprojekt..." $Blue
    $buildResult = dotnet build --configuration Release
    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "❌ Build misslyckades" $Red
        exit 1
    }

    # Installera Playwright-webbläsare
    Write-ColorOutput "🌐 Installerar Playwright-webbläsare..." $Blue
    try {
        & dotnet tool install --global Microsoft.Playwright.CLI 2>$null
        & playwright install 2>$null
        Write-ColorOutput "✅ Playwright-webbläsare installerade" $Green
    }
    catch {
        Write-ColorOutput "⚠️ Varning: Kunde inte installera Playwright-webbläsare automatiskt" $Yellow
    }

    # Kör testerna
    Write-ColorOutput "🧪 Kör E2E-tester..." $Blue
    
    $testArgs = @("test", "--configuration", "Release", "--logger", "console;verbosity=normal")
    
    if ($TestFilter) {
        $testArgs += "--filter"
        $testArgs += $TestFilter
        Write-ColorOutput "🔍 Testfilter: $TestFilter" $Blue
    }
    
    if ($Verbose) {
        $testArgs += "--verbosity"
        $testArgs += "diagnostic"
    }

    $testStartTime = Get-Date
    & dotnet @testArgs
    $testExitCode = $LASTEXITCODE
    $testEndTime = Get-Date
    $testDuration = $testEndTime - $testStartTime

    # Visa testresultat
    Write-ColorOutput "`n📊 Testresultat:" $Blue
    Write-ColorOutput "⏱️ Testtid: $($testDuration.ToString('mm\:ss'))" $Blue
    
    if ($testExitCode -eq 0) {
        Write-ColorOutput "✅ Alla tester lyckades!" $Green
    }
    else {
        Write-ColorOutput "❌ Vissa tester misslyckades (exit code: $testExitCode)" $Red
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
        Write-ColorOutput "📸 Skärmdumpar (fel):" $Blue
        $screenshotFiles | ForEach-Object { Write-ColorOutput "  - $($_.Name)" $Red }
    }
    
    $reportFiles = Get-ChildItem -Path "test-reports" -Filter "*.xlsx" -ErrorAction SilentlyContinue
    if ($reportFiles) {
        Write-ColorOutput "📊 Testrapporter:" $Blue
        $reportFiles | ForEach-Object { Write-ColorOutput "  - $($_.Name)" $Green }
    }

    exit $testExitCode
}
catch {
    Write-ColorOutput "❌ Oväntat fel: $($_.Exception.Message)" $Red
    exit 1
}
finally {
    # Stoppa AppHost om vi startade den
    if ($AppHostProcess -and -not $AppHostProcess.HasExited) {
        Write-ColorOutput "🛑 Stoppar AppHost..." $Blue
        try {
            $AppHostProcess.Kill()
            $AppHostProcess.WaitForExit(5000)
            Write-ColorOutput "✅ AppHost stoppad" $Green
        }
        catch {
            Write-ColorOutput "⚠️ Kunde inte stoppa AppHost korrekt" $Yellow
        }
    }
    
    Write-ColorOutput "🏁 E2E-tester avslutade" $Blue
}
