# AccountingAgent Documentation Index

## 📚 Core Documentation

### **Getting Started**
- [**README.md**](../README.md) - Main project overview, setup, and quick start guide
- [**DEPLOYMENT_GUIDE.md**](DEPLOYMENT_GUIDE.md) - Production deployment instructions
- [**API_REFERENCE.md**](API_REFERENCE.md) - Complete API documentation

### **Architecture & Development**
- [**PLUGINS.md**](PLUGINS.md) - Plugin architecture and development guide
- [**TEST_ARCHITECTURE_GUIDE.md**](TEST_ARCHITECTURE_GUIDE.md) - Mock-free testing patterns and CI/CD
- [**QUARTZ_WORKFLOW_SYSTEM_GUIDE.md**](QUARTZ_WORKFLOW_SYSTEM_GUIDE.md) - Workflow system guide

### **Testing & Quality**
- [**NIGHTLY_TESTS.md**](NIGHTLY_TESTS.md) - Automated testing and email reporting
- [**DOCUMENTATION_UPDATE_SUMMARY.md**](DOCUMENTATION_UPDATE_SUMMARY.md) - Recent documentation updates

## 🔌 Plugin System (10 Plugins - 265 Tests)

### **Communication Plugins (140 tests)**
- **SmsPlugin** (25 tests) - SMS via Twilio
- **WhatsAppPlugin** (39 tests) - WhatsApp Business API via Twilio
- **SlackPlugin** (37 tests) - Slack meddelanden och kanaler
- **MicrosoftOfficePlugin** (50 tests) - Teams, OneDrive, Microsoft 365 Kalender

### **Cloud Storage Plugins (27 tests)**
- **GoogleWorkspacePlugin** (9 tests) - Google Drive, Gmail, Google Calendar
- **DropboxPlugin** (18 tests) - Dropbox filhantering

### **Tools & Services Plugins (98 tests)**
- **DateTimePlugin** (35 tests) - Datum/tid-funktioner med TimeZoneDB API
- **GoogleSearchPlugin** (19 tests) - Webbsökning via Google Custom Search API
- **OpenMeteoPlugin** (16 tests) - Väderdata och prognoser
- **OpenCagePlugin** (17 tests) - Geocoding och platsdata

## 🧪 Testing & CI/CD

### **Mock-Free Testing Architecture**
- **265 plugin unit tests** across 10 plugins
- **Real objects** instead of mocks
- **TestConfiguration class** for IConfiguration
- **NullLogger<Plugin>.Instance** for logging
- **[Trait("Category", "CI")]** for CI/CD tests

### **Automated Testing**
- **Nightly tests**: 02:00 UTC every night
- **GitHub Actions CI/CD**: Automated testing on push/PR
- **Email reporting**: Failures sent to AccountingAgent team
- **Build system**: .NET 9 with warnings-as-errors

## 🏢 Business & Roadmap

### **Bokföringsbyrå Specialization**
- [**BOKFÖRINGSBYRÅ_PLUGINS_ROADMAP.md**](BOKFÖRINGSBYRÅ_PLUGINS_ROADMAP.md) - Complete accounting firm plugin roadmap
- [**PHASE_2_COMPLETION_REPORT.md**](PHASE_2_COMPLETION_REPORT.md) - Phase 2 completion status
- [**PHASE_3_COMPLETION_REPORT.md**](PHASE_3_COMPLETION_REPORT.md) - Phase 3 completion status

### **Implementation Progress**
- [**IMPLEMENTATION_PROGRESS_REPORT.md**](IMPLEMENTATION_PROGRESS_REPORT.md) - Overall implementation status
- [**DYNAMIC_PLUGIN_DISCOVERY_IMPLEMENTATION.md**](DYNAMIC_PLUGIN_DISCOVERY_IMPLEMENTATION.md) - Plugin discovery system
- [**PLUGIN_AUTO_REGISTRATION.md**](PLUGIN_AUTO_REGISTRATION.md) - Automatic plugin registration

## 🔧 Technical Guides

### **Workflow System**
- [**WorkflowScheduling.md**](WorkflowScheduling.md) - Workflow scheduling guide
- [**WorkflowSchedulerGuide.md**](WorkflowSchedulerGuide.md) - Scheduler implementation guide
- [**WorkflowTransitionBugFix.md**](WorkflowTransitionBugFix.md) - Workflow transition fixes

### **Integration & Standards**
- [**MCP-README.md**](MCP-README.md) - Model Context Protocol integration
- [**MCP_STANDARDS_INTEGRATION.md**](MCP_STANDARDS_INTEGRATION.md) - MCP standards compliance
- [**ToolNameEnforcement.md**](ToolNameEnforcement.md) - Tool naming conventions

### **Specific Plugins**
- [**WhatsAppPlugin.md**](WhatsAppPlugin.md) - WhatsApp plugin detailed guide
- [**SmartAgentGuide.md**](SmartAgentGuide.md) - Smart agent mobile guide

## 🐛 Troubleshooting & Fixes

### **Docker & Deployment**
- [**DockerContainerFixes.md**](DockerContainerFixes.md) - Docker container startup fixes
- [**WorkflowTransitionTroubleshooting.md**](WorkflowTransitionTroubleshooting.md) - Workflow troubleshooting

### **Development Notes**
- [**Prompt-Users-Tools-Recomodations.txt**](Prompt-Users-Tools-Recomodations.txt) - Development recommendations
- [**accountingagent.txt**](accountingagent.txt) - System notes

## 📊 System Status

### **Production Ready**
- ✅ **10 plugins** with comprehensive unit tests
- ✅ **Mock-free testing architecture** with CI/CD
- ✅ **Automated nightly testing** with email reporting
- ✅ **.NET 9** with warnings-as-errors build system
- ✅ **Docker deployment** ready for Linux clusters

### **Under Development**
- 🔄 **FortnoxPlugin** - Swedish accounting integration
- 🔄 **SebPlugin** - Swedish bank integration

### **Quality Metrics**
- **265 unit tests** across all plugins
- **100% CI/CD coverage** for plugin tests
- **Automated quality gates** with warnings-as-errors
- **Nightly monitoring** with email alerts

## 🔗 Quick Links

### **Development**
```bash
# Run all CI tests
dotnet test --configuration Release --filter "Category=CI"

# Test specific plugin
dotnet test tests/SmsPlugin.Tests/ --configuration Release

# Test email functionality
bash scripts/test-email-report.sh
```

### **Monitoring**
- **Health Dashboard**: http://localhost:7001/health.html
- **API Documentation**: http://localhost:7001/swagger
- **GitHub Actions**: [Nightly Tests & Report workflow]

### **Support**
- **Email Reports**: Sent to AccountingAgent team on test failures
- **Documentation**: All guides in this docs/ directory
- **Architecture**: See PLUGINS.md and TEST_ARCHITECTURE_GUIDE.md

---

**Last Updated**: August 2025  
**Documentation Status**: ✅ Current and Comprehensive
