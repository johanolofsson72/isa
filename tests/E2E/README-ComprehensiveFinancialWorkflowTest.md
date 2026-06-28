# 🎯 Comprehensive Financial Reporting Workflow Test

## Overview

This comprehensive end-to-end test validates the complete financial reporting workflow in the AccountingAgent system using Playwright automation. The test executes all 8 specified report requests sequentially and validates every aspect of the reporting pipeline.

## Test Scope

### 🔍 **What This Test Validates:**

1. **WebChat Interface Integration**
   - Navigation to webchat interface
   - Authentication handling
   - Message sending functionality
   - OpenAI response processing

2. **Report Generation Pipeline**
   - All 8 specified financial report requests
   - Sequential execution with proper timing
   - OpenAI processing completion
   - Error handling and recovery

3. **Email Delivery System**
   - Email sent to Anna Andersson
   - Proper attachment handling
   - Delivery confirmation

4. **File Storage System**
   - Reports saved to `/path/to/accountingagent/analysis_reports/`
   - Both source data and final reports stored
   - File integrity validation

5. **Content Accuracy Validation**
   - Report structure matches specifications
   - Cover pages (startsida) when requested
   - Charts and diagrams when requested
   - Pivot tables when requested
   - Numerical data accuracy
   - Professional formatting compliance

## Test Reports

The test validates these 8 specific financial reports:

### 1. **2023 Utgiftsrapport**
```
Skapa en professionell utgiftsrapport för 2023 med prydlig startsida och sedan sidor för diagram och pivot-tabeller och skicka till Anna Andersson samt spara både underlaget och den färdiga rapporten i /path/to/accountingagent/analysis_reports/
```
**Validates:** Cover page, charts, pivot tables, file storage, email delivery

### 2. **2024 Projektrapport**
```
Skapa en professionell rapport för 2024 med prydlig startsida och sedan sidor där du kan se samtliga projekt och som summerar intäkter och kostnader och som går att filtrera på datum och skicka till Anna Andersson samt spara både underlaget och den färdiga rapporten i /path/to/accountingagent/analysis_reports/ för jämförelse
```
**Validates:** Cover page, project summaries, filtering capabilities, data accuracy

### 3. **5-års Kvartalsrapport**
```
Skapa en professionell rapport med prydlig startsida och sedan sidor för pivot som visar omsättning, varukostnad, övriga externa kostnader, personalkostnader, avskrivningar, finansiella kostnader uppdelat på kvartal för de senaste 5 åren och skicka till Anna Andersson samt spara både underlaget och den färdiga rapporten i /path/to/accountingagent/analysis_reports/ för jämförelse
```
**Validates:** Multi-year data, quarterly breakdowns, comprehensive cost categories

### 4. **2022 Bruttovinstrapport**
```
Skapa en professionell rapport för 2022 med prydlig startsida och sedan sidor som visar omsättning, varukostnad, bruttovinst i SEK, bruttovinst i % för fg vecka och skicka till Anna Andersson samt spara både underlaget och den färdiga rapporten i /path/to/accountingagent/analysis_reports/ för jämförelse
```
**Validates:** Gross profit calculations, percentage calculations, weekly data

### 5. **2024 Resultatrapport**
```
Skapa en professionell resultatrapport för 2024 och skicka till Anna Andersson samt spara både underlaget och den färdiga rapporten i /path/to/accountingagent/analysis_reports/ för jämförelse
```
**Validates:** Income statement format, professional presentation

### 6. **2023 Årsrapport**
```
Skapa en professionell årsrapport för 2023 och skicka till Anna Andersson samt spara både underlaget och den färdiga rapporten i /path/to/accountingagent/analysis_reports/ för jämförelse
```
**Validates:** Annual report structure, comprehensive financial overview

### 7. **Q3 Momsrapport**
```
Skapa en professionell momsrapport för Q3 och skicka till Anna Andersson samt spara både underlaget och den färdiga rapporten i /path/to/accountingagent/analysis_reports/ för jämförelse
```
**Validates:** VAT calculations, quarterly tax reporting

