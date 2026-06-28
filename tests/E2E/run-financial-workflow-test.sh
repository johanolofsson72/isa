#!/bin/bash

# 🎯 Comprehensive Financial Reporting Workflow Test Runner
# This script runs the complete end-to-end test for all 8 financial reports

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Configuration
TEST_NAME="ComprehensiveFinancialReportingWorkflowTest"
TEST_METHOD="ExecuteComprehensiveFinancialReportingWorkflow_ShouldValidateAllReports"
PROJECT_PATH="tests/E2E"
REPORTS_DIR="/path/to/accountingagent/analysis_reports/"
LOG_FILE="comprehensive-workflow-test-$(date +%Y-%m-%d-%H-%M-%S).log"

echo -e "${CYAN}🎯 COMPREHENSIVE FINANCIAL REPORTING WORKFLOW TEST${NC}"
echo -e "${CYAN}=================================================${NC}"
echo ""

# Parse command line arguments
HEADLESS=false
TIMEOUT=30
CLEAN_REPORTS=true

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
        --no-clean)
            CLEAN_REPORTS=false
            shift
            ;;
        --help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --headless     Run browser in headless mode"
            echo "  --timeout N    Set timeout in minutes (default: 30)"
            echo "  --no-clean     Don't clean existing reports"
            echo "  --help         Show this help message"
            echo ""
            echo "Examples:"
            echo "  $0                    # Run with default settings"
            echo "  $0 --headless        # Run in headless mode"
            echo "  $0 --timeout 45      # Run with 45 minute timeout"
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            exit 1
            ;;
    esac
done

echo -e "${YELLOW}📋 Test Configuration:${NC}"
echo -e "   Test Name: ${TEST_NAME}"
echo -e "   Method: ${TEST_METHOD}"
echo -e "   Headless Mode: ${HEADLESS}"
echo -e "   Timeout: ${TIMEOUT} minutes"
echo -e "   Reports Directory: ${REPORTS_DIR}"
echo -e "   Log File: ${LOG_FILE}"
echo ""

# Pre-test setup
echo -e "${YELLOW}🔧 Pre-test Setup${NC}"

# Ensure reports directory exists
if [ ! -d "$REPORTS_DIR" ]; then
    echo -e "${GREEN}📁 Creating reports directory: ${REPORTS_DIR}${NC}"
    mkdir -p "$REPORTS_DIR"
fi

# Clean existing reports if requested
if [ "$CLEAN_REPORTS" = true ]; then
    echo -e "${GREEN}🧹 Cleaning existing reports...${NC}"
    rm -f "${REPORTS_DIR}"*
    echo -e "${GREEN}✅ Reports directory cleaned${NC}"
fi

# Set environment variables
export PLAYWRIGHT_HEADLESS="$HEADLESS"
export TEST_TIMEOUT_MINUTES="$TIMEOUT"
export REPORTS_DIRECTORY="$REPORTS_DIR"

# Check AppHost availability
echo -e "${YELLOW}🌐 Checking AppHost availability...${NC}"
if curl -k -s --connect-timeout 10 "https://localhost:7001/health" > /dev/null 2>&1; then
    echo -e "${GREEN}✅ AppHost is running and accessible${NC}"
else
    echo -e "${YELLOW}⚠️ AppHost health check failed - test will attempt to connect anyway${NC}"
fi

echo ""

# Run the comprehensive test
echo -e "${CYAN}🚀 Starting Comprehensive Financial Reporting Workflow Test${NC}"
echo -e "${CYAN}============================================================${NC}"

TEST_START_TIME=$(date +%s)

# Build the test project first
echo -e "${YELLOW}🔨 Building test project...${NC}"
if dotnet build "$PROJECT_PATH" --configuration Release --verbosity quiet; then
    echo -e "${GREEN}✅ Test project built successfully${NC}"
else
    echo -e "${RED}❌ Failed to build test project${NC}"
    exit 1
fi

echo ""

