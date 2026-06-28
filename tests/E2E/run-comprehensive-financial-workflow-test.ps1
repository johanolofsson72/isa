#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Runs the Comprehensive Financial Reporting Workflow Test
    
.DESCRIPTION
    This script executes the complete end-to-end test that validates all 8 financial report requests
    through the AccountingAgent WebChat interface. It includes:
    - AppHost startup verification
    - Sequential execution of all 8 report requests
    - Email delivery validation
    - File storage verification
    - Report content accuracy validation
    
.PARAMETER Headless
    Run browser in headless mode (default: false for debugging)
    
.PARAMETER Timeout
    Test timeout in minutes (default: 30)
    
.PARAMETER CleanReports
    Clean existing reports before test (default: true)
    
.EXAMPLE
    ./run-comprehensive-financial-workflow-test.ps1
    
.EXAMPLE
    ./run-comprehensive-financial-workflow-test.ps1 -Headless $true -Timeout 45
#>

param(
    [bool]$Headless = $false,
    [int]$Timeout = 30,
    [bool]$CleanReports = $true,
    [string]$ReportsDirectory = "/path/to/accountingagent/analysis_reports/"
)

# Set error handling
$ErrorActionPreference = "Stop"

Write-Host "🎯 COMPREHENSIVE FINANCIAL REPORTING WORKFLOW TEST" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$TestName = "ComprehensiveFinancialReportingWorkflowTest"
$TestMethod = "ExecuteComprehensiveFinancialReportingWorkflow_ShouldValidateAllReports"
$ProjectPath = "tests/E2E"
$LogFile = "comprehensive-workflow-test-$(Get-Date -Format 'yyyy-MM-dd-HH-mm-ss').log"

Write-Host "📋 Test Configuration:" -ForegroundColor Yellow
Write-Host "   Test Name: $TestName" -ForegroundColor Gray
Write-Host "   Method: $TestMethod" -ForegroundColor Gray
Write-Host "   Headless Mode: $Headless" -ForegroundColor Gray
Write-Host "   Timeout: $Timeout minutes" -ForegroundColor Gray
Write-Host "   Reports Directory: $ReportsDirectory" -ForegroundColor Gray
Write-Host "   Log File: $LogFile" -ForegroundColor Gray
Write-Host ""

# Pre-test setup
Write-Host "🔧 Pre-test Setup" -ForegroundColor Yellow

# Ensure reports directory exists
if (!(Test-Path $ReportsDirectory)) {
    Write-Host "📁 Creating reports directory: $ReportsDirectory" -ForegroundColor Green
    New-Item -ItemType Directory -Path $ReportsDirectory -Force | Out-Null
}

# Clean existing reports if requested
if ($CleanReports) {
    Write-Host "🧹 Cleaning existing reports..." -ForegroundColor Green
    Get-ChildItem -Path $ReportsDirectory -File | Remove-Item -Force -ErrorAction SilentlyContinue
    Write-Host "✅ Reports directory cleaned" -ForegroundColor Green
}

# Set environment variables for test
$env:PLAYWRIGHT_HEADLESS = $Headless.ToString().ToLower()
$env:TEST_TIMEOUT_MINUTES = $Timeout.ToString()
$env:REPORTS_DIRECTORY = $ReportsDirectory

# Verify AppHost is running (optional check)
Write-Host "🌐 Checking AppHost availability..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "https://localhost:7001/health" -SkipCertificateCheck -TimeoutSec 10 -ErrorAction SilentlyContinue
    if ($response.StatusCode -eq 200) {
        Write-Host "✅ AppHost is running and accessible" -ForegroundColor Green
    } else {
        Write-Host "⚠️ AppHost health check returned status: $($response.StatusCode)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "⚠️ AppHost health check failed - test will attempt to connect anyway" -ForegroundColor Yellow
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Gray
}

Write-Host ""

# Run the comprehensive test
Write-Host "🚀 Starting Comprehensive Financial Reporting Workflow Test" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

$testStartTime = Get-Date

