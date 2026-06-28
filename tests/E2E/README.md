# 🎯 AccountingAgent Comprehensive E2E Tests

## Overview

This comprehensive end-to-end test suite validates the complete AccountingAgent workflow from chat input to email delivery using **Playwright browser automation**. The tests ensure that the entire system works correctly in a production-like environment.

## 🧪 Test Coverage

### Test Cases Executed

1. **📊 Expense Report Test**
   - Input: "Skapa en professionell utgiftsrapport för 2023 med diagram och pivot-tabeller och skicka till Anna Andersson"
   - Validates: Professional expense report with charts and pivot tables

2. **📈 Project Summary Report Test**
   - Input: "Skapa en professionell rapport där du kan se samtliga projekt och som summerar intäkter och kostnader och som går att filtrera på datum och skicka till Anna Andersson"
   - Validates: Comprehensive project report with revenue/cost summaries and filtering

3. **🎯 Financial Analysis Pivot Report Test**
   - Input: "Skapa en professionell rapport i pivot som visar omsättning, varukostnad, övriga externa kostnader, personalkostnader, avskrivningar, finansiella kostnader uppdelat på kvartal för de senaste 5 åren och skicka till Anna Andersson"
   - Validates: Complex pivot table with 5-year quarterly financial breakdown

4. **📅 Weekly Financial Summary Test**
   - Input: "Skapa en professionell rapport som visar omsättning, varukostnad, bruttovinst i SEK, bruttovinst i % för fg vecka och skicka till Anna Andersson"
   - Validates: Weekly financial summary with gross profit calculations

## 🔍 Validation Criteria

Each test validates:

- ✅ **OpenAI Response Completion** - No content filter errors or timeouts
- ✅ **Email Delivery** - Confirmation that email was sent to Anna Andersson
- ✅ **Report Attachment** - Excel file with expected content generated
- ✅ **Pivot Table Integrity** - No naming conflicts or "Key already exists" errors
- ✅ **Swedish Format** - Professional Swedish language formatting
- ✅ **System Stability** - No crashes or critical errors during execution

## 🚀 Running the Tests

### Prerequisites

1. **.NET 9.0 SDK** installed
2. **Node.js** (for Playwright)
3. **AccountingAgent** source code
4. **Azure OpenAI** configuration in appsettings.json

### Quick Start

#### Windows (PowerShell)
```powershell
cd tests/E2E
./run-e2e-tests.ps1
```

#### macOS/Linux (Bash)
```bash
cd tests/E2E
chmod +x run-e2e-tests.sh
./run-e2e-tests.sh
```

### Advanced Options

#### Headless Mode (for CI/CD)
```bash
./run-e2e-tests.sh --headless
```

#### Custom Timeout
```bash
./run-e2e-tests.sh --timeout 20
```

#### Manual Test Execution
```bash
# Install Playwright browsers
dotnet build
pwsh bin/Debug/net9.0/playwright.ps1 install chromium

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"
```

## 🏗️ Test Architecture

### Components

1. **Test Setup**
   - Programmatically starts AppHost
   - Waits for all services (Agent, WebChat, Aspire Dashboard) to be ready
   - Initializes Playwright browser automation
   - Navigates to WebChat interface

2. **Test Execution**
   - Sends chat messages via browser automation
   - Monitors application logs in real-time
   - Waits for OpenAI response completion
   - Validates email delivery through log analysis

3. **Validation**
   - Checks for content filter errors
   - Confirms pivot table naming uniqueness
   - Validates Swedish format compliance
   - Ensures system stability throughout

4. **Cleanup**
   - Closes browser sessions
   - Terminates AppHost process
   - Cleans up temporary resources

### Key Features

- **Real Browser Automation** - Uses Chromium via Playwright
- **Log Monitoring** - Real-time application log analysis
- **Timeout Handling** - Configurable timeouts for different operations
- **Error Detection** - Comprehensive error detection and reporting
- **Resource Cleanup** - Automatic cleanup of test resources

## 📊 Expected Results

### Success Criteria

When all tests pass, you should see:

```
🎉 ALL E2E TESTS PASSED!
========================
Test Duration: 12:34
✅ Expense Report Test: PASSED
✅ Project Summary Report Test: PASSED  
✅ Financial Analysis Pivot Report Test: PASSED
✅ Weekly Financial Summary Test: PASSED

📊 VALIDATION CONFIRMED:
  ✅ OpenAI responses completed successfully
  ✅ Email delivery to Anna Andersson confirmed
  ✅ Report attachments generated
  ✅ No content filter errors
  ✅ Unique pivot table names (no conflicts)
  ✅ Professional Swedish format confirmed

🏆 AccountingAgent E2E workflow is FULLY FUNCTIONAL!
```

### Common Issues

If tests fail, check for:

- **AppHost Startup Failures** - Port conflicts, missing dependencies
- **Service Readiness Timeouts** - Services taking too long to start
- **OpenAI Content Filter Errors** - Azure OpenAI blocking requests
- **Email Configuration** - SMTP settings or authentication issues
- **Pivot Table Conflicts** - Duplicate naming (should be fixed)
- **WebChat UI Changes** - Element selectors may need updates

## 🔧 Troubleshooting

### Debug Mode

Run tests with browser visible for debugging:
```bash
./run-e2e-tests.sh  # Browser will be visible by default
```

### Log Analysis

Check application logs in the test output for detailed error information.

### Manual Verification

1. Start AppHost manually: `dotnet run --project src/AppHost`
2. Navigate to https://localhost:17024
3. Access WebChat and test manually
4. Check Aspire Dashboard for service status

## 🎯 Integration with CI/CD

For automated testing in CI/CD pipelines:

```yaml
- name: Run E2E Tests
  run: |
    cd tests/E2E
    ./run-e2e-tests.sh --headless --timeout 20
```

The tests are designed to be reliable and deterministic for automated execution.
