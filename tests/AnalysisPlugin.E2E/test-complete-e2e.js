const { chromium } = require('playwright');

async function runCompleteE2ETest() {
  console.log('🚀 Startar komplett E2E-test för AnalysisPlugin...');
  
  const browser = await chromium.launch({ 
    headless: false,
    slowMo: 1000
  });
  
  const context = await browser.newContext({
    ignoreHTTPSErrors: true
  });
  
  let testResults = {
    webchat: false,
    dashboard: false,
    analysisPlugin: false
  };
  
  try {
    // Test 1: WebChat SIE-filförfrågan
    console.log('\n📱 === TEST 1: WebChat SIE-filförfrågan ===');
    const webchatPage = await context.newPage();
    
    await webchatPage.goto('https://localhost:7041/', { waitUntil: 'networkidle' });
    await webchatPage.waitForTimeout(2000);
    
    // Inloggning
    const loginRequired = await webchatPage.locator('input[type="password"]').isVisible();
    if (loginRequired) {
      console.log('🔐 Loggar in...');
      await webchatPage.fill('input[type="password"]', 'isa72');
      await webchatPage.press('input[type="password"]', 'Enter');
      await webchatPage.waitForTimeout(2000);
    }
    
    // Skicka SIE-förfrågan
    const chatInput = await webchatPage.locator('input[type="text"], textarea').filter({ hasNotText: 'file' }).first();
    await chatInput.waitFor({ state: 'visible', timeout: 10000 });
    
    const message = 'Kan du hämta en SIE fil för 2024 och skriva ut de tre första raderna från den här i chatten';
    await chatInput.fill(message);
    
    const sendButton = await webchatPage.locator('button:has-text("Skicka"), button[type="submit"], .send-button, button:has(svg), [class*="send"]').first();
    await sendButton.waitFor({ state: 'visible', timeout: 5000 });
    await sendButton.click();
    
    console.log('📤 Meddelande skickat, väntar på AI-svar...');
    await webchatPage.waitForTimeout(8000);
    
    // Validera svar
    const messages = await webchatPage.locator('.message, .chat-message, [class*="message"]').all();
    let aiResponse = '';
    
    for (let i = 0; i < messages.length; i++) {
      const messageText = await messages[i].textContent();
      if (messageText && messageText.trim().length > 10 && 
          !messageText.includes(message) && 
          !messageText.includes('Välkommen') &&
          !messageText.match(/^\d{2}:\d{2}$/)) {
        aiResponse = messageText.trim();
      }
    }
    
    if (aiResponse && aiResponse.length > 0) {
      console.log('✅ WebChat-test LYCKADES');
      console.log(`📝 AI-svar: "${aiResponse.substring(0, 150)}..."`);
      testResults.webchat = true;
      
      // Kontrollera om AnalysisPlugin används
      if (aiResponse.toLowerCase().includes('sie') || 
          aiResponse.toLowerCase().includes('fortnox') ||
          aiResponse.toLowerCase().includes('bokföring')) {
        console.log('✅ AnalysisPlugin verkar användas');
        testResults.analysisPlugin = true;
      }
    } else {
      console.log('❌ WebChat-test MISSLYCKADES - inget AI-svar');
    }
    
    await webchatPage.screenshot({ 
      path: `complete-e2e-webchat-${new Date().toISOString().replace(/[:.]/g, '-')}.png`,
      fullPage: true 
    });
    
    await webchatPage.close();
    
    // Test 2: Dashboard-åtkomst
    console.log('\n📊 === TEST 2: Dashboard-åtkomst ===');
    const dashboardPage = await context.newPage();
    
    await dashboardPage.goto('https://localhost:17024', { waitUntil: 'networkidle' });
    await dashboardPage.waitForTimeout(3000);
    
    const title = await dashboardPage.title();
    console.log(`📄 Dashboard-titel: "${title}"`);
    
    if (title.toLowerCase().includes('apphost') || title.toLowerCase().includes('resources')) {
      console.log('✅ Dashboard-test LYCKADES');
      testResults.dashboard = true;
      
      // Kontrollera resurser
      const agentVisible = await dashboardPage.locator('text=agent').first().isVisible({ timeout: 3000 });
      const webchatVisible = await dashboardPage.locator('text=webchat').first().isVisible({ timeout: 3000 });
      
      if (agentVisible && webchatVisible) {
        console.log('✅ Agent och WebChat synliga i dashboard');
      }
    } else {
      console.log('❌ Dashboard-test MISSLYCKADES');
    }
    
    await dashboardPage.screenshot({ 
      path: `complete-e2e-dashboard-${new Date().toISOString().replace(/[:.]/g, '-')}.png`,
      fullPage: true 
    });
    
    await dashboardPage.close();
    
  } catch (error) {
    console.error('❌ E2E-test misslyckades:', error.message);
  } finally {
    await browser.close();
  }
  
  // Sammanfattning
  console.log('\n🎯 === TESTRESULTAT ===');
  console.log(`📱 WebChat: ${testResults.webchat ? '✅ LYCKADES' : '❌ MISSLYCKADES'}`);
  console.log(`📊 Dashboard: ${testResults.dashboard ? '✅ LYCKADES' : '❌ MISSLYCKADES'}`);
  console.log(`🔧 AnalysisPlugin: ${testResults.analysisPlugin ? '✅ ANVÄNDS' : '❌ ANVÄNDS EJ'}`);
  
  const allPassed = testResults.webchat && testResults.dashboard;
  console.log(`\n🏆 ÖVERGRIPANDE RESULTAT: ${allPassed ? '✅ ALLA TESTER LYCKADES' : '❌ VISSA TESTER MISSLYCKADES'}`);
  
  return allPassed;
}

// Kör testet
runCompleteE2ETest().then(success => {
  process.exit(success ? 0 : 1);
}).catch(error => {
  console.error('💥 Oväntat fel:', error);
  process.exit(1);
});
