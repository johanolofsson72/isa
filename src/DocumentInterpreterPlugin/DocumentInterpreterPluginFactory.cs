using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Attributes;

namespace DocumentInterpreter;

/// <summary>
/// Factory for creating DocumentInterpreter plugin instances
/// </summary>
[PluginDiscoverable("DocumentInterpreter", "AI-powered interpretation of financial documents like invoices, receipts, and other accounting documents",
    Category = "Document Processing", Priority = 20,
    Tags = new[] { "AI", "OCR", "Financial", "Invoices", "Receipts", "Accounting", "tolka", "dokument", "undersök", "kolla", "analysera", "PDF", "fil", "document", "interpret", "parse" })]
public class DocumentInterpreterPluginFactory : IKernelPluginFactory
{
    public string PluginName => "DocumentInterpreter";
    
    public string? Description => "Plugin for AI-powered interpretation of financial documents including invoices, receipts, credit notes, bank statements, and other accounting documents. Supports multiple input sources and provides structured, vendor-neutral output with quality metrics.";

    public object CreatePlugin(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        return new Plugin(configuration, loggerFactory.CreateLogger<Plugin>());
    }
}