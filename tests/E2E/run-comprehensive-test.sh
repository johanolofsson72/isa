#!/bin/bash

# 🎯 Comprehensive Report Workflow Test Runner
# This script runs the complete end-to-end test for report generation workflow

echo "🎯 COMPREHENSIVE REPORT WORKFLOW TEST RUNNER"
echo "============================================="
echo ""

# Check if AppHost is running
echo "🔍 Checking if AppHost is running..."
if ! curl -s -k https://localhost:7041 > /dev/null 2>&1; then
    echo "❌ ERROR: AppHost is not running!"
    echo "Please start AppHost first:"
    echo "   cd /path/to/accountingagent"
    echo "   dotnet run --project src/AppHost/AppHost.csproj"
    echo ""
    exit 1
fi

echo "✅ AppHost is running"
echo ""

# Ensure reports directory exists
REPORTS_DIR="/path/to/accountingagent/analysis_reports"
echo "📁 Ensuring reports directory exists: $REPORTS_DIR"
mkdir -p "$REPORTS_DIR"

# Clean up old test files
echo "🧹 Cleaning up old test screenshots..."
rm -f comprehensive-test-*.png
rm -f expense-report-fix-test.png
rm -f isolated-report-*.png

echo ""
echo "🚀 Starting comprehensive test execution..."
echo "⏱️  Expected duration: ~15-20 minutes"
echo "📊 Testing 5 different report types with full validation"
echo ""

# Run the comprehensive test
cd /path/to/accountingagent
dotnet test tests/E2E --filter "ComprehensiveReportWorkflow_ShouldGenerateValidReportsWithCorrectStructureAndContent" --logger "console;verbosity=detailed"

TEST_RESULT=$?

echo ""
echo "📋 TEST EXECUTION COMPLETED"
echo "=========================="

if [ $TEST_RESULT -eq 0 ]; then
    echo "🎉 SUCCESS: All tests passed!"
    echo ""
    echo "📁 Generated files:"
    echo "   Screenshots: comprehensive-test-*.png"
    echo "   Reports: $REPORTS_DIR/*.xlsx"
    echo ""
    echo "🔍 Check the test output above for detailed validation results."
else
    echo "❌ FAILURE: Some tests failed!"
    echo ""
    echo "🔧 Troubleshooting steps:"
    echo "1. Check that AppHost is running properly"
    echo "2. Verify WebChat is accessible at https://localhost:7041"
    echo "3. Check the detailed test output above for specific failures"
    echo "4. Review generated screenshots for UI issues"
    echo ""
fi

echo ""
echo "📊 Test artifacts location:"
echo "   Screenshots: $(pwd)/"
echo "   Reports: $REPORTS_DIR/"
echo "   Logs: Check console output above"

exit $TEST_RESULT
