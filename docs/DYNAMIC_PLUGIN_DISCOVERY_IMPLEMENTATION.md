# Dynamic Plugin Discovery Implementation Plan

## 📋 Overview

This document outlines the complete implementation plan for transitioning from hardcoded plugin registration to a fully dynamic, attribute-based plugin discovery system in AccountingAgent.

## 🎯 Goals

- **Eliminate hardcoded plugin lists** in AgentPluginDiscoveryService
- **Automate system prompt generation** from plugin metadata
- **Automate MCP capabilities generation** from plugin metadata
- **Simplify plugin addition** to just adding an attribute
- **Maintain quality and control** through explicit marking

## 🏗️ Architecture Overview

### Current State
```
AgentPluginDiscoveryService
├── Hardcoded plugin factory types (11 plugins)
├── Manual systemprompt.txt updates (400+ lines)
└── Manual plugin.capabilities files per plugin

Problems:
- 3 places to update for each new plugin
- Manual maintenance overhead
- Risk of inconsistencies
```

### Target State
```
DynamicPluginDiscoveryService
├── Assembly scanning for [PluginDiscoverable] attributes
├── Automatic metadata extraction from KernelFunction attributes
├── Dynamic system prompt generation
└── Dynamic MCP capabilities generation

Benefits:
- Single attribute to add new plugins
- Automatic documentation generation
- Consistent metadata across all systems
```

## 🔧 New Components to Develop

### 1. PluginDiscoverableAttribute
```csharp
[AttributeUsage(AttributeTargets.Class)]
public class PluginDiscoverableAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; }
    public string Category { get; set; } = "General";
    public int Priority { get; set; } = 100;
    public bool Enabled { get; set; } = true;
    
    public PluginDiscoverableAttribute(string name, string description)
    {
        Name = name;
        Description = description;
    }
}
```

### 2. PluginMetadata Model
```csharp
public class PluginMetadata
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }
    public int Priority { get; set; }
    public bool Enabled { get; set; }
    public List<FunctionMetadata> Functions { get; set; } = new();
    public Type FactoryType { get; set; }
    public IKernelPluginFactory Factory { get; set; }
}

public class FunctionMetadata
{
    public string Name { get; set; }
    public string Description { get; set; }
    public List<ParameterMetadata> Parameters { get; set; } = new();
    public Type ReturnType { get; set; }
}

public class ParameterMetadata
{
    public string Name { get; set; }
    public string Description { get; set; }
    public Type Type { get; set; }
    public bool IsRequired { get; set; }
    public object? DefaultValue { get; set; }
}
```

### 3. DynamicPluginDiscoveryService
```csharp
public class DynamicPluginDiscoveryService : PluginDiscoveryService
{
    private readonly IPluginMetadataExtractor _metadataExtractor;
    private readonly ILogger<DynamicPluginDiscoveryService> _logger;
    private List<PluginMetadata>? _cachedPlugins;

    public override int RegisterAllPlugins(Kernel kernel)
    {
        var plugins = GetDiscoverablePlugins();
        var registeredCount = 0;

        foreach (var plugin in plugins.Where(p => p.Enabled).OrderBy(p => p.Priority))
        {
            try
            {
                var instance = plugin.Factory.CreatePlugin(_configuration, _loggerFactory);
                kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(instance, plugin.Name));
                registeredCount++;
                _logger.LogInformation("Registered plugin: {PluginName}", plugin.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register plugin: {PluginName}", plugin.Name);
            }
        }

        return registeredCount;
    }

    private List<PluginMetadata> GetDiscoverablePlugins()
    {
        if (_cachedPlugins != null) return _cachedPlugins;

        var plugins = new List<PluginMetadata>();
        var assemblies = GetReferencedAssemblies();

        foreach (var assembly in assemblies)
        {
            var pluginTypes = assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<PluginDiscoverableAttribute>() != null)
                .Where(t => typeof(IKernelPluginFactory).IsAssignableFrom(t));

            foreach (var type in pluginTypes)
            {
                var metadata = _metadataExtractor.ExtractMetadata(type);
                plugins.Add(metadata);
            }
        }

        _cachedPlugins = plugins;
        return plugins;
    }
}
```

