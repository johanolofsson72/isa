const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: false });
  const page = await browser.newPage();
  
  console.log('Navigating to WebChat...');
  await page.goto('https://localhost:7041/', { waitUntil: 'networkidle' });

  // Wait for Blazor to initialize
  await page.waitForTimeout(5000);

  // Check if login is required
  const passwordField = page.locator('input[type="password"], #password');
  if (await passwordField.count() > 0) {
    console.log('Login required, logging in...');
    await passwordField.fill('isa72');
    await page.locator('button:has-text("Logga in")').click();
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(5000);
    console.log('Login completed');
  }
  
  console.log('Page title:', await page.title());
  
  // Take screenshot
  await page.screenshot({ path: 'webchat-debug.png', fullPage: true });
  
  // Get all input elements
  const inputs = await page.locator('input, textarea').all();
  console.log(`Found ${inputs.length} input elements:`);
  
  for (let i = 0; i < inputs.length; i++) {
    const input = inputs[i];
    const type = await input.getAttribute('type');
    const placeholder = await input.getAttribute('placeholder');
    const id = await input.getAttribute('id');
    const className = await input.getAttribute('class');
    const visible = await input.isVisible();
    
    console.log(`Input ${i}: type=${type}, placeholder=${placeholder}, id=${id}, class=${className}, visible=${visible}`);
  }
  
  // Get all buttons
  const buttons = await page.locator('button').all();
  console.log(`Found ${buttons.length} buttons:`);
  
  for (let i = 0; i < buttons.length; i++) {
    const button = buttons[i];
    const text = await button.textContent();
    const visible = await button.isVisible();
    
    console.log(`Button ${i}: text="${text}", visible=${visible}`);
  }
  
  // Get page content
  const content = await page.content();
  console.log('Page content length:', content.length);
  
  // Wait a bit more to see if anything loads
  console.log('Waiting for additional content...');
  await page.waitForTimeout(10000);
  
  await browser.close();
})();
