# WhatsApp Plugin Documentation

## Overview
WhatsAppPlugin provides WhatsApp messaging capabilities using the Twilio API. It follows the same structure and registration pattern as MailPlugin and other plugins in the system.

## Configuration

### appsettings.json
```json
{
  "WhatsApp": {
    "AccountSid": "YOUR_TWILIO_ACCOUNT_SID",
    "AuthToken": "YOUR_TWILIO_AUTH_TOKEN", 
    "FromNumber": "+14155238886",
    "Contacts": {
      "Anna": "+46701234567"
    }
  }
}
```

### Configuration Parameters
- **AccountSid**: Your Twilio Account SID
- **AuthToken**: Your Twilio Auth Token
- **FromNumber**: WhatsApp sender number (in E.164 format)
- **Contacts**: Optional contact mapping for named contacts

## Available Functions

### 1. SendMessageAsync
Sends a WhatsApp message to a phone number.

**Parameters:**
- `to` (string): Recipient phone number in E.164 format (e.g., +46701234567)
- `message` (string): Message content to send

**Returns:** Success/failure message with Twilio message ID

### 2. SendMessageToContactAsync
Sends a WhatsApp message to a named contact from configuration.

**Parameters:**
- `contactName` (string): Name of the contact (e.g., "Anna")
- `message` (string): Message content to send

**Returns:** Success/failure message

### 3. GetContactAsync
Retrieves a contact's phone number by name.

**Parameters:**
- `contactName` (string): Name of the contact to look up

**Returns:** Phone number or error message

### 4. GetConfigurationStatusAsync
Gets current plugin configuration for debugging.

**Returns:** Configuration status information

## Usage Examples

### In Workflows (YAML)
```yaml
- id: send-whatsapp-step
  prompt: "Skicka ett WhatsApp-meddelande till Anna med texten: 'Hej Anna! Detta är ett test-meddelande.'"
  order: 1
  actionType: plugin
```

### Direct Function Calls
```csharp
// Send to phone number
await whatsAppPlugin.SendMessageAsync("+46701234567", "Hello from accounting agent!");

// Send to named contact
await whatsAppPlugin.SendMessageToContactAsync("Anna", "Hello Anna!");

// Get contact info
var phoneNumber = await whatsAppPlugin.GetContactAsync("Anna");
```

## Setup Instructions

### Development/Testing (Twilio Sandbox)
1. Go to [Twilio WhatsApp Sandbox](https://www.twilio.com/console/sms/whatsapp/sandbox)
2. Use sandbox number as `FromNumber` (e.g., +14155238886)
3. Recipients must join sandbox by sending join code to sandbox number
4. Perfect for testing and development

### Production Setup
1. Get approved WhatsApp Business Profile through Twilio
2. Verify sender number is authorized for WhatsApp messaging
3. Ensure number is in E.164 format
4. Check Twilio Console that number is WhatsApp-enabled

## Error Handling

The plugin includes comprehensive error handling for:
- Invalid phone number formats
- WhatsApp channel configuration issues
- Twilio API errors
- Network connectivity issues
- Missing configuration

Common error codes:
- **21211**: Invalid phone number format
- **63007**: Sender not authorized for WhatsApp  
- **21614**: Message body is required

## Registration

The plugin is automatically registered in `KernelExtensions.cs`:

```csharp
var whatsAppPlugin = new WhatsApp.Plugin(
    configuration,
    loggerFactory.CreateLogger<WhatsApp.Plugin>()
);
kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(whatsAppPlugin, "WhatsApp"));
```

## Dependencies

- Twilio SDK
- Microsoft.SemanticKernel
- Microsoft.Extensions.Configuration
- Microsoft.Extensions.Logging

## Notes

- All phone numbers must be in E.164 format (+CountryCodeNumber)
- WhatsApp messages are subject to Twilio's policies and WhatsApp's business policies
- For production use, ensure compliance with WhatsApp Business API terms
- The plugin supports both direct phone number messaging and named contact messaging
