# Comprehensive Report E2E Test Runner
# This script runs the comprehensive report generation tests

param(
    [switch]$Headless = $false,
    [int]$TimeoutMinutes = 30
)

Write-Host "🚀 Starting Comprehensive Report E2E Tests" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green

$RepoRoot = "/path/to/accountingagent"
$ReportsDir = "$RepoRoot/analysis_reports"
$TestProject = "$RepoRoot/tests/E2E"

Write-Host "📁 Repository Root: $RepoRoot" -ForegroundColor Blue
Write-Host "📊 Reports Directory: $ReportsDir" -ForegroundColor Blue
Write-Host "🧪 Test Project: $TestProject" -ForegroundColor Blue

# Ensure reports directory exists
if (!(Test-Path $ReportsDir)) {
    New-Item -ItemType Directory -Path $ReportsDir -Force | Out-Null
}
Write-Host "✅ Reports directory ready" -ForegroundColor Green

# Clean previous test results
if (Test-Path "$ReportsDir/*") {
    Remove-Item "$ReportsDir/*" -Recurse -Force
    Write-Host "✅ Previous reports cleaned" -ForegroundColor Green
}

# Install Playwright browsers if needed
Write-Host "🌐 Installing Playwright browsers..." -ForegroundColor Yellow
Set-Location $TestProject

try {
    # Try to install Playwright browsers
    if (Test-Path "bin/Debug/net9.0/playwright.ps1") {
        & "bin/Debug/net9.0/playwright.ps1" install chromium
    } else {
        # Build first, then install
        dotnet build E2E.csproj
        & "bin/Debug/net9.0/playwright.ps1" install chromium
    }
    Write-Host "✅ Playwright browsers ready" -ForegroundColor Green
} catch {
    Write-Host "⚠️ Playwright browser installation failed: $_" -ForegroundColor Yellow
    Write-Host "Continuing with test run..." -ForegroundColor Yellow
}

# Build the test project
Write-Host "🔨 Building E2E test project..." -ForegroundColor Yellow
dotnet build E2E.csproj
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed" -ForegroundColor Red
    exit 1
}
Write-Host "✅ E2E tests built successfully" -ForegroundColor Green

# Run the comprehensive tests
Write-Host "🧪 Running comprehensive E2E tests..." -ForegroundColor Yellow
Write-Host "This will test all 8 report scenarios:" -ForegroundColor Blue
Write-Host "1. Utgiftsrapport 2023 (med startsida, diagram, pivot)" -ForegroundColor White
Write-Host "2. Projektrapport 2024 (med startsida, filtrering)" -ForegroundColor White
Write-Host "3. Kvartalsrapport 5 år (med startsida, pivot)" -ForegroundColor White
Write-Host "4. Veckorapport 2022 (med startsida, bruttovinst)" -ForegroundColor White
Write-Host "5. Resultatrapport 2024" -ForegroundColor White
Write-Host "6. Årsrapport 2023" -ForegroundColor White
Write-Host "7. Momsrapport Q3" -ForegroundColor White
Write-Host "8. Framtidsanalys 2026 Q1" -ForegroundColor White

# Set test timeout
$env:DOTNET_TEST_TIMEOUT = $TimeoutMinutes * 60

# Run tests with detailed output
$TestArgs = @(
    "test", "E2E.csproj",
    "--logger", "console;verbosity=detailed",
    "--logger", "trx;LogFileName=comprehensive-report-e2e-results.trx",
    "--results-directory", "TestResults",
    "--collect:XPlat Code Coverage",
    "--", "RunConfiguration.TestSessionTimeout=$($TimeoutMinutes * 60 * 1000)"
)

if ($Headless) {
    Write-Host "Running in headless mode..." -ForegroundColor Yellow
}

dotnet @TestArgs

$TestExitCode = $LASTEXITCODE

# Check test results
if ($TestExitCode -eq 0) {
    Write-Host "🎉 All E2E tests passed successfully!" -ForegroundColor Green
    
    # Count generated files
    $ReportFiles = Get-ChildItem -Path $ReportsDir -Recurse -Include "*.xlsx", "*.pdf", "*.json" -ErrorAction SilentlyContinue
    $ReportCount = $ReportFiles.Count
    Write-Host "📊 Generated $ReportCount report files" -ForegroundColor Green
    
    # List generated files
    if ($ReportCount -gt 0) {
        Write-Host "📁 Generated files:" -ForegroundColor Blue
        $ReportFiles | ForEach-Object { Write-Host "  - $($_.Name)" -ForegroundColor White }
    }
    
} else {
    Write-Host "❌ E2E tests failed with exit code $TestExitCode" -ForegroundColor Red
    
    # Show test results if available
    $ResultsFile = "$TestProject/TestResults/comprehensive-report-e2e-results.trx"
    if (Test-Path $ResultsFile) {
        Write-Host "📋 Test results available in: $ResultsFile" -ForegroundColor Yellow
    }
}

Write-Host "🏁 E2E test run completed" -ForegroundColor Blue
Set-Location $RepoRoot
exit $TestExitCode
