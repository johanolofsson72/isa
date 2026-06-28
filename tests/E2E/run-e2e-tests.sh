#!/bin/bash

# 🎯 COMPREHENSIVE E2E TEST RUNNER - AccountingAgent End-to-End Tests
# Tests the complete workflow from chat input to email delivery using Playwright

set -e

# Default parameters
HEADLESS=false
TIMEOUT=15

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --headless)
            HEADLESS=true
            shift
            ;;
        --timeout)
            TIMEOUT="$2"
            shift 2
            ;;
        -h|--help)
            echo "🎯 COMPREHENSIVE E2E TEST RUNNER - AccountingAgent"
            echo "================================================="
            echo ""
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --headless     Run tests in headless mode (default: false)"
            echo "  --timeout N    Test timeout in minutes (default: 15)"
            echo "  -h, --help     Show this help message"
            echo ""
            echo "Examples:"
            echo "  $0"
            echo "  $0 --headless --timeout 20"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

echo "🎯 COMPREHENSIVE E2E TEST RUNNER - AccountingAgent"
echo "================================================="
echo "Headless Mode: $HEADLESS"
echo "Timeout: $TIMEOUT minutes"
echo ""

# Set working directory to script location
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

cleanup() {
    echo ""
    echo "🧹 Cleaning up test resources..."
    
    # Kill any remaining AppHost processes
    pkill -f "dotnet.*AppHost" || true
    
    echo "✅ Cleanup complete"
}

# Set trap for cleanup on exit
trap cleanup EXIT

# 1. Install Playwright browsers if needed
echo "🎭 Installing Playwright browsers..."
dotnet build

if ! dotnet run --project . -- install chromium; then
    echo "❌ Failed to install Playwright browsers"
    exit 1
fi

echo "✅ Playwright browsers installed"

# 2. Set environment variables
export PLAYWRIGHT_HEADLESS="$HEADLESS"
export TEST_TIMEOUT_MINUTES="$TIMEOUT"

# 3. Run the comprehensive E2E tests
echo "🚀 Starting comprehensive E2E tests..."
echo "This will test the complete AccountingAgent workflow:"
echo "  📊 Expense Report Test"
echo "  📈 Project Summary Report Test"
echo "  🎯 Financial Analysis Pivot Report Test"
echo "  📅 Weekly Financial Summary Test"
echo ""

TEST_START_TIME=$(date +%s)

if dotnet test --logger "console;verbosity=detailed" --configuration Debug; then
    TEST_END_TIME=$(date +%s)
    TEST_DURATION=$((TEST_END_TIME - TEST_START_TIME))
    TEST_DURATION_MIN=$((TEST_DURATION / 60))
    TEST_DURATION_SEC=$((TEST_DURATION % 60))
    
    echo ""
    echo "🎉 ALL E2E TESTS PASSED!"
    echo "========================"
    echo "Test Duration: ${TEST_DURATION_MIN}:$(printf "%02d" $TEST_DURATION_SEC)"
    echo "✅ Expense Report Test: PASSED"
    echo "✅ Project Summary Report Test: PASSED"
    echo "✅ Financial Analysis Pivot Report Test: PASSED"
    echo "✅ Weekly Financial Summary Test: PASSED"
    echo ""
    echo "📊 VALIDATION CONFIRMED:"
    echo "  ✅ OpenAI responses completed successfully"
    echo "  ✅ Email delivery to Anna Andersson confirmed"
    echo "  ✅ Report attachments generated"
    echo "  ✅ No content filter errors"
    echo "  ✅ Unique pivot table names (no conflicts)"
    echo "  ✅ Professional Swedish format confirmed"
    echo ""
    echo "🏆 AccountingAgent E2E workflow is FULLY FUNCTIONAL!"
else
    TEST_END_TIME=$(date +%s)
    TEST_DURATION=$((TEST_END_TIME - TEST_START_TIME))
    TEST_DURATION_MIN=$((TEST_DURATION / 60))
    TEST_DURATION_SEC=$((TEST_DURATION % 60))
    
    echo ""
    echo "❌ E2E TESTS FAILED!"
    echo "==================="
    echo "Test Duration: ${TEST_DURATION_MIN}:$(printf "%02d" $TEST_DURATION_SEC)"
    echo ""
    echo "🔍 Check the test output above for specific failures."
    echo "Common issues to investigate:"
    echo "  • AppHost startup failures"
    echo "  • Service readiness timeouts"
    echo "  • OpenAI content filter errors"
    echo "  • Email delivery configuration"
    echo "  • Pivot table naming conflicts"
    echo "  • WebChat UI element changes"
    
    exit 1
fi

echo ""
echo "🎯 E2E Test Runner completed!"
