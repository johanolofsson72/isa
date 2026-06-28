const { chromium } = require('playwright');

async function testSieFileRequest() {
  console.log('🚀 Startar Playwright-test för SIE-filförfrågan...');
  
  const browser = await chromium.launch({ 
    headless: false, // Visa webbläsaren så vi kan se vad som händer
    slowMo: 1000 // Sakta ner för att kunna följa med
  });
  
  const context = await browser.newContext({
    ignoreHTTPSErrors: true // Ignorera SSL-certifikatfel för localhost
  });
  
  const page = await context.newPage();
  
  try {
    console.log('📱 Navigerar till WebChat...');
    await page.goto('https://localhost:7041/', { waitUntil: 'networkidle' });
    
    // Vänta på att sidan laddas
    await page.waitForTimeout(2000);
    
    // Kontrollera om inloggning krävs
    const loginRequired = await page.locator('input[type="password"]').isVisible();
    
    if (loginRequired) {
      console.log('🔐 Inloggning krävs, loggar in...');
      await page.fill('input[type="password"]', 'isa72');
      await page.press('input[type="password"]', 'Enter');
      await page.waitForTimeout(2000);
      console.log('✅ Inloggning slutförd');
    }
    
    // Vänta på att WebChat-gränssnittet laddas
    console.log('⏳ Väntar på WebChat-gränssnitt...');
    await page.waitForTimeout(3000);

    // Hitta chattinmatningsfältet (undvik filuppladdningsfält)
    const chatInput = await page.locator('input[type="text"], textarea').filter({ hasNotText: 'file' }).first();
    await chatInput.waitFor({ state: 'visible', timeout: 10000 });
    
    console.log('💬 Skickar SIE-filförfrågan...');
    const message = 'Använd GetSIEAsync för att hämta SIE-data och visa de första raderna';
    
    await chatInput.fill(message);
    
    // Hitta och klicka på skicka-knappen
    const sendButton = await page.locator('button:has-text("Skicka"), button[type="submit"], .send-button, button:has(svg), [class*="send"]').first();
    await sendButton.waitFor({ state: 'visible', timeout: 5000 });
    await sendButton.click();
    
    console.log('📤 Meddelande skickat, väntar på svar...');
    
    // Vänta på AI-svar (längre timeout för denna typ av förfrågan)
    await page.waitForTimeout(5000);
    
    // Hämta alla meddelanden
    const messages = await page.locator('.message, .chat-message, [class*="message"]').all();
    console.log(`📨 Hittade ${messages.length} meddelanden totalt`);
    
    let aiResponse = '';
    let userMessage = 'Kan du hämta en SIE fil för 2024';

    for (let i = 0; i < messages.length; i++) {
      const messageText = await messages[i].textContent();
      console.log(`Meddelande ${i}: "${messageText?.trim().substring(0, 100)}..."`);

      // Leta efter AI:ns svar - det som kommer efter användarens meddelande och inte är tomt
      if (messageText && messageText.trim().length > 10 &&
          !messageText.includes(userMessage) &&
          !messageText.includes('Välkommen') &&
          !messageText.match(/^\d{2}:\d{2}$/)) {
        aiResponse = messageText.trim();
      }
    }
    
    // Validera att vi fick ett svar
    console.log('\n🔍 VALIDERING:');
    
    if (!aiResponse || aiResponse.length === 0) {
      console.log('❌ MISSLYCKADES: Inget AI-svar mottaget');
      return false;
    }
    
    console.log('✅ AI-svar mottaget');
    console.log(`📝 Svar (första 200 tecken): "${aiResponse.substring(0, 200)}..."`);
    
    // Kontrollera om svaret innehåller SIE-relaterat innehåll
    const containsSieContent = aiResponse.toLowerCase().includes('sie') || 
                              aiResponse.includes('#FLAGGA') || 
                              aiResponse.includes('#PROGRAM') ||
                              aiResponse.includes('fortnox') ||
                              aiResponse.toLowerCase().includes('bokföring');
    
    if (containsSieContent) {
      console.log('✅ Svaret verkar innehålla SIE-relaterat innehåll');
    } else {
      console.log('⚠️  Svaret verkar inte innehålla SIE-relaterat innehåll');
    }
    
    // Ta en skärmdump för dokumentation
    await page.screenshot({ 
      path: `sie-test-result-${new Date().toISOString().replace(/[:.]/g, '-')}.png`,
      fullPage: true 
    });
    
    console.log('📸 Skärmdump sparad');
    
    return true;
    
  } catch (error) {
    console.error('❌ Test misslyckades:', error.message);
    
    // Ta en skärmdump vid fel
    await page.screenshot({ 
      path: `sie-test-error-${new Date().toISOString().replace(/[:.]/g, '-')}.png`,
      fullPage: true 
    });
    
    return false;
  } finally {
    await browser.close();
  }
}

// Kör testet
testSieFileRequest().then(success => {
  if (success) {
    console.log('\n🎉 TEST LYCKADES!');
    process.exit(0);
  } else {
    console.log('\n💥 TEST MISSLYCKADES!');
    process.exit(1);
  }
}).catch(error => {
  console.error('💥 Oväntat fel:', error);
  process.exit(1);
});