# Run the specific test
echo -e "${YELLOW}🧪 Executing comprehensive workflow test...${NC}"
echo -e "   This test will execute all 8 report requests sequentially"
echo -e "   Expected duration: 15-25 minutes depending on OpenAI response times"
echo ""

TEST_COMMAND="dotnet test \"$PROJECT_PATH\" --filter \"$TEST_NAME.$TEST_METHOD\" --logger \"console;verbosity=detailed\" --configuration Release --no-build"

echo -e "Executing: ${TEST_COMMAND}"
echo ""

# Execute the test and capture output
if eval "$TEST_COMMAND" 2>&1 | tee "$LOG_FILE"; then
    TEST_EXIT_CODE=0
else
    TEST_EXIT_CODE=1
fi

# Calculate test duration
TEST_END_TIME=$(date +%s)
TEST_DURATION=$((TEST_END_TIME - TEST_START_TIME))
DURATION_FORMATTED=$(printf "%02d:%02d:%02d" $((TEST_DURATION/3600)) $((TEST_DURATION%3600/60)) $((TEST_DURATION%60)))

echo ""
echo -e "${CYAN}📊 TEST EXECUTION COMPLETED${NC}"
echo -e "${CYAN}===========================${NC}"

echo -e "${YELLOW}⏱️ Total Test Duration: ${DURATION_FORMATTED}${NC}"
echo -e "${YELLOW}📄 Log File: ${LOG_FILE}${NC}"

if [ $TEST_EXIT_CODE -eq 0 ]; then
    echo ""
    echo -e "${GREEN}🎉 COMPREHENSIVE WORKFLOW TEST PASSED!${NC}"
    echo -e "${GREEN}=====================================${NC}"
    echo ""
    echo -e "${GREEN}✅ All 8 financial reports were successfully:${NC}"
    echo -e "${GREEN}   📤 Sent via WebChat interface${NC}"
    echo -e "${GREEN}   🤖 Processed by OpenAI${NC}"
    echo -e "${GREEN}   📧 Emailed to Anna Andersson${NC}"
    echo -e "${GREEN}   📁 Saved to analysis_reports directory${NC}"
    echo -e "${GREEN}   🔍 Validated for content accuracy${NC}"
    
    # Display generated files
    echo ""
    echo -e "${YELLOW}📂 Generated Report Files:${NC}"
    if [ -d "$REPORTS_DIR" ]; then
        ls -la "$REPORTS_DIR" | grep -v "^total" | grep -v "^d" | while read -r line; do
            filename=$(echo "$line" | awk '{print $9}')
            filesize=$(echo "$line" | awk '{print $5}')
            if [ -n "$filename" ] && [ "$filename" != "." ] && [ "$filename" != ".." ]; then
                echo -e "   📄 ${filename} (${filesize} bytes)"
            fi
        done
    fi
    
else
    echo ""
    echo -e "${RED}❌ COMPREHENSIVE WORKFLOW TEST FAILED!${NC}"
    echo -e "${RED}=====================================${NC}"
    echo ""
    echo -e "${YELLOW}Please check the log file for detailed error information: ${LOG_FILE}${NC}"
    
    # Display last few lines of output for immediate feedback
    echo ""
    echo -e "${YELLOW}📄 Last 20 lines of test output:${NC}"
    tail -20 "$LOG_FILE" | sed 's/^/   /'
fi

# Cleanup
echo ""
echo -e "${YELLOW}🧹 Post-test Cleanup${NC}"
unset PLAYWRIGHT_HEADLESS
unset TEST_TIMEOUT_MINUTES
unset REPORTS_DIRECTORY

echo ""
echo -e "${CYAN}📋 Test Summary:${NC}"
if [ $TEST_EXIT_CODE -eq 0 ]; then
    echo -e "   Result: ${GREEN}✅ PASSED${NC}"
else
    echo -e "   Result: ${RED}❌ FAILED${NC}"
fi
echo -e "   Duration: ${DURATION_FORMATTED}"
echo -e "   Log File: ${LOG_FILE}"

exit $TEST_EXIT_CODE
