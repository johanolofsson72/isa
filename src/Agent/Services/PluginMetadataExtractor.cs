using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Shared;
using Shared.Attributes;
using Shared.Models;

namespace Agent.Services;

/// <summary>
/// Interface for extracting metadata from plugin types and instances.
/// </summary>
public interface IPluginMetadataExtractor
{
    /// <summary>
    /// Extracts complete metadata from a plugin factory type.
    /// </summary>
    PluginMetadata ExtractMetadata(Type factoryType);
    
    /// <summary>
    /// Extracts function metadata from a plugin instance.
    /// </summary>
    List<FunctionMetadata> ExtractFunctionMetadata(object pluginInstance);
}

/// <summary>
/// Service for extracting comprehensive metadata from plugins using reflection.
/// </summary>
public class PluginMetadataExtractor : IPluginMetadataExtractor
{
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PluginMetadataExtractor> _logger;

    public PluginMetadataExtractor(
        IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<PluginMetadataExtractor>();
    }

    /// <summary>
    /// Extracts complete metadata from a plugin factory type.
    /// </summary>
    public PluginMetadata ExtractMetadata(Type factoryType)
    {
        var attribute = factoryType.GetCustomAttribute<PluginDiscoverableAttribute>();
        if (attribute == null)
        {
            throw new ArgumentException($"Type {factoryType.Name} does not have [PluginDiscoverable] attribute", nameof(factoryType));
        }

        _logger.LogDebug("Extracting metadata for plugin factory: {FactoryType}", factoryType.Name);

        // Create factory instance
        IKernelPluginFactory? factory = null;
        object? pluginInstance = null;
        List<FunctionMetadata> functions = new();

        try
        {
            factory = (IKernelPluginFactory)Activator.CreateInstance(factoryType)!;
            
            // Create plugin instance to extract function metadata
            pluginInstance = factory.CreatePlugin(_configuration, _loggerFactory);
            functions = ExtractFunctionMetadata(pluginInstance);
            
            _logger.LogDebug("Extracted {FunctionCount} functions from plugin {PluginName}", 
                functions.Count, attribute.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create plugin instance for metadata extraction: {FactoryType}", factoryType.Name);
        }

        var metadata = new PluginMetadata
        {
            Name = attribute.Name,
            Description = attribute.Description,
            Category = attribute.Category,
            Priority = attribute.Priority,
            Enabled = attribute.Enabled,
            Version = attribute.Version,
            Tags = attribute.Tags,
            FactoryType = factoryType,
            Factory = factory,
            AssemblyName = factoryType.Assembly.GetName().Name ?? "Unknown",
            Functions = functions,
            ExtractedAt = DateTime.UtcNow
        };

        return metadata;
    }

    /// <summary>
    /// Extracts function metadata from a plugin instance.
    /// </summary>
    public List<FunctionMetadata> ExtractFunctionMetadata(object pluginInstance)
    {
        if (pluginInstance == null)
        {
            return new List<FunctionMetadata>();
        }

        var functions = new List<FunctionMetadata>();
        var pluginType = pluginInstance.GetType();

        _logger.LogDebug("Extracting function metadata from plugin type: {PluginType}", pluginType.Name);

        // Find all methods with KernelFunction attribute
        var methods = pluginType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<KernelFunctionAttribute>() != null);

        foreach (var method in methods)
        {
            try
            {
                var functionMetadata = ExtractMethodMetadata(method);
                functions.Add(functionMetadata);
                
                _logger.LogDebug("Extracted function metadata: {FunctionName} with {ParameterCount} parameters", 
                    functionMetadata.Name, functionMetadata.Parameters.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract metadata for method {MethodName} in {PluginType}", 
                    method.Name, pluginType.Name);
            }
        }

        return functions;
    }

    /// <summary>
    /// Extracts metadata from a single method.
    /// </summary>
    private FunctionMetadata ExtractMethodMetadata(MethodInfo method)
    {
        var kernelFunc = method.GetCustomAttribute<KernelFunctionAttribute>();
        var description = method.GetCustomAttribute<DescriptionAttribute>();

        var functionMetadata = new FunctionMetadata
        {
            Name = kernelFunc?.Name ?? method.Name,
            Description = description?.Description ?? "",
            ReturnType = method.ReturnType,
            IsAsync = IsAsyncMethod(method),
            Parameters = ExtractParameterMetadata(method)
        };

        // Extract additional metadata from attributes
        ExtractAdditionalMetadata(method, functionMetadata);

        return functionMetadata;
    }

