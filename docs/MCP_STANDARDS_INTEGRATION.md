# Microsoft MCP Standards Integration Plan

## 📋 Overview

This document outlines how our dynamic plugin discovery implementation will integrate with Microsoft's Model Context Protocol (MCP) standards and best practices.

## 🔍 Research Summary

Based on comprehensive research of Microsoft's MCP documentation and community examples, here are the key findings:

### **Official MCP Standards**
- **Protocol Version**: 2025-06-18 (latest)
- **Core Specification**: [modelcontextprotocol.io](https://modelcontextprotocol.io/)
- **Microsoft Integration**: Azure AI Foundry Agent Service support
- **Community**: 64.6k+ stars, 663+ contributors, extensive ecosystem

### **Key MCP Concepts**
1. **Tools**: Functions that LLMs can invoke (our plugins)
2. **Resources**: Data sources that can be read
3. **Prompts**: Reusable prompt templates
4. **Capabilities**: Server feature declarations
5. **Security**: Human-in-the-loop, input validation, access controls

## 🏗️ MCP Compliance Requirements

### **1. Tool Schema Validation**
All generated MCP capabilities must include proper JSON Schema:

```json
{
  "name": "send_email",
  "title": "Email Sender",
  "description": "Send emails via Mailjet API",
  "inputSchema": {
    "type": "object",
    "properties": {
      "to": {
        "type": "string",
        "description": "Recipient email address"
      },
      "subject": {
        "type": "string", 
        "description": "Email subject line"
      },
      "body": {
        "type": "string",
        "description": "Email body content"
      }
    },
    "required": ["to", "subject", "body"]
  },
  "outputSchema": {
    "type": "object",
    "properties": {
      "success": {
        "type": "boolean",
        "description": "Whether email was sent successfully"
      },
      "messageId": {
        "type": "string",
        "description": "Unique message identifier"
      }
    },
    "required": ["success"]
  }
}
```

### **2. Capabilities Declaration**
Server must properly declare capabilities:

```json
{
  "capabilities": {
    "tools": {
      "listChanged": true
    },
    "resources": {
      "subscribe": false,
      "listChanged": false
    },
    "prompts": {
      "listChanged": false
    }
  }
}
```

### **3. Error Handling Standards**
Two types of errors:

**Protocol Errors (JSON-RPC):**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "error": {
    "code": -32602,
    "message": "Unknown tool: invalid_tool_name"
  }
}
```

**Tool Execution Errors:**
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "content": [
      {
        "type": "text",
        "text": "Failed to send email: Invalid API key"
      }
    ],
    "isError": true
  }
}
```

### **4. Security Best Practices**
Following Microsoft's guidelines:

**Servers MUST:**
- Validate all tool inputs
- Implement proper access controls
- Rate limit tool invocations
- Sanitize tool outputs

**Clients SHOULD:**
- Prompt for user confirmation on sensitive operations
- Show tool inputs before calling server
- Validate tool results
- Implement timeouts
- Log tool usage for audit

## 🔧 Implementation Integration

### **DynamicMcpCapabilitiesGenerator Updates**

```csharp
public class DynamicMcpCapabilitiesGenerator : IMcpCapabilitiesGenerator
{
    public JsonNode GenerateCapabilities(List<PluginMetadata> plugins)
    {
        var capabilities = new JsonObject
        {
            ["capabilities"] = new JsonObject
            {
                ["tools"] = new JsonObject
                {
                    ["listChanged"] = true
                }
            },
            ["tools"] = GenerateToolsArray(plugins)
        };

        return capabilities;
    }

    private JsonArray GenerateToolsArray(List<PluginMetadata> plugins)
    {
        var tools = new JsonArray();

        foreach (var plugin in plugins.Where(p => p.Enabled))
        {
            foreach (var function in plugin.Functions)
            {
                var tool = new JsonObject
                {
                    ["name"] = $"{plugin.Name}_{function.Name}",
                    ["title"] = function.Description ?? function.Name,
                    ["description"] = GenerateDetailedDescription(plugin, function),
                    ["inputSchema"] = GenerateInputSchema(function),
                    ["outputSchema"] = GenerateOutputSchema(function)
                };

                tools.Add(tool);
            }
        }

        return tools;
    }

    private JsonObject GenerateInputSchema(FunctionMetadata function)
    {
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject()
        };

        var required = new JsonArray();

        foreach (var param in function.Parameters)
        {
            var paramSchema = new JsonObject
            {
                ["type"] = MapTypeToJsonSchema(param.Type),
                ["description"] = param.Description ?? param.Name
            };

            schema["properties"]![param.Name] = paramSchema;

            if (param.IsRequired)
            {
                required.Add(param.Name);
            }
        }

        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        return schema;
    }

    private string MapTypeToJsonSchema(Type type)
    {
        if (type == typeof(string)) return "string";
        if (type == typeof(int) || type == typeof(long)) return "integer";
        if (type == typeof(double) || type == typeof(float)) return "number";
        if (type == typeof(bool)) return "boolean";
        if (type.IsArray || type.IsGenericType) return "array";
        return "object";
    }
}
```

