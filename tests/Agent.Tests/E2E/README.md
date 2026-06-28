# End-to-End Tests for Document Upload and Interpretation

This directory contains comprehensive end-to-end tests that validate the complete file upload and document interpretation workflow in the AccountingAgent system.

## DocumentUploadAndInterpretationE2ETests

### Overview

The `DocumentUploadAndInterpretationE2ETests` class provides a comprehensive test that validates the entire file upload and document interpretation pipeline:

**WebChat File Upload** → **Agent File ID Processing** → **DocumentInterpreter Plugin** → **AI Analysis Response**

### Test Workflow

1. **Setup and Launch**: Checks that both Agent and WebChat services are running
2. **Authentication**: Navigates to WebChat and logs in using password "isa72"
3. **File Upload**: Uploads a PDF invoice file from the `ekonomi/` directory
4. **Document Analysis Request**: Sends the exact message: "Hej Isa, kan du tolka denna leverantörsfakturan och skriva ut dess innhåll i chatten?"
5. **Validation**: Verifies that Isa successfully processes the document and returns structured content

### Prerequisites

Before running the E2E tests, ensure:

1. **AppHost is running**:
   ```bash
   cd src/AppHost
   dotnet run --configuration Release
   ```

2. **Services are accessible**:
   - Agent API: https://localhost:7042/health
   - WebChat: https://localhost:7041/health

3. **Test files exist**:
   - `ekonomi/demo.pdf` (or other invoice files in the ekonomi directory)

4. **Playwright browsers installed**:
   ```bash
   playwright install
   ```

### Running the Tests

#### Option 1: Using the Test Runner Script
```bash
./run-document-upload-e2e-test.sh
```

#### Option 2: Manual Test Execution
```bash
cd tests/Agent.Tests

# Temporarily remove Skip attribute
sed -i.bak 's/\[Fact(Skip = "E2E test[^"]*")\]/[Fact]/' E2E/DocumentUploadAndInterpretationE2ETests.cs

# Run the test
dotnet test --filter "TestType=Integration&Category=E2E" --logger "console;verbosity=detailed"

# Restore Skip attribute
mv E2E/DocumentUploadAndInterpretationE2ETests.cs.bak E2E/DocumentUploadAndInterpretationE2ETests.cs
```

#### Option 3: IDE Test Runner
1. Open the test file in your IDE
2. Temporarily comment out the `Skip` parameter in the `[Fact]` attribute
3. Run the test using your IDE's test runner
4. Restore the `Skip` parameter after testing

### Test Configuration

The test uses the following configuration:

- **Browser**: Chromium (headless=false for debugging)
- **WebChat URL**: https://localhost:7041
- **Agent API URL**: https://localhost:7042
- **Login Password**: "isa72"
- **Test File**: ekonomi/demo.pdf
- **Timeout**: 30 seconds for UI operations, 2 minutes for document processing

### Success Criteria

The test passes when:

1. ✅ **Authentication succeeds** - User can log into WebChat
2. ✅ **File upload works** - PDF file appears in the file list
3. ✅ **Message sent successfully** - Analysis request is sent to Isa
4. ✅ **Document processing completes** - Isa responds with document analysis
5. ✅ **Response validation passes** - Response contains document content (not error messages)

### Validation Logic

The test validates that the response:

- **Contains document analysis content** (keywords: dokument, faktura, leverantör, belopp, datum, analys, innehåll)
- **Is substantial** (more than 50 characters, indicating actual processing occurred)
- **Does not contain error messages** (no "kan inte hitta", "inte tillgång", "fel")

### Troubleshooting

#### Common Issues

1. **Services not running**:
   ```
   Error: WebChat and Agent services are not running
   Solution: Start AppHost with `dotnet run --project src/AppHost`
   ```

2. **File not found**:
   ```
   Error: Test file not found: ekonomi/demo.pdf
   Solution: Ensure the test file exists in the ekonomi directory
   ```

3. **Authentication failure**:
   ```
   Error: Timeout waiting for chat input
   Solution: Check WebChat login functionality and password
   ```

4. **Document processing timeout**:
   ```
   Error: Timeout waiting for document analysis response
   Solution: Check Agent logs for DocumentInterpreter processing errors
   ```

#### Debug Information

The test automatically:
- Takes screenshots on failure for debugging
- Provides detailed logging of each test step
- Validates service availability before running

#### Log Analysis

Check the following logs for debugging:

1. **Agent logs**: Look for file ID processing and DocumentInterpreter calls
2. **WebChat logs**: Check file upload and SignalR connection status
3. **Test output**: Review detailed step-by-step execution logs

### Integration Points Tested

This E2E test validates the integration between:

1. **WebChat → Agent**: File upload and chat message handling
2. **Agent → TempFilesService**: File ID resolution and file retrieval
3. **Agent → DocumentInterpreter**: Document processing pipeline
4. **DocumentInterpreter → Azure Services**: AI-based document analysis
5. **Agent → WebChat**: Response delivery via SignalR

### Regression Testing

This test serves as a regression test for:

- File upload functionality in WebChat
- File ID processing in Agent ChatExecutionService
- DocumentInterpreter plugin integration
- Azure Document Intelligence and Computer Vision services
- End-to-end chat workflow with file attachments

Run this test after any changes to:
- File upload/download logic
- Document processing pipeline
- Chat message handling
- Azure service integrations

### Performance Expectations

Typical execution times:
- **Authentication**: 3-5 seconds
- **File upload**: 2-3 seconds
- **Document processing**: 30-60 seconds (depending on file size and Azure response times)
- **Total test time**: 1-2 minutes

### Browser Configuration

The test uses Chromium with:
- SSL certificate errors ignored (for localhost HTTPS)
- Slow motion enabled (500ms) for visibility during debugging
- Screenshots captured on failure for debugging

For CI/CD environments, set `Headless = true` in the browser launch options.
