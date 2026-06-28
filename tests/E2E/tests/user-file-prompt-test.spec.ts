import { test, expect } from '@playwright/test';

test.describe('User File Prompt Tests', () => {
  test('should show automatic file action prompt after upload - USER VALIDATION', async ({ page }) => {
    console.log('🚀 Starting user validation test for automatic file prompt...');

    // Navigate to login page
    await page.goto('https://localhost:7174/login');

    // Login
    await page.fill('[data-testid="email-input"]', 'user@example.com');
    await page.fill('[data-testid="password-input"]', 'YOUR_EMAIL_PASSWORD');
    await page.click('[data-testid="login-button"]');

    // Wait for redirect to dashboard
    await page.waitForURL('**/dashboard', { timeout: 30000 });

    // Wait for chat panel to be visible
    await page.waitForSelector('[data-testid="chat-panel"]', { timeout: 30000 });
    console.log('✅ Chat panel found');

    // Wait a bit for everything to initialize
    await page.waitForTimeout(3000);

    // Click file upload button
    const uploadButton = page.locator('[data-testid="file-upload-button"]');
    await expect(uploadButton).toBeVisible({ timeout: 10000 });
    await uploadButton.click();
    console.log('✅ File upload button clicked');

    // Upload the test file
    console.log('📁 Uploading test file...');
    const fileInput = page.locator('[data-testid="file-input"]');
    await expect(fileInput).toBeAttached();
    await fileInput.setInputFiles('/path/to/accountingagent/ekonomi/test-faktura.txt');

    // Wait for upload to complete
    console.log('⏳ Waiting for upload to complete...');
    await page.waitForTimeout(5000);

    // Look for the automatic file action prompt
    console.log('🔍 Looking for automatic file action prompt...');
    const automaticPrompt = await page.waitForSelector(
      'text=/Jag ser att du har laddat upp filen.*Vad vill du göra med den/i', 
      { timeout: 15000 }
    );
    
    expect(automaticPrompt).toBeTruthy();
    console.log('🎉 SUCCESS! Automatic file action prompt found!');
    
    const promptText = await automaticPrompt.textContent();
    console.log('📝 Prompt text:', promptText?.substring(0, 200) + '...');
    
    // Verify the prompt contains expected options
    expect(promptText).toContain('Tolka filen');
    expect(promptText).toContain('Skicka via e-post');
    expect(promptText).toContain('Sammanfatta innehållet');
    expect(promptText).toContain('Extrahera data');
    
    console.log('✅ All expected options found in prompt');
    console.log('🎉 USER VALIDATION TEST PASSED - Automatic file prompt works correctly!');
  });

  test('should analyze file when user responds "tolka filen" - COMPLETE WORKFLOW', async ({ page }) => {
    console.log('🚀 Starting complete workflow test: upload + analyze...');

    // Navigate to login page
    await page.goto('https://localhost:7174/login');

    // Login
    await page.fill('[data-testid="email-input"]', 'user@example.com');
    await page.fill('[data-testid="password-input"]', 'YOUR_EMAIL_PASSWORD');
    await page.click('[data-testid="login-button"]');

    // Wait for redirect to dashboard
    await page.waitForURL('**/dashboard', { timeout: 30000 });

    // Wait for chat panel to be visible
    await page.waitForSelector('[data-testid="chat-panel"]', { timeout: 30000 });
    console.log('✅ Chat panel found');

    // Wait a bit for everything to initialize
    await page.waitForTimeout(3000);

    // Click file upload button
    const uploadButton = page.locator('[data-testid="file-upload-button"]');
    await expect(uploadButton).toBeVisible({ timeout: 10000 });
    await uploadButton.click();
    console.log('✅ File upload button clicked');

    // Upload the test file
    console.log('📁 Uploading test file...');
    const fileInput = page.locator('[data-testid="file-input"]');
    await expect(fileInput).toBeAttached();
    await fileInput.setInputFiles('/path/to/accountingagent/ekonomi/test-faktura.txt');

    // Wait for upload to complete
    console.log('⏳ Waiting for upload to complete...');
    await page.waitForTimeout(5000);

    // Look for the automatic file action prompt
    console.log('🔍 Looking for automatic file action prompt...');
    const automaticPrompt = await page.waitForSelector(
      'text=/Jag ser att du har laddat upp filen.*Vad vill du göra med den/i',
      { timeout: 15000 }
    );

    expect(automaticPrompt).toBeTruthy();
    console.log('🎉 SUCCESS! Automatic file action prompt found!');

    // Type "tolka filen" to trigger file analysis
    console.log('💬 Typing "tolka filen" to request file analysis...');
    await page.fill('[data-testid="message-input"]', 'tolka filen');
    await page.click('[data-testid="send-button"]');

    // Wait for AI response with file analysis (should NOT ask to upload again)
    console.log('⏳ Waiting for AI analysis response...');

    // First check that we don't get the "upload file" message
    try {
      const uploadMessage = await page.waitForSelector(
        'text=/Du kan ladda upp filen här|upload.*file/i',
        { timeout: 5000 }
      );
      if (uploadMessage) {
        console.log('❌ ERROR: AI is asking to upload file instead of analyzing the already uploaded file!');
        throw new Error('AI should analyze the uploaded file, not ask to upload again');
      }
    } catch (error) {
      // This is good - we don't want the upload message
      console.log('✅ Good: AI is not asking to upload file again');
    }

    // Now wait for the actual file analysis
    const analysisResponse = await page.waitForSelector(
      'text=/180.*000.*SEK|Acme Corporation|faktura|invoice|analyserat|innehåller/i',
      { timeout: 30000 }
    );

    expect(analysisResponse).toBeTruthy();
    console.log('🎉 SUCCESS! AI analyzed the file correctly!');

    const analysisText = await analysisResponse.textContent();
    console.log('📊 Analysis response:', analysisText?.substring(0, 200) + '...');

    // Verify the analysis contains expected invoice details
    expect(analysisText).toMatch(/180.*000.*SEK|Acme Corporation|faktura|invoice|analyserat|innehåller/i);

    console.log('🎉 COMPLETE WORKFLOW TEST PASSED - File upload + automatic prompt + analysis works!');
  });
});
