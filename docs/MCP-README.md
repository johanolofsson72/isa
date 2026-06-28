# ISA MCP Server Configuration
> NOTE (2025-08-19): Dubbletten `src/Agent/mcp-server-config.json` har tagits bort. Använd `src/Agent/wwwroot/mcp-server-config.json` för klientupptäckt och `mcp-config.json` i repo-roten för lokal utveckling.



## 📋 **Översikt**

ISA Accounting Agent fungerar som en fullständig MCP (Model Context Protocol) server som kan integreras med Augment Code och andra MCP-kompatibla klienter.

## 🗂️ **Konfigurationsfiler**

### **Produktion: `mcp-server-config.json`**
- **Användning**: Kopiera till `/mnt/hot/docker/isa/config/` på servern
- **URL**: `https://mcp.example.com/mcp`
- **Miljö**: Production
- **SSL**: Aktiverat med Let's Encrypt
- **Skalning**: 1-3 instanser baserat på CPU/minne

### **Lokal utveckling: `mcp-server-config.local.json`**
- **Användning**: För lokal utveckling och testning
- **URL**: `https://localhost:7042/mcp`
- **Miljö**: Development
- **SSL**: Self-signed certifikat

## 🚀 **Installation på server**

### **1. Kopiera konfigurationsfil:**
```bash
# På din lokala maskin
scp config/mcp-server-config.json deploy@example.com:/mnt/hot/docker/isa/config/

# På servern
cd /mnt/hot/docker/isa/config
sudo chown root:root mcp-server-config.json
sudo chmod 644 mcp-server-config.json
```

### **2. Verifiera att ISA körs:**
```bash
# Kontrollera att containers körs
docker service ls | grep isa_

# Testa MCP-endpointen
curl -k https://mcp.example.com/health
```

### **3. Testa MCP-funktionalitet:**
```bash
# Test initialize
curl -k -X POST https://mcp.example.com/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {
      "protocolVersion": "2024-11-05",
      "capabilities": {},
      "clientInfo": {"name": "test", "version": "1.0.0"}
    }
  }'
```

## 🔧 **MCP Endpoints**

### **Huvudendpoint:**
- **URL**: `https://mcp.example.com/mcp`
- **Metod**: POST
- **Content-Type**: application/json

### **Stödda MCP-metoder:**
- `initialize` - Initialisera MCP-session
- `initialized` - Bekräfta initialisering
- `tools/list` - Lista tillgängliga verktyg
- `tools/call` - Anropa verktyg
- `chat.completions.create` - Chat-komplettering
- `server.capabilities` - Server-kapaciteter
- `progress/status` - Progress-status

### **Hjälpendpoints:**
- **Health**: `https://mcp.example.com/health`
- **Capabilities**: `https://mcp.example.com/api/mcp/capabilities`
- **Tools**: `https://mcp.example.com/api/mcp/tools`
- **Stats**: `https://mcp.example.com/api/mcp/stats`

## 🎯 **Augment Code Integration**

### **Automatisk upptäckt:**
Augment Code kommer automatiskt att upptäcka ISA MCP-servern om konfigurationsfilen finns på rätt plats.

### **Manuell konfiguration:**
Om automatisk upptäckt inte fungerar, lägg till manuellt i Augment Code:

```json
{
  "mcpServers": {
    "isa-accounting-agent": {
      "transport": {
        "type": "http",
        "url": "https://mcp.example.com/mcp"
      }
    }
  }
}
```

## 🔍 **Felsökning**

### **Vanliga problem:**

#### **1. Connection refused**
```bash
# Kontrollera att ISA körs
docker service ps isa_agent isa_web

# Kontrollera portar
netstat -tlnp | grep :7042
```

#### **2. SSL-certifikatfel**
```bash
# Kontrollera certifikat
openssl s_client -connect mcp.example.com:443 -servername mcp.example.com
```

#### **3. MCP-metod inte implementerad**
```bash
# Kontrollera loggar
docker service logs isa_agent --tail 50
```

### **Debug-kommandon:**
```bash
# Testa alla MCP-metoder
curl -k -X POST https://mcp.example.com/mcp -H "Content-Type: application/json" -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0.0"}}}'

curl -k -X POST https://mcp.example.com/mcp -H "Content-Type: application/json" -d '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}'

curl -k -X POST https://mcp.example.com/mcp -H "Content-Type: application/json" -d '{"jsonrpc":"2.0","id":3,"method":"server.capabilities","params":{}}'
```

## 📊 **Monitoring**

### **Health Check:**
- **URL**: `https://mcp.example.com/health`
- **Intervall**: 30 sekunder
- **Timeout**: 5 sekunder

### **Metrics:**
- **URL**: `https://mcp.example.com/metrics`
- **Format**: Prometheus

### **Logging:**
- **Nivå**: Information (produktion), Debug (utveckling)
- **Format**: Strukturerad JSON
- **Plats**: Docker service logs

## 🔐 **Säkerhet**

### **CORS:**
- Tillåtna origins: augmentcode.com, github.com, vscode://
- Tillåtna metoder: GET, POST, OPTIONS
- Tillåtna headers: Content-Type, Authorization, X-Session-Id

### **Rate Limiting:**
- 100 requests per minut
- Burst limit: 20 requests
- Automatisk backoff vid överbelastning

### **SSL/TLS:**
- Let's Encrypt certifikat
- HSTS aktiverat
- HTTP → HTTPS redirect

## 📈 **Performance**

### **Skalning:**
- Min instanser: 1
- Max instanser: 3
- CPU target: 70%
- Memory target: 80%

### **Caching:**
- Capabilities cache: 5 minuter
- Tools list cache: 5 minuter
- Connection pooling aktiverat

## 🎉 **Funktioner**

### **11 Plugins med 47 funktioner:**
- **SMS Plugin** - Skicka SMS via 46elks
- **WhatsApp Plugin** - WhatsApp-meddelanden
- **Google Workspace** - Drive, Calendar, Chat
- **DateTime Plugin** - Datum/tid-hantering
- **Dropbox Plugin** - Filhantering
- **Google Search** - Sökfunktioner
- **Slack Plugin** - Slack-integration
- **OpenCage Plugin** - Geokodning
- **Microsoft Office** - Office 365-integration
- **Mail Plugin** - E-post via Mailjet
- **OpenMeteo Plugin** - Väderdata

### **AI-funktioner:**
- Chat-komplettering med GPT-4
- Token limit-hantering
- System prompt-injection
- Session-hantering
- Streaming-stöd (kommande)

**ISA MCP Server är nu redo för produktion och Augment Code-integration!** 🚀
