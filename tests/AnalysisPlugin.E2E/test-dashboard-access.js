const { chromium } = require('playwright');

async function testDashboardAccess() {
  console.log('🚀 Startar Playwright-test för Dashboard-åtkomst...');
  
  const browser = await chromium.launch({ 
    headless: false,
    slowMo: 1000
  });
  
  const context = await browser.newContext({
    ignoreHTTPSErrors: true
  });
  
  const page = await context.newPage();
  
  try {
    console.log('📊 Navigerar till Aspire Dashboard...');
    
    // Testa olika möjliga dashboard-URLs
    const dashboardUrls = [
      'https://localhost:17024',
      'http://localhost:17024', 
      'https://localhost:15000',
      'http://localhost:15000'
    ];
    
    let dashboardFound = false;
    let workingUrl = '';
    
    for (const url of dashboardUrls) {
      try {
        console.log(`🔍 Testar dashboard URL: ${url}`);
        await page.goto(url, { waitUntil: 'networkidle', timeout: 10000 });
        
        // Kontrollera om vi kom till en dashboard-sida
        const title = await page.title();
        console.log(`📄 Sidtitel: "${title}"`);
        
        if (title.toLowerCase().includes('aspire') ||
            title.toLowerCase().includes('dashboard') ||
            title.toLowerCase().includes('apphost') ||
            title.toLowerCase().includes('resources')) {
          dashboardFound = true;
          workingUrl = url;
          console.log(`✅ Dashboard hittad på: ${url}`);
          break;
        }

        // Testa även om sidan har dashboard-element
        try {
          const hasResources = await page.locator('text=Resources').first().isVisible({ timeout: 3000 });
          if (hasResources) {
            dashboardFound = true;
            workingUrl = url;
            console.log(`✅ Dashboard hittad på: ${url} (via Resources-element)`);
            break;
          }
        } catch {
          // Ignorera fel
        }
      } catch (error) {
        console.log(`❌ Kunde inte nå: ${url} - ${error.message}`);
      }
    }
    
    if (!dashboardFound) {
      console.log('❌ Ingen dashboard hittades på de testade URL:erna');
      return false;
    }
    
    console.log('🔍 Letar efter loggar och resurser...');
    
    // Vänta lite för att sidan ska ladda helt
    await page.waitForTimeout(3000);
    
    // Leta efter olika dashboard-element
    const elements = [
      'Resources',
      'Logs', 
      'Traces',
      'Metrics',
      'agent',
      'webchat'
    ];
    
    const foundElements = [];
    
    for (const element of elements) {
      try {
        const locator = page.locator(`text=${element}`).first();
        if (await locator.isVisible({ timeout: 2000 })) {
          foundElements.push(element);
          console.log(`✅ Hittade: ${element}`);
        }
      } catch {
        console.log(`❌ Hittade inte: ${element}`);
      }
    }
    
    // Försök klicka på Logs om det finns
    try {
      const logsLink = page.locator('text=Logs, a:has-text("Logs"), [href*="logs"]').first();
      if (await logsLink.isVisible({ timeout: 5000 })) {
        console.log('📋 Klickar på Logs...');
        await logsLink.click();
        await page.waitForTimeout(2000);
        
        // Kontrollera om vi ser loggdata
        const logContent = await page.locator('pre, code, .log-entry, [class*="log"]').first().textContent({ timeout: 5000 });
        if (logContent && logContent.length > 0) {
          console.log('✅ Loggdata hittades');
          console.log(`📝 Första loggraden: "${logContent.substring(0, 100)}..."`);
        }
      }
    } catch (error) {
      console.log(`⚠️ Kunde inte komma åt loggar: ${error.message}`);
    }
    
    // Försök hitta Agent och WebChat resurser
    try {
      const resourcesLink = page.locator('text=Resources, a:has-text("Resources"), [href*="resources"]').first();
      if (await resourcesLink.isVisible({ timeout: 5000 })) {
        console.log('🔧 Klickar på Resources...');
        await resourcesLink.click();
        await page.waitForTimeout(2000);
        
        // Leta efter Agent och WebChat
        const agentVisible = await page.locator('text=agent, text=Agent').first().isVisible({ timeout: 3000 });
        const webchatVisible = await page.locator('text=webchat, text=WebChat').first().isVisible({ timeout: 3000 });
        
        if (agentVisible) {
          console.log('✅ Agent-resurs hittades');
        }
        if (webchatVisible) {
          console.log('✅ WebChat-resurs hittades');
        }
      }
    } catch (error) {
      console.log(`⚠️ Kunde inte komma åt resurser: ${error.message}`);
    }
    
    // Ta skärmdump av dashboard
    await page.screenshot({ 
      path: `dashboard-access-${new Date().toISOString().replace(/[:.]/g, '-')}.png`,
      fullPage: true 
    });
    
    console.log('📸 Dashboard-skärmdump sparad');
    
    // Sammanfattning
    console.log('\n📊 DASHBOARD-VALIDERING:');
    console.log(`✅ Dashboard tillgänglig på: ${workingUrl}`);
    console.log(`✅ Hittade element: ${foundElements.join(', ')}`);
    
    return foundElements.length > 0;
    
  } catch (error) {
    console.error('❌ Dashboard-test misslyckades:', error.message);
    
    await page.screenshot({ 
      path: `dashboard-error-${new Date().toISOString().replace(/[:.]/g, '-')}.png`,
      fullPage: true 
    });
    
    return false;
  } finally {
    await browser.close();
  }
}

// Kör testet
testDashboardAccess().then(success => {
  if (success) {
    console.log('\n🎉 DASHBOARD-TEST LYCKADES!');
    process.exit(0);
  } else {
    console.log('\n💥 DASHBOARD-TEST MISSLYCKADES!');
    process.exit(1);
  }
}).catch(error => {
  console.error('💥 Oväntat fel:', error);
  process.exit(1);
});