### 4. PluginMetadataExtractor
```csharp
public interface IPluginMetadataExtractor
{
    PluginMetadata ExtractMetadata(Type factoryType);
    List<FunctionMetadata> ExtractFunctionMetadata(Type pluginType);
}

public class PluginMetadataExtractor : IPluginMetadataExtractor
{
    public PluginMetadata ExtractMetadata(Type factoryType)
    {
        var attribute = factoryType.GetCustomAttribute<PluginDiscoverableAttribute>();
        var factory = (IKernelPluginFactory)Activator.CreateInstance(factoryType);
        
        // Get plugin type by creating a temporary instance
        var tempInstance = factory.CreatePlugin(new ConfigurationBuilder().Build(), 
            new NullLoggerFactory());
        var pluginType = tempInstance.GetType();

        return new PluginMetadata
        {
            Name = attribute.Name,
            Description = attribute.Description,
            Category = attribute.Category,
            Priority = attribute.Priority,
            Enabled = attribute.Enabled,
            FactoryType = factoryType,
            Factory = factory,
            Functions = ExtractFunctionMetadata(pluginType)
        };
    }

    public List<FunctionMetadata> ExtractFunctionMetadata(Type pluginType)
    {
        var functions = new List<FunctionMetadata>();
        var methods = pluginType.GetMethods()
            .Where(m => m.GetCustomAttribute<KernelFunctionAttribute>() != null);

        foreach (var method in methods)
        {
            var kernelFunc = method.GetCustomAttribute<KernelFunctionAttribute>();
            var description = method.GetCustomAttribute<DescriptionAttribute>();

            var functionMeta = new FunctionMetadata
            {
                Name = kernelFunc?.Name ?? method.Name,
                Description = description?.Description ?? kernelFunc?.Description ?? "",
                ReturnType = method.ReturnType,
                Parameters = ExtractParameterMetadata(method)
            };

            functions.Add(functionMeta);
        }

        return functions;
    }

    private List<ParameterMetadata> ExtractParameterMetadata(MethodInfo method)
    {
        var parameters = new List<ParameterMetadata>();

        foreach (var param in method.GetParameters())
        {
            var description = param.GetCustomAttribute<DescriptionAttribute>();
            
            parameters.Add(new ParameterMetadata
            {
                Name = param.Name ?? "",
                Description = description?.Description ?? "",
                Type = param.ParameterType,
                IsRequired = !param.HasDefaultValue,
                DefaultValue = param.HasDefaultValue ? param.DefaultValue : null
            });
        }

        return parameters;
    }
}
```

### 5. SystemPromptBuilder
```csharp
public interface ISystemPromptBuilder
{
    Task<string> BuildSystemPromptAsync();
    Task<string> BuildSystemPromptAsync(List<PluginMetadata> plugins);
}

public class SystemPromptBuilder : ISystemPromptBuilder
{
    private readonly IConfiguration _configuration;
    private readonly IPluginMetadataExtractor _metadataExtractor;
    private readonly ILogger<SystemPromptBuilder> _logger;

    public async Task<string> BuildSystemPromptAsync()
    {
        var discoveryService = new DynamicPluginDiscoveryService(_configuration, _loggerFactory);
        var plugins = discoveryService.GetDiscoverablePlugins();
        return await BuildSystemPromptAsync(plugins);
    }

    public async Task<string> BuildSystemPromptAsync(List<PluginMetadata> plugins)
    {
        var basePrompt = await LoadBaseSystemPromptAsync();
        var pluginDescriptions = GeneratePluginDescriptions(plugins);
        
        return $"{basePrompt}\n\n{pluginDescriptions}";
    }

    private async Task<string> LoadBaseSystemPromptAsync()
    {
        var promptPath = _configuration.GetValue<string>("Azure:OpenAI:BaseSystemPromptFile")
            ?? "systemprompt_base.txt";
            
        if (File.Exists(promptPath))
        {
            return await File.ReadAllTextAsync(promptPath);
        }
        
        _logger.LogWarning("Base system prompt file not found: {Path}", promptPath);
        return "You are a helpful AI assistant.";
    }

    private string GeneratePluginDescriptions(List<PluginMetadata> plugins)
    {
        var sb = new StringBuilder();
        sb.AppendLine("🔧 TILLGÄNGLIGA PLUGINS OCH FUNKTIONER:");
        sb.AppendLine();

        var groupedPlugins = plugins
            .Where(p => p.Enabled)
            .GroupBy(p => p.Category)
            .OrderBy(g => g.Key);

        foreach (var group in groupedPlugins)
        {
            sb.AppendLine($"## {group.Key}");
            sb.AppendLine();

            foreach (var plugin in group.OrderBy(p => p.Priority))
            {
                sb.AppendLine($"### {plugin.Name}");
                sb.AppendLine($"{plugin.Description}");
                sb.AppendLine();

                if (plugin.Functions.Any())
                {
                    sb.AppendLine("**Funktioner:**");
                    foreach (var func in plugin.Functions)
                    {
                        sb.AppendLine($"- `{func.Name}`: {func.Description}");
                        
                        if (func.Parameters.Any())
                        {
                            var paramList = string.Join(", ", func.Parameters.Select(p => 
                                $"{p.Name}{(p.IsRequired ? "*" : "")}"));
                            sb.AppendLine($"  - Parametrar: {paramList}");
                        }
                    }
                    sb.AppendLine();
                }
            }
        }

        return sb.ToString();
    }
}
```

