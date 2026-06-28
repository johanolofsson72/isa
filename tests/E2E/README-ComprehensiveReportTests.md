# Comprehensive Report E2E Tests

## Overview

This test suite provides comprehensive end-to-end testing of the AccountingAgent report generation system. It tests the complete workflow from WebChat input to email delivery and file validation.

## Test Scenarios

The test suite covers 8 different report generation scenarios:

1. **Utgiftsrapport 2023** - Professional expense report with start page, diagrams, and pivot tables
2. **Projektrapport 2024** - Project report with start page, project summaries, and date filtering
3. **Kvartalsrapport 5 år** - Quarterly report showing revenue, costs, etc. by quarter for 5 years
4. **Veckorapport 2022** - Weekly report showing revenue, cost of goods, gross profit in SEK and %
5. **Resultatrapport 2024** - Professional income statement for 2024
6. **Årsrapport 2023** - Professional annual report for 2023
7. **Momsrapport Q3** - Professional VAT report for Q3
8. **Framtidsanalys 2026 Q1** - Professional future analysis for Q1 2026

## What Each Test Does

### 1. WebChat Interaction
- Starts AppHost (Aspire orchestrator)
- Opens WebChat in browser using Playwright
- Sends specific report request message
- Waits for AI response (up to 2 minutes)

### 2. Email Delivery Verification
- Waits 30 seconds for email delivery to Anna Andersson
- Verifies that email was sent successfully

### 3. File Generation Verification
- Checks that new files were created in `/path/to/accountingagent/analysis_reports/`
- Verifies both source data and final reports are saved

### 4. Content Validation
- **Excel Files (.xlsx)**:
  - Verifies worksheets exist
  - Checks for start page if requested
  - Validates diagram/chart data if requested
  - Confirms pivot table structure if requested
  - Ensures numerical financial data is present
  
- **PDF Files (.pdf)**:
  - Validates file size and PDF header
  - Ensures file contains meaningful content
  
- **JSON Files (.json)**:
  - Validates JSON structure
  - Checks for financial data fields

### 5. Data Accuracy Verification
- Compares source data with generated reports
- Ensures no data discrepancies (critical requirement)
- Validates that all requested elements are present

## Running the Tests

### Prerequisites
- .NET 9.0 SDK
- Playwright browsers installed
- AppHost configured and ready to run
- Email service configured for delivery to Anna Andersson

### Command Line Options

#### PowerShell (Recommended)
```powershell
# Run with default settings (visible browser, 30 min timeout)
./run-comprehensive-report-tests.ps1

# Run in headless mode
./run-comprehensive-report-tests.ps1 -Headless

# Run with custom timeout (45 minutes)
./run-comprehensive-report-tests.ps1 -TimeoutMinutes 45
```

#### Bash
```bash
# Run with default settings
./run-e2e-tests.sh

# Run in headless mode
./run-e2e-tests.sh --headless

# Run with custom timeout
./run-e2e-tests.sh --timeout 45
```

#### Direct dotnet test
```bash
dotnet test E2E.csproj --logger "console;verbosity=detailed"
```

## Test Configuration

### Timeouts
- **Chat Response**: 2 minutes (120 seconds)
- **Email Delivery**: 30 seconds
- **Overall Test Session**: 30 minutes (configurable)

### File Locations
- **Reports Directory**: `/path/to/accountingagent/analysis_reports/`
- **WebChat URL**: `https://localhost:7041`
- **Test Results**: `tests/E2E/TestResults/`

### Browser Settings
- **Default**: Visible browser with 1 second slow motion
- **Headless Mode**: Available via command line flag
- **Browser**: Chromium (via Playwright)

## Quality Assurance

### Critical Requirements
1. **Email Delivery**: Every report MUST be sent to Anna Andersson
2. **File Storage**: Both source data and final reports MUST be saved locally
3. **Data Accuracy**: Generated reports MUST match source data exactly (zero tolerance for discrepancies)
4. **Content Requirements**: All requested elements (start pages, diagrams, pivots) MUST be present

### Validation Levels
- **File Existence**: Verifies files are created
- **File Content**: Validates file structure and format
- **Data Integrity**: Ensures numerical data is present and valid
- **Requirement Compliance**: Confirms all user-requested elements are included

## Troubleshooting

### Common Issues
1. **AppHost fails to start**: Check port availability (7041, 7042, 22243)
2. **SSL Certificate errors**: Ensure development SSL bypass is configured
3. **Email delivery fails**: Verify email service configuration
4. **Browser automation fails**: Ensure Playwright browsers are installed

### Debug Mode
Run tests with visible browser to see the actual interaction:
```powershell
./run-comprehensive-report-tests.ps1  # Default is visible browser
```

### Log Files
- Test results: `TestResults/comprehensive-report-e2e-results.trx`
- Console output: Detailed logging during test execution
- Generated files: Check `/path/to/accountingagent/analysis_reports/`

## Success Criteria

A test run is considered successful when:
1. All 8 report scenarios complete without errors
2. Each scenario generates the expected files
3. All files pass content validation
4. Email delivery confirmation for each report
5. Data accuracy validation passes for all reports

The test suite ensures that the AccountingAgent system can reliably generate professional financial reports with accurate data and proper formatting as requested by users.