    /// <summary>
    /// Extracts parameter metadata from a method.
    /// </summary>
    private List<ParameterMetadata> ExtractParameterMetadata(MethodInfo method)
    {
        var parameters = new List<ParameterMetadata>();

        foreach (var param in method.GetParameters())
        {
            // Skip special parameters like CancellationToken
            if (IsSpecialParameter(param))
            {
                continue;
            }

            var description = param.GetCustomAttribute<DescriptionAttribute>();
            
            var paramMetadata = new ParameterMetadata
            {
                Name = param.Name ?? "",
                Description = description?.Description ?? "",
                Type = param.ParameterType,
                IsRequired = !param.HasDefaultValue && !IsNullableType(param.ParameterType),
                DefaultValue = param.HasDefaultValue ? param.DefaultValue : null
            };

            // Extract constraints from attributes
            ExtractParameterConstraints(param, paramMetadata);

            parameters.Add(paramMetadata);
        }

        return parameters;
    }

    /// <summary>
    /// Checks if a method is asynchronous.
    /// </summary>
    private static bool IsAsyncMethod(MethodInfo method)
    {
        return method.ReturnType.IsGenericType && 
               (method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>) ||
                method.ReturnType == typeof(Task));
    }

    /// <summary>
    /// Checks if a parameter should be skipped (e.g., CancellationToken).
    /// </summary>
    private static bool IsSpecialParameter(ParameterInfo param)
    {
        return param.ParameterType == typeof(System.Threading.CancellationToken) ||
               param.ParameterType == typeof(IServiceProvider) ||
               param.ParameterType == typeof(ILogger) ||
               param.ParameterType.IsAssignableFrom(typeof(ILogger));
    }

    /// <summary>
    /// Checks if a type is nullable.
    /// </summary>
    private static bool IsNullableType(Type type)
    {
        return !type.IsValueType || 
               (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));
    }

    /// <summary>
    /// Extracts additional metadata from method attributes.
    /// </summary>
    private void ExtractAdditionalMetadata(MethodInfo method, FunctionMetadata functionMetadata)
    {
        // Add any additional attributes as metadata
        var attributes = method.GetCustomAttributes().Where(a => 
            a.GetType() != typeof(KernelFunctionAttribute) && 
            a.GetType() != typeof(DescriptionAttribute));

        foreach (var attr in attributes)
        {
            var attrType = attr.GetType();
            functionMetadata.AdditionalMetadata[attrType.Name] = attr;
        }
    }

    /// <summary>
    /// Extracts parameter constraints from attributes.
    /// </summary>
    private void ExtractParameterConstraints(ParameterInfo param, ParameterMetadata paramMetadata)
    {
        // Extract validation attributes and other constraints
        var attributes = param.GetCustomAttributes();

        foreach (var attr in attributes.Where(a => a.GetType() != typeof(DescriptionAttribute)))
        {
            var attrType = attr.GetType();
            paramMetadata.Constraints[attrType.Name] = attr;
        }

        // Add type-specific constraints
        if (param.ParameterType == typeof(string))
        {
            paramMetadata.Constraints["Type"] = "string";
        }
        else if (param.ParameterType.IsNumeric())
        {
            paramMetadata.Constraints["Type"] = "number";
        }
        else if (param.ParameterType == typeof(bool))
        {
            paramMetadata.Constraints["Type"] = "boolean";
        }
        else if (param.ParameterType.IsArray || param.ParameterType.IsGenericType)
        {
            paramMetadata.Constraints["Type"] = "array";
        }
        else
        {
            paramMetadata.Constraints["Type"] = "object";
        }
    }
}

/// <summary>
/// Extension methods for type checking.
/// </summary>
public static class TypeExtensions
{
    /// <summary>
    /// Checks if a type is numeric.
    /// </summary>
    public static bool IsNumeric(this Type type)
    {
        return type == typeof(int) || type == typeof(long) || type == typeof(short) ||
               type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) ||
               type == typeof(byte) || type == typeof(sbyte) ||
               type == typeof(float) || type == typeof(double) || type == typeof(decimal) ||
               type == typeof(int?) || type == typeof(long?) || type == typeof(short?) ||
               type == typeof(uint?) || type == typeof(ulong?) || type == typeof(ushort?) ||
               type == typeof(byte?) || type == typeof(sbyte?) ||
               type == typeof(float?) || type == typeof(double?) || type == typeof(decimal?);
    }
}
