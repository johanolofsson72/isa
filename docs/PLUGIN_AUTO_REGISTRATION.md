# Plugin Auto-Registration System & Hybrid MCP Architecture

This document describes the plugin auto-registration system and the new hybrid MCP architecture that provides both individual tools and intelligent agent functionality.

## Overview

The system automatically registers all available plugins at runtime and exposes them through a hybrid MCP architecture:

1. **Auto-Registration**: Plugins are automatically discovered and registered with Semantic Kernel
2. **Hybrid MCP Exposure**: All plugins are exposed as both individual tools AND through an intelligent execute_task agent
3. **Maximum Compatibility**: Works with all MCP clients (Augment Code, VSCode, Claude Desktop, etc.)

## Hybrid Architecture Benefits

- ✅ **48 tools total** - 47 individual tools + execute_task agent
- ✅ **Natural language commands** - "Lista filer i min Dropbox"
- ✅ **Individual tool access** - dropbox_listfiles, weather_getcurrent, etc.
- ✅ **MCP server functionality** - Full MCP protocol compliance
- ✅ **Augment Code compatibility** - Works with all MCP clients
- ✅ **Intelligent agent** - Complex multi-step task execution
- ✅ **Fallback mechanism** - Tool mapping generation if static files missing

## How It Works

1. **Plugin Registration**: The `PluginDiscoveryService` discovers and registers all plugins with Semantic Kernel
2. **Hybrid Tool Generation**: The `McpService` exposes plugins in two ways:
   - **Individual tools**: Direct access to each plugin function
   - **execute_task**: Intelligent agent that can use all plugins
3. **Dynamic Tool Mapping**: Generates tool mappings from Kernel plugins if static files are missing
4. **MCP Protocol**: Full compliance with Microsoft MCP standards

## Creating a New Plugin for Hybrid Architecture

To create a new plugin that works with the hybrid MCP architecture:

### Step 1: Create the Plugin Class

Create your plugin class as usual with methods decorated with `[KernelFunction]`:

```csharp
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MyPlugin;

public class Plugin
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<Plugin> _logger;

    public Plugin(IConfiguration configuration, ILogger<Plugin> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        // Initialize your plugin here
    }

    [KernelFunction]
    [Description("Description of what this function does")]
    public async Task<string> MyFunction(string parameter)
    {
        // Your plugin logic here
        return "Result";
    }
}
```

### Step 2: Create the Plugin Factory

Create a factory class that implements `IKernelPluginFactory`:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared;

namespace MyPlugin;

public class MyPluginFactory : IKernelPluginFactory
{
    public string PluginName => "MyPlugin";
    
    public string? Description => "Description of what your plugin does";

    public object CreatePlugin(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        return new Plugin(configuration, loggerFactory.CreateLogger<Plugin>());
    }
}
```

### Step 3: Add Shared Project Reference

Add a reference to the Shared project in your plugin's `.csproj` file:

```xml
<ItemGroup>
  <ProjectReference Include="../Shared/Shared.csproj" />
</ItemGroup>
```

### Step 4: Add Plugin to Agent

Add your plugin as a project reference in the Agent's `.csproj` file:

```xml
<ItemGroup>
  <ProjectReference Include="../MyPlugin/MyPlugin.csproj" />
</ItemGroup>
```

### Step 5: Register Plugin in PluginDiscoveryService

Add your plugin factory to the `GetPluginFactories()` method in `src/Shared/PluginDiscoveryService.cs`:

```csharp
try
{
    pluginFactories.Add(new MyPlugin.MyPluginFactory());
    _logger.LogDebug("Added MyPluginFactory");
}

### Step 6: Automatic Hybrid Exposure

Once your plugin is registered, it will automatically be exposed in the hybrid MCP architecture:

**Individual Tools:**
- `myplugin_myfunction` - Direct access to your plugin function
- Available in Augment Code tool list
- Can be called directly via MCP protocol

**Execute Task Integration:**
- Your plugin functions are automatically available to the execute_task agent
- Users can use natural language: "Use MyPlugin to do something"
- ISA will intelligently select and use your plugin when appropriate

**MCP Server:**
- Your plugin appears in tools/list endpoint
- Full MCP protocol compliance
- Works with all MCP clients
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to create MyPluginFactory");
}
```

**That's it!** Your plugin will be registered when the Agent starts.

## Configuration

If your plugin requires configuration, make sure to:

1. Use standard configuration patterns with `IConfiguration`
2. Provide meaningful error messages if required configuration is missing
3. The plugin factory should handle initialization failures gracefully

Example configuration handling:

```csharp
public Plugin(IConfiguration configuration, ILogger<Plugin> logger)
{
    _configuration = configuration;
    _logger = logger;
    
    var apiKey = configuration["MyPlugin:ApiKey"];
    if (string.IsNullOrEmpty(apiKey))
        throw new ArgumentNullException("MyPlugin:ApiKey configuration is missing");
}
```

## Testing

You can test your plugin factory by creating a unit test:

```csharp
[Fact]
public void PluginFactory_ShouldCreatePlugin()
{
    // Arrange
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string>
        {
            ["MyPlugin:ApiKey"] = "test-key"
        })
        .Build();
    
    var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    
    // Act
    var factory = new MyPluginFactory();
    var plugin = factory.CreatePlugin(configuration, loggerFactory);
    
    // Assert
    Assert.NotNull(plugin);
    Assert.Equal("MyPlugin", factory.PluginName);
}
```

## Migration from Old System

To migrate an existing plugin to the new system:

1. Create a plugin factory class as shown above
2. Add the Shared project reference
3. Remove the manual registration from `KernelExtensions.cs` (if it exists)

The plugin class itself doesn't need to change - only add the factory.

## Troubleshooting

### Plugin Not Discovered

- Ensure your factory class implements `IKernelPluginFactory`
- Verify the Shared project reference is added
- Check that the plugin project is referenced by the Agent

### Plugin Fails to Register

- Check the logs for detailed error messages
- Verify required configuration is provided
- Ensure the plugin constructor doesn't throw unhandled exceptions

### Plugin Works in Tests but Not in Agent

- Verify the plugin project is referenced by the Agent
- Check that all dependencies are properly configured
- Review the plugin discovery logs for clues

## Hybrid MCP Architecture

The hybrid system consists of several key components:

### Core Components

- `IKernelPluginFactory`: Interface that all plugin factories must implement
- `PluginDiscoveryService`: Service that discovers and registers plugins with Semantic Kernel
- `McpService`: Hybrid MCP server that exposes plugins in two ways
- `McpToolMappingGenerator`: Generates tool mappings from Kernel plugins
- `AgentService`: Intelligent agent that can use all plugins via execute_task

### Hybrid Tool Exposure

**Individual Tools (47 tools):**
```csharp
// Each plugin function becomes an individual MCP tool
dropbox_listfiles
weather_getcurrentweather
sms_sendsms
mail_sendemail
// ... all plugin functions
```

**Execute Task Agent (1 tool):**
```csharp
// Intelligent agent that can use all plugins
execute_task
// Accepts natural language descriptions
// Plans and executes complex multi-step tasks
// Uses appropriate plugins automatically
```

### MCP Protocol Implementation

The `McpService` implements the full MCP protocol:

- `initialize`: Establishes MCP connection
- `tools/list`: Returns all 48 tools (47 individual + execute_task)
- `tools/call`: Executes either individual tools or execute_task
- Dynamic tool mapping generation if static files are missing
- Full error handling and logging

This architecture provides maximum flexibility and compatibility with all MCP clients.