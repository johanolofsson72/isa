const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: false });
  const page = await browser.newPage();

  // Listen to console logs
  page.on('console', msg => console.log('BROWSER:', msg.text()));
  page.on('pageerror', error => console.log('PAGE ERROR:', error.message));
  
  console.log('Navigating to WebChat...');
  await page.goto('https://localhost:7041/', { waitUntil: 'networkidle' });
  
  // Wait for Blazor to initialize
  await page.waitForTimeout(5000);
  
  // Login
  const passwordField = page.locator('input[type="password"], #password');
  if (await passwordField.count() > 0) {
    console.log('Login required, logging in...');
    await passwordField.fill('isa72');
    await page.locator('button:has-text("Logga in")').click();
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(5000);
    console.log('Login completed');
  }
  
  // Find chat input
  const chatInput = page.locator('#messageInput, .message-input');
  await chatInput.waitFor({ state: 'visible', timeout: 10000 });
  
  console.log('Sending test message...');
  await chatInput.fill('Testa AnalysisPlugin med enkel SIE4-data');

  // Try to find and click send button first
  const sendButton = page.locator('[data-testid="send-button"]');
  if (await sendButton.count() > 0) {
    console.log('Found send button, clicking...');
    await sendButton.click();
  } else {
    console.log('No send button found, pressing Enter...');
    await chatInput.press('Enter');
  }

  console.log('Message sent, waiting for response...');
  
  // Wait for response (look for new messages)
  await page.waitForTimeout(10000);
  
  // Get all messages
  const messages = await page.locator('.message, .chat-message').all();
  console.log(`Found ${messages.length} messages:`);
  
  for (let i = 0; i < messages.length; i++) {
    const message = messages[i];
    const text = await message.textContent();
    console.log(`Message ${i}: "${text}"`);
  }
  
  await browser.close();
})();