try {
    # Build the test project first
    Write-Host "🔨 Building test project..." -ForegroundColor Yellow
    dotnet build $ProjectPath --configuration Release --verbosity quiet
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build test project"
    }
    
    Write-Host "✅ Test project built successfully" -ForegroundColor Green
    Write-Host ""
    
    # Run the specific test
    Write-Host "🧪 Executing comprehensive workflow test..." -ForegroundColor Yellow
    Write-Host "   This test will execute all 8 report requests sequentially" -ForegroundColor Gray
    Write-Host "   Expected duration: 15-25 minutes depending on OpenAI response times" -ForegroundColor Gray
    Write-Host ""
    
    $testCommand = "dotnet test `"$ProjectPath`" --filter `"$TestName.$TestMethod`" --logger `"console;verbosity=detailed`" --configuration Release --no-build"
    
    Write-Host "Executing: $testCommand" -ForegroundColor Gray
    Write-Host ""
    
    # Execute the test and capture output
    $testOutput = Invoke-Expression $testCommand 2>&1
    $testExitCode = $LASTEXITCODE
    
    # Save output to log file
    $testOutput | Out-File -FilePath $LogFile -Encoding UTF8
    
    # Display test results
    Write-Host ""
    Write-Host "📊 TEST EXECUTION COMPLETED" -ForegroundColor Cyan
    Write-Host "===========================" -ForegroundColor Cyan
    
    $testEndTime = Get-Date
    $testDuration = $testEndTime - $testStartTime
    
    Write-Host "⏱️ Total Test Duration: $($testDuration.ToString('hh\:mm\:ss'))" -ForegroundColor Yellow
    Write-Host "📄 Log File: $LogFile" -ForegroundColor Yellow
    
    if ($testExitCode -eq 0) {
        Write-Host ""
        Write-Host "🎉 COMPREHENSIVE WORKFLOW TEST PASSED!" -ForegroundColor Green
        Write-Host "=====================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "✅ All 8 financial reports were successfully:" -ForegroundColor Green
        Write-Host "   📤 Sent via WebChat interface" -ForegroundColor Green
        Write-Host "   🤖 Processed by OpenAI" -ForegroundColor Green
        Write-Host "   📧 Emailed to Anna Andersson" -ForegroundColor Green
        Write-Host "   📁 Saved to analysis_reports directory" -ForegroundColor Green
        Write-Host "   🔍 Validated for content accuracy" -ForegroundColor Green
        
        # Display generated files
        Write-Host ""
        Write-Host "📂 Generated Report Files:" -ForegroundColor Yellow
        $reportFiles = Get-ChildItem -Path $ReportsDirectory -File | Sort-Object CreationTime -Descending
        foreach ($file in $reportFiles) {
            $fileSize = [math]::Round($file.Length / 1KB, 1)
            Write-Host "   📄 $($file.Name) ($fileSize KB)" -ForegroundColor Gray
        }
        
    } else {
        Write-Host ""
        Write-Host "❌ COMPREHENSIVE WORKFLOW TEST FAILED!" -ForegroundColor Red
        Write-Host "=====================================" -ForegroundColor Red
        Write-Host ""
        Write-Host "Please check the log file for detailed error information: $LogFile" -ForegroundColor Yellow
        
        # Display last few lines of output for immediate feedback
        Write-Host ""
        Write-Host "📄 Last 20 lines of test output:" -ForegroundColor Yellow
        $testOutput | Select-Object -Last 20 | ForEach-Object { Write-Host "   $_" -ForegroundColor Gray }
    }
    
} catch {
    Write-Host ""
    Write-Host "💥 CRITICAL ERROR DURING TEST EXECUTION" -ForegroundColor Red
    Write-Host "=======================================" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    $testExitCode = 1
}

# Cleanup
Write-Host ""
Write-Host "🧹 Post-test Cleanup" -ForegroundColor Yellow
Remove-Item Env:PLAYWRIGHT_HEADLESS -ErrorAction SilentlyContinue
Remove-Item Env:TEST_TIMEOUT_MINUTES -ErrorAction SilentlyContinue
Remove-Item Env:REPORTS_DIRECTORY -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "📋 Test Summary:" -ForegroundColor Cyan
Write-Host "   Result: $(if ($testExitCode -eq 0) { '✅ PASSED' } else { '❌ FAILED' })" -ForegroundColor $(if ($testExitCode -eq 0) { 'Green' } else { 'Red' })
Write-Host "   Duration: $($testDuration.ToString('hh\:mm\:ss'))" -ForegroundColor Gray
Write-Host "   Log File: $LogFile" -ForegroundColor Gray

exit $testExitCode
