# 🌍 EXTERNAL API SETUP - REAL DATA INTEGRATION

**VIKTIGT:** Systemet använder nu **ENDAST RIKTIGA API:ER** - inga hårdkodade värden!

## 🔑 OBLIGATORISKA API-NYCKLAR

### 1. **Alpha Vantage API** (Rekommenderat)
- **Syfte:** Ekonomiska indikatorer, valutakurser, marknadsdata
- **Kostnad:** Gratis tier (5 calls/minut, 500 calls/dag)
- **Registrering:** https://www.alphavantage.co/support/#api-key
- **Miljövariabel:** `ALPHA_VANTAGE_API_KEY`

```bash
export ALPHA_VANTAGE_API_KEY="your_api_key_here"
```

## 🇸🇪 SVENSKA API:ER (Gratis)

### 2. **Riksbank API**
- **Syfte:** Styrränta, valutakurser, ekonomiska prognoser
- **Kostnad:** Gratis
- **Dokumentation:** https://www.riksbank.se/sv/statistik/sok-rantor--valutakurser/oppet-api/
- **Ingen API-nyckel krävs**

### 3. **SCB API (Statistics Sweden)**
- **Syfte:** BNP, inflation, arbetslöshet, branschstatistik
- **Kostnad:** Gratis
- **Dokumentation:** https://www.scb.se/vara-tjanster/oppna-data/api-for-statistikdatabasen/
- **Ingen API-nyckel krävs**

## 🌍 INTERNATIONELLA API:ER (Gratis)

### 4. **Exchange Rate API**
- **Syfte:** Realtidsvalutakurser
- **Kostnad:** Gratis (1500 requests/månad)
- **Dokumentation:** https://exchangerate-api.com/
- **Ingen API-nyckel krävs för basic tier**

### 5. **ECB API (European Central Bank)**
- **Syfte:** Europeiska ekonomiska prognoser
- **Kostnad:** Gratis
- **Dokumentation:** https://sdw.ecb.europa.eu/
- **Ingen API-nyckel krävs**

### 6. **OECD API**
- **Syfte:** Internationella branschbenchmarks
- **Kostnad:** Gratis
- **Dokumentation:** https://data.oecd.org/
- **Ingen API-nyckel krävs**

## ⚙️ KONFIGURATION

### Miljövariabler (Rekommenderat)
```bash
# Obligatorisk för full funktionalitet
export ALPHA_VANTAGE_API_KEY="your_alpha_vantage_key"

# Valfria (använder defaults om ej angivna)
export API_HTTP_TIMEOUT="30"
export ECONOMIC_CACHE_HOURS="6"
export INDUSTRY_CACHE_HOURS="12"
export FORECAST_CACHE_HOURS="24"
export ENABLE_API_LOGGING="false"
export API_RETRY_ATTEMPTS="2"
export API_RETRY_DELAY="2000"
```

### Docker Environment
```dockerfile
ENV ALPHA_VANTAGE_API_KEY="your_api_key_here"
ENV API_HTTP_TIMEOUT="30"
ENV ECONOMIC_CACHE_HOURS="6"
```

### appsettings.json (Alternativ)
```json
{
  "ExternalApi": {
    "AlphaVantageApiKey": "your_api_key_here",
    "HttpTimeoutSeconds": 30,
    "EconomicIndicatorsCacheHours": 6,
    "IndustryBenchmarksCacheHours": 12,
    "MarketForecastCacheHours": 24,
    "EnableDetailedApiLogging": false,
    "ApiRetryAttempts": 2,
    "RetryDelayMilliseconds": 2000
  }
}
```

## 🚀 SNABBSTART

### 1. Minimal Setup (Endast gratis API:er)
```bash
# Fungerar med begränsad funktionalitet
dotnet run
```

### 2. Rekommenderad Setup (Med Alpha Vantage)
```bash
# Registrera gratis Alpha Vantage API-nyckel
export ALPHA_VANTAGE_API_KEY="your_key_here"
dotnet run
```

### 3. Production Setup
```bash
# Alla miljövariabler konfigurerade
export ALPHA_VANTAGE_API_KEY="your_production_key"
export API_HTTP_TIMEOUT="45"
export ECONOMIC_CACHE_HOURS="4"
export ENABLE_API_LOGGING="true"
dotnet run
```

## 📊 API-ANVÄNDNING

### Datakällor per funktion:

#### 🇸🇪 **Svenska Ekonomiska Indikatorer:**
- **Riksbank:** Styrränta, valutakurser
- **Exchange Rate API:** SEK/EUR, SEK/USD kurser
- **Alpha Vantage:** Inflation, arbetslöshet (om tillgänglig)
- **SCB:** BNP-data, konsumentförtroende

#### 🏭 **Branschbenchmarks:**
- **Alpha Vantage:** Sektorprestanda
- **SCB:** Svenska branschstatistik
- **OECD:** Internationella branschjämförelser

#### 📈 **Marknadsprognoser:**
- **Riksbank:** Ränteprognoser
- **Alpha Vantage:** BNP-prognoser, marknadsvolatilitet
- **ECB:** Europeiska inflationsprognoser

## ⚠️ VIKTIGA BEGRÄNSNINGAR

### Rate Limits:
- **Alpha Vantage Free:** 5 calls/minut, 500 calls/dag
- **Exchange Rate API:** 1500 requests/månad
- **Riksbank/SCB/ECB/OECD:** Inga officiella begränsningar

### Caching:
- **Ekonomiska indikatorer:** 6 timmar (konfigurerbart)
- **Branschbenchmarks:** 12 timmar (konfigurerbart)
- **Marknadsprognoser:** 24 timmar (konfigurerbart)

## 🔧 FELSÖKNING

### Vanliga problem:

#### 1. "No Alpha Vantage API key provided"
```bash
export ALPHA_VANTAGE_API_KEY="your_key_here"
```

#### 2. "Failed to fetch any real economic data"
- Kontrollera internetanslutning
- Verifiera API-nycklar
- Kolla rate limits

#### 3. "API integration required"
- Systemet har inga fallbacks - API:er måste fungera
- Kontrollera API-status på respektive webbsidor

### Debug-läge:
```bash
export ENABLE_API_LOGGING="true"
dotnet run
```

## 📈 KOSTNADSKALKYL

### Gratis Tier (Rekommenderat för utveckling):
- **Alpha Vantage:** $0 (500 calls/dag)
- **Alla andra API:er:** $0
- **Total kostnad:** $0/månad

### Production Tier:
- **Alpha Vantage Premium:** $49.99/månad (unlimited calls)
- **Alla andra API:er:** $0
- **Total kostnad:** ~$50/månad

## 🎯 NÄSTA STEG

1. **Registrera Alpha Vantage API-nyckel** (5 minuter)
2. **Sätt miljövariabel** `ALPHA_VANTAGE_API_KEY`
3. **Testa systemet** med riktiga API-anrop
4. **Övervaka rate limits** i produktion
5. **Uppgradera till Premium** vid behov

**Systemet är nu 100% API-drivet utan hårdkodade värden!** 🚀
