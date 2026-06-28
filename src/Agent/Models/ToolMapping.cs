namespace Agent.Models;

/// <summary>
/// Tool mapping for MCP hybrid mode
/// </summary>
public class ToolMapping
{
    public DateTime GeneratedAt { get; set; }
    public int TotalPlugins { get; set; }
    public int TotalTools { get; set; }
    public Dictionary<string, ToolMappingEntry> Tools { get; set; } = new();
}

/// <summary>
/// Individual tool mapping entry
/// </summary>
public class ToolMappingEntry
{
    public string ToolName { get; set; } = string.Empty;
    public string PluginName { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ToolParameter> Parameters { get; set; } = new();
}

/// <summary>
/// Tool parameter definition
/// </summary>
public class ToolParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Required { get; set; }
}