## 📋 Migration Phases

### Phase 1: Hybrid Discovery (Safety First)
**Goal**: Add dynamic discovery alongside existing hardcoded system
**Duration**: 1-2 days
**Risk**: Low

**Tasks**:
1. Create PluginDiscoverableAttribute
2. Create basic DynamicPluginDiscoveryService
3. Add attributes to existing plugin factories
4. Compare results with hardcoded list
5. Log any discrepancies

**Success Criteria**:
- All existing plugins found via dynamic discovery
- No regressions in plugin loading
- Comprehensive logging of discovery process

### Phase 2: Replace Hardcoded Discovery
**Goal**: Switch to dynamic discovery as primary method
**Duration**: 1 day
**Risk**: Medium

**Tasks**:
1. Replace AgentPluginDiscoveryService with DynamicPluginDiscoveryService
2. Remove hardcoded plugin list
3. Keep fallback mechanism for safety
4. Update unit tests

**Success Criteria**:
- All plugins load correctly via dynamic discovery
- No hardcoded plugin references remain
- Tests pass

### Phase 3: Metadata Extraction
**Goal**: Extract rich metadata from plugin code
**Duration**: 2-3 days
**Risk**: Medium

**Tasks**:
1. Implement PluginMetadataExtractor
2. Extract KernelFunction attributes and descriptions
3. Build comprehensive plugin metadata
4. Compare with manual descriptions for quality

**Success Criteria**:
- Complete metadata extraction for all plugins
- Quality comparable to manual descriptions
- Performance acceptable (< 100ms startup overhead)

### Phase 4: Dynamic System Prompt
**Goal**: Generate system prompt from plugin metadata
**Duration**: 2-3 days
**Risk**: Medium-High

**Tasks**:
1. Split systemprompt.txt into base + plugin sections
2. Implement SystemPromptBuilder
3. Generate plugin descriptions from metadata
4. Update ChatHistoryFactory to use dynamic prompts
5. A/B test prompt quality

**Success Criteria**:
- Dynamic prompts maintain AI assistant quality
- Plugin descriptions are accurate and helpful
- No degradation in AI responses

### Phase 5: Dynamic MCP Capabilities
**Goal**: Generate MCP capabilities automatically
**Duration**: 1-2 days
**Risk**: Low-Medium

**Tasks**:
1. Implement DynamicMcpCapabilitiesGenerator
2. Convert plugin metadata to MCP format
3. Update McpService to use dynamic capabilities
4. Remove manual plugin.capabilities files

**Success Criteria**:
- MCP clients receive correct capabilities
- All plugin functions accessible via MCP
- No manual capabilities files needed

## 🔄 Rollback Strategy

Each phase includes rollback mechanisms:

1. **Phase 1**: Keep hardcoded list active, just add logging
2. **Phase 2**: Feature flag to switch back to hardcoded
3. **Phase 3**: Fallback to basic plugin info if metadata extraction fails
4. **Phase 4**: Keep static systemprompt.txt as backup
5. **Phase 5**: Keep manual capabilities files until confirmed working

## 📊 Success Metrics

- **Plugin Discovery**: 100% of plugins found automatically
- **Startup Performance**: < 200ms additional overhead
- **System Prompt Quality**: No degradation in AI responses
- **MCP Compatibility**: All existing MCP clients continue working
- **Developer Experience**: New plugin addition takes < 5 minutes

## 🚨 Risk Mitigation

1. **Assembly Loading Issues**: Comprehensive error handling and logging
2. **Metadata Quality**: Hybrid approach allowing manual overrides
3. **Performance Impact**: Caching and lazy loading
4. **Breaking Changes**: Extensive testing and gradual rollout
5. **Debugging Difficulty**: Rich logging and diagnostic tools

## 📝 Testing Strategy

1. **Unit Tests**: Each component thoroughly tested
2. **Integration Tests**: Full plugin discovery pipeline
3. **Performance Tests**: Startup time and memory usage
4. **Quality Tests**: Compare generated vs manual descriptions
5. **Regression Tests**: Ensure existing functionality preserved

## 🎯 Next Steps

1. Review and approve this implementation plan
2. Create detailed task list for Phase 1
3. Set up development branch for dynamic discovery work
4. Begin implementation of PluginDiscoverableAttribute
5. Research Microsoft MCP standards for integration