### **McpService Updates**

```csharp
public class McpService : IMcpService
{
    private readonly IDynamicMcpCapabilitiesGenerator _capabilitiesGenerator;
    private readonly IPluginDiscoveryService _pluginDiscovery;

    public async Task<JsonNode> GetCapabilitiesAsync()
    {
        var plugins = await _pluginDiscovery.GetDiscoverablePluginsAsync();
        return _capabilitiesGenerator.GenerateCapabilities(plugins);
    }

    public async Task<JsonNode> ListToolsAsync()
    {
        var capabilities = await GetCapabilitiesAsync();
        return capabilities["tools"];
    }

    public async Task<JsonNode> CallToolAsync(string toolName, JsonNode arguments)
    {
        try
        {
            // Validate tool exists
            var tools = await ListToolsAsync();
            var tool = tools?.AsArray()?.FirstOrDefault(t => t?["name"]?.ToString() == toolName);
            
            if (tool == null)
            {
                return CreateProtocolError(-32602, $"Unknown tool: {toolName}");
            }

            // Validate input against schema
            var inputSchema = tool["inputSchema"];
            if (!ValidateInput(arguments, inputSchema))
            {
                return CreateProtocolError(-32602, "Invalid tool arguments");
            }

            // Execute tool with rate limiting and security checks
            var result = await ExecuteToolWithSecurity(toolName, arguments);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool execution failed: {ToolName}", toolName);
            return CreateToolExecutionError($"Tool execution failed: {ex.Message}");
        }
    }

    private JsonNode CreateProtocolError(int code, string message)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = message
            }
        };
    }

    private JsonNode CreateToolExecutionError(string message)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["result"] = new JsonObject
            {
                ["content"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "text",
                        ["text"] = message
                    }
                },
                ["isError"] = true
            }
        };
    }
}
```

## 🔒 Security Implementation

### **Input Validation Service**

```csharp
public class McpInputValidationService : IMcpInputValidationService
{
    public bool ValidateInput(JsonNode input, JsonNode schema)
    {
        // Implement JSON Schema validation
        // Check required fields
        // Validate data types
        // Sanitize input values
        return true;
    }

    public JsonNode SanitizeInput(JsonNode input)
    {
        // Remove potentially dangerous content
        // Escape special characters
        // Validate against known attack patterns
        return input;
    }
}
```

### **Rate Limiting Service**

```csharp
public class McpRateLimitingService : IMcpRateLimitingService
{
    private readonly Dictionary<string, DateTime> _lastCalls = new();
    private readonly TimeSpan _minInterval = TimeSpan.FromMilliseconds(100);

    public bool IsAllowed(string toolName, string clientId)
    {
        var key = $"{clientId}:{toolName}";
        var now = DateTime.UtcNow;

        if (_lastCalls.TryGetValue(key, out var lastCall))
        {
            if (now - lastCall < _minInterval)
            {
                return false;
            }
        }

        _lastCalls[key] = now;
        return true;
    }
}
```

## 🎯 Azure AI Foundry Integration

### **Configuration Support**

```json
{
  "mcpServers": {
    "accountingagent": {
      "server_label": "AccountingAgent MCP Server",
      "server_url": "http://localhost:3000/mcp",
      "require_approval": true,
      "headers": {
        "Authorization": "Bearer ${ACCOUNTING_AGENT_API_KEY}",
        "X-Client-Version": "1.0.0"
      }
    }
  }
}
```

### **Transport Support**

Support for multiple MCP transports:
- **SSE (Server-Sent Events)**: For web-based clients
- **STDIO**: For command-line integration
- **HTTP**: For REST API compatibility

## 📊 Quality Assurance

### **MCP Compliance Testing**

1. **Schema Validation**: Ensure all generated schemas are valid JSON Schema
2. **Protocol Compliance**: Test against official MCP test suite
3. **Security Testing**: Validate input sanitization and rate limiting
4. **Performance Testing**: Ensure acceptable response times
5. **Integration Testing**: Test with real MCP clients (Claude Desktop, VS Code, etc.)

### **Monitoring and Logging**

```csharp
public class McpAuditService : IMcpAuditService
{
    public async Task LogToolInvocation(string toolName, JsonNode input, JsonNode output, TimeSpan duration)
    {
        var auditEntry = new McpAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            ToolName = toolName,
            InputHash = ComputeHash(input),
            OutputHash = ComputeHash(output),
            Duration = duration,
            Success = !IsError(output)
        };

        await _auditRepository.SaveAsync(auditEntry);
    }
}
```

## 🚀 Implementation Priority

1. **Phase 5a**: Implement basic MCP capabilities generation
2. **Phase 5b**: Add input/output schema generation
3. **Phase 5c**: Implement security validation
4. **Phase 5d**: Add rate limiting and audit logging
5. **Phase 5e**: Test with real MCP clients
6. **Phase 5f**: Azure AI Foundry integration testing

This ensures our AccountingAgent becomes a fully compliant, production-ready MCP server that follows Microsoft's standards and integrates seamlessly with the broader MCP ecosystem.
