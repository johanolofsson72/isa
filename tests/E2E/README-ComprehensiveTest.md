# 🎯 Comprehensive Report Workflow Test

## Overview

This comprehensive end-to-end test validates the complete report generation workflow using Playwright automation. It tests 5 different report types with full validation of structure, content, and data accuracy.

## Test Features

### 🔧 **Test Setup**
- Automatically starts and validates AppHost is running
- Uses isolated browser sessions to prevent conversation history contamination
- Creates clean test environment for each report

### 📊 **Test Execution Process**
For each of the 5 test requests:
1. **Send Request** - Submits the request text to WebChat interface
2. **Wait for Processing** - Allows up to 60 seconds for OpenAI processing
3. **Email Validation** - Verifies email sent to Anna Andersson
4. **File System Check** - Confirms report created in `/analysis_reports/`
5. **Excel Parsing** - Opens and validates Excel report structure
6. **Content Validation** - Verifies data matches request requirements
7. **SIE4 Data Comparison** - Ensures financial data accuracy

### ✅ **Critical Validation Requirements**

#### **Structure Validation**
- ✅ Front page validation when "startsida" requested
- ✅ Minimum sheet count (at least 2 sheets)
- ✅ Data sheets with substantial content
- ✅ Professional report formatting

#### **Content Validation**
- ✅ Required data types present (diagrams, pivot tables, etc.)
- ✅ Year-specific data accuracy
- ✅ Request-specific content matching
- ✅ No generic/contaminated content

#### **Data Accuracy**
- ✅ Financial data matches requested year/period
- ✅ SIE4 data consistency validation
- ✅ Account codes and amounts verification
- ✅ Date range accuracy

## Test Cases

### 1. **Expense Report 2023**
```
"Skapa en professionell utgiftsrapport för 2023 med prydlig startsida och sedan sidor för diagram och pivot-tabeller och skicka till Anna Andersson"
```
**Expected:** Front page + expense diagrams + pivot tables for 2023 data

### 2. **Project Summary Report 2024**
```
"Skapa en professionell rapport för 2024 med prydlig startsida och sedan sidor där du kan se samtliga projekt och som summerar intäkter och kostnader och som går att filtrera på datum och skicka till Anna Andersson"
```
**Expected:** Front page + project overview + revenue/cost summary + date filtering for 2024

### 3. **5-Year Quarterly Pivot Report**
```
"Skapa en professionell rapport med prydlig startsida och sedan sidor för pivot som visar omsättning, varukostnad, övriga externa kostnader, personalkostnader, avskrivningar, finansiella kostnader uppdelat på kvartal för de senaste 5 åren och skicka till Anna Andersson"
```
**Expected:** Front page + quarterly pivot tables for 5-year period

### 4. **Weekly Financial Summary 2022**
```
"Skapa en professionell rapport för 2022 med prydlig startsida och sedan sidor som visar omsättning, varukostnad, bruttovinst i SEK, bruttovinst i % för fg vecka och skicka till Anna Andersson"
```
**Expected:** Front page + weekly analysis + gross profit calculations for 2022

### 5. **Future Analysis Report 2026**
```
"Skapa en professionell analys/framtidsrapport för 2026 med prydlig startsida och sedan sidor som visar omsättning, varukostnad, bruttovinst i SEK och skicka till Anna Andersson"
```
**Expected:** Front page + future projections for 2026

## Running the Test

### Prerequisites
1. **AppHost Running:** `dotnet run --project src/AppHost/AppHost.csproj`
2. **WebChat Accessible:** https://localhost:7041
3. **Reports Directory:** `/path/to/accountingagent/analysis_reports/`

### Execution Options

#### Option 1: Using the Test Runner Script
```bash
./tests/E2E/run-comprehensive-test.sh
```

#### Option 2: Direct dotnet test
```bash
dotnet test tests/E2E --filter "ComprehensiveReportWorkflow_ShouldGenerateValidReportsWithCorrectStructureAndContent" --logger "console;verbosity=detailed"
```

### Expected Duration
- **Total Time:** 15-20 minutes
- **Per Report:** 3-4 minutes
- **Includes:** AI processing, file generation, validation

## Test Artifacts

### Generated Files
- **Screenshots:** `comprehensive-test-*.png`
- **Reports:** `/analysis_reports/*.xlsx`
- **Logs:** Console output with detailed validation results

### Validation Results
- **Success Rate:** Percentage of successful reports
- **Structure Validation:** Front page and sheet structure
- **Content Validation:** Required data types present
- **SIE4 Consistency:** Financial data accuracy
- **Email Confirmation:** Delivery verification

## Success Criteria

### ✅ **Passing Test Requirements**
- **80% Success Rate:** At least 4 of 5 reports must generate successfully
- **Unique Content:** Each report must contain request-specific content
- **Proper Structure:** Front page + data sheets as requested
- **Data Accuracy:** Financial data must match SIE4 source
- **No Contamination:** No generic content from other reports

### 📊 **Quality Metrics**
- **Content Uniqueness:** Each report contains only relevant data
- **Structure Compliance:** Matches requested format exactly
- **Data Integrity:** All amounts and dates are accurate
- **Professional Quality:** Reports suitable for business use

## Troubleshooting

### Common Issues
1. **AppHost Not Running:** Ensure AppHost is started before test
2. **WebChat Inaccessible:** Check https://localhost:7041 is responding
3. **File Permission Issues:** Verify write access to reports directory
4. **Browser Issues:** Playwright may need browser installation

### Debug Information
- **Screenshots:** Visual confirmation of UI interactions
- **Detailed Logs:** Step-by-step validation results
- **File System:** Generated reports for manual inspection
- **Error Messages:** Specific failure reasons for debugging

This comprehensive test ensures the report generation system works end-to-end with real user scenarios and validates both technical functionality and business requirements.
