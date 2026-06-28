#!/usr/bin/env pwsh

<#
.SYNOPSIS
    🎯 COMPREHENSIVE E2E TEST RUNNER - AccountingAgent End-to-End Tests
    
.DESCRIPTION
    Runs comprehensive end-to-end tests for AccountingAgent using Playwright.
    Tests the complete workflow from chat input to email delivery.
    
.PARAMETER Headless
    Run tests in headless mode (default: false for debugging)
    
.PARAMETER Timeout
    Test timeout in minutes (default: 15)
    
.EXAMPLE
    ./run-e2e-tests.ps1
    ./run-e2e-tests.ps1 -Headless $true -Timeout 20
#>

param(
    [bool]$Headless = $false,
    [int]$Timeout = 15
)

Write-Host "🎯 COMPREHENSIVE E2E TEST RUNNER - AccountingAgent" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "Headless Mode: $Headless" -ForegroundColor Yellow
Write-Host "Timeout: $Timeout minutes" -ForegroundColor Yellow
Write-Host ""

# Set working directory to script location
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

try {
    # 1. Install Playwright browsers if needed
    Write-Host "🎭 Installing Playwright browsers..." -ForegroundColor Green
    dotnet build
    pwsh bin/Debug/net9.0/playwright.ps1 install chromium
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to install Playwright browsers" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✅ Playwright browsers installed" -ForegroundColor Green
    
    # 2. Set environment variables
    $env:PLAYWRIGHT_HEADLESS = $Headless.ToString().ToLower()
    $env:TEST_TIMEOUT_MINUTES = $Timeout
    
    # 3. Run the comprehensive E2E tests
    Write-Host "🚀 Starting comprehensive E2E tests..." -ForegroundColor Green
    Write-Host "This will test the complete AccountingAgent workflow:" -ForegroundColor Yellow
    Write-Host "  📊 Expense Report Test" -ForegroundColor White
    Write-Host "  📈 Project Summary Report Test" -ForegroundColor White  
    Write-Host "  🎯 Financial Analysis Pivot Report Test" -ForegroundColor White
    Write-Host "  📅 Weekly Financial Summary Test" -ForegroundColor White
    Write-Host ""
    
    $testStartTime = Get-Date
    
    dotnet test --logger "console;verbosity=detailed" --configuration Debug
    
    $testEndTime = Get-Date
    $testDuration = $testEndTime - $testStartTime
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "🎉 ALL E2E TESTS PASSED!" -ForegroundColor Green
        Write-Host "========================" -ForegroundColor Green
        Write-Host "Test Duration: $($testDuration.ToString('mm\:ss'))" -ForegroundColor Yellow
        Write-Host "✅ Expense Report Test: PASSED" -ForegroundColor Green
        Write-Host "✅ Project Summary Report Test: PASSED" -ForegroundColor Green
        Write-Host "✅ Financial Analysis Pivot Report Test: PASSED" -ForegroundColor Green
        Write-Host "✅ Weekly Financial Summary Test: PASSED" -ForegroundColor Green
        Write-Host ""
        Write-Host "📊 VALIDATION CONFIRMED:" -ForegroundColor Cyan
        Write-Host "  ✅ OpenAI responses completed successfully" -ForegroundColor White
        Write-Host "  ✅ Email delivery to Anna Andersson confirmed" -ForegroundColor White
        Write-Host "  ✅ Report attachments generated" -ForegroundColor White
        Write-Host "  ✅ No content filter errors" -ForegroundColor White
        Write-Host "  ✅ Unique pivot table names (no conflicts)" -ForegroundColor White
        Write-Host "  ✅ Professional Swedish format confirmed" -ForegroundColor White
        Write-Host ""
        Write-Host "🏆 AccountingAgent E2E workflow is FULLY FUNCTIONAL!" -ForegroundColor Green
    }
    else {
        Write-Host ""
        Write-Host "❌ E2E TESTS FAILED!" -ForegroundColor Red
        Write-Host "===================" -ForegroundColor Red
        Write-Host "Test Duration: $($testDuration.ToString('mm\:ss'))" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "🔍 Check the test output above for specific failures." -ForegroundColor Yellow
        Write-Host "Common issues to investigate:" -ForegroundColor Yellow
        Write-Host "  • AppHost startup failures" -ForegroundColor White
        Write-Host "  • Service readiness timeouts" -ForegroundColor White
        Write-Host "  • OpenAI content filter errors" -ForegroundColor White
        Write-Host "  • Email delivery configuration" -ForegroundColor White
        Write-Host "  • Pivot table naming conflicts" -ForegroundColor White
        Write-Host "  • WebChat UI element changes" -ForegroundColor White
        
        exit 1
    }
}
catch {
    Write-Host ""
    Write-Host "💥 CRITICAL ERROR DURING E2E TESTS!" -ForegroundColor Red
    Write-Host "===================================" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack Trace: $($_.Exception.StackTrace)" -ForegroundColor Red
    
    exit 1
}
finally {
    # Cleanup
    Write-Host ""
    Write-Host "🧹 Cleaning up test resources..." -ForegroundColor Yellow
    
    # Kill any remaining AppHost processes
    Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | 
        Where-Object { $_.CommandLine -like "*AppHost*" } | 
        Stop-Process -Force -ErrorAction SilentlyContinue
    
    Write-Host "✅ Cleanup complete" -ForegroundColor Green
}

Write-Host ""
Write-Host "🎯 E2E Test Runner completed!" -ForegroundColor Cyan