### 8. **2026 Q1 Framtidsanalys**
```
Skapa en professionell framtidsanalys inför 2026 Q1 och skicka till Anna Andersson samt spara både underlaget och den färdiga rapporten i /path/to/accountingagent/analysis_reports/ för jämförelse
```
**Validates:** Future analysis capabilities, forecasting integration

## Running the Test

### Prerequisites

1. **AppHost Running:** Ensure the AccountingAgent AppHost is running on `https://localhost:7001`
2. **WebChat Accessible:** WebChat interface should be available at `https://localhost:7001/webchat`
3. **Authentication:** Login password should be `isa72`
4. **Reports Directory:** Ensure `/path/to/accountingagent/analysis_reports/` exists
5. **Playwright Installed:** Playwright browsers should be installed

### Execution Methods

#### Method 1: PowerShell Script (Recommended)
```powershell
# Run with default settings
./run-comprehensive-financial-workflow-test.ps1

# Run in headless mode with extended timeout
./run-comprehensive-financial-workflow-test.ps1 -Headless $true -Timeout 45

# Run without cleaning existing reports
./run-comprehensive-financial-workflow-test.ps1 -CleanReports $false
```

#### Method 2: Direct dotnet test
```bash
dotnet test tests/E2E --filter "ComprehensiveFinancialReportingWorkflowTest.ExecuteComprehensiveFinancialReportingWorkflow_ShouldValidateAllReports" --logger "console;verbosity=detailed"
```

### Test Duration

- **Expected Duration:** 15-25 minutes
- **Factors Affecting Duration:**
  - OpenAI response times (2-3 minutes per report)
  - File generation and processing time
  - Email delivery processing
  - Network latency

## Validation Criteria

### ✅ **Success Criteria (All Must Pass):**

1. **Message Delivery:** All 8 messages successfully sent via WebChat
2. **OpenAI Processing:** All 8 requests processed by OpenAI with responses
3. **Email Delivery:** All 8 reports emailed to Anna Andersson
4. **File Storage:** All 8 reports saved to analysis_reports directory
5. **Content Validation:** All reports meet specification requirements
6. **Data Accuracy:** No numerical discrepancies or data integrity issues
7. **Format Compliance:** Professional formatting as requested

### ❌ **Failure Conditions:**

- Any report fails to generate
- Email delivery fails for any report
- Files not saved to specified directory
- Content doesn't match specifications (missing charts, pivot tables, etc.)
- Numerical data inaccuracies
- Test timeout exceeded
- Critical errors during execution

## Test Output

### Console Output
The test provides detailed console output including:
- Progress indicators for each report
- Validation results for each step
- File generation confirmations
- Error messages and debugging information
- Final summary with success/failure status

### Log Files
- Comprehensive log file generated: `comprehensive-workflow-test-YYYY-MM-DD-HH-mm-ss.log`
- Contains full test execution details
- Useful for debugging failed tests

### Generated Reports
All successfully generated reports are saved to:
```
/path/to/accountingagent/analysis_reports/
```

## Troubleshooting

### Common Issues

1. **AppHost Not Running**
   - Ensure AppHost is started and accessible
   - Check `https://localhost:7001/health`

2. **Authentication Failures**
   - Verify login password is `isa72`
   - Check WebChat interface accessibility

3. **OpenAI Timeout**
   - Increase timeout settings
   - Check OpenAI API key configuration
   - Verify network connectivity

4. **File Storage Issues**
   - Ensure reports directory exists and is writable
   - Check disk space availability
   - Verify file permissions

5. **Email Delivery Problems**
   - Check email service configuration
   - Verify SMTP settings
   - Check spam/junk folders

### Debug Mode
Run with visible browser for debugging:
```powershell
./run-comprehensive-financial-workflow-test.ps1 -Headless $false
```

## Quality Assurance

This test implements strict quality assurance measures:

- **Zero Tolerance for Data Inaccuracies:** Any numerical discrepancy fails the test
- **Specification Compliance:** Reports must exactly match user specifications
- **Complete Workflow Validation:** Every step of the pipeline is validated
- **Comprehensive Error Handling:** Detailed error reporting for debugging
- **Performance Monitoring:** Duration tracking for performance analysis

The test prioritizes **quality over quantity** - each report must be perfect rather than just generated.
