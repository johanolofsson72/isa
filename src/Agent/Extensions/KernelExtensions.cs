using System.ClientModel;
using System.ClientModel.Primitives;
using System.Reflection;
using Agent.Configuration;
using Agent.Services;
using Agent.Services.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using OpenAI;

namespace Agent.Extensions;

public static class KernelExtensions
{
    public const string LiteLLMHttpClient = "litellm";

    public static Kernel CreateKernelWithPlugins(
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        IServiceProvider? serviceProvider = null
    )
    {
#pragma warning disable SKEXP0010
        var logger = loggerFactory.CreateLogger(typeof(KernelExtensions));

        var llmOptions = serviceProvider?.GetService<IOptions<LiteLLMOptions>>()?.Value
                         ?? configuration.GetSection(LiteLLMOptions.SectionName).Get<LiteLLMOptions>()
                         ?? new LiteLLMOptions();
        var cfOptions = serviceProvider?.GetService<IOptions<CloudflareAccessOptions>>()?.Value
                        ?? configuration.GetSection(CloudflareAccessOptions.SectionName).Get<CloudflareAccessOptions>()
                        ?? new CloudflareAccessOptions();

        if (string.IsNullOrWhiteSpace(llmOptions.Endpoint))
        {
            throw new InvalidOperationException("LiteLLM:Endpoint configuration is missing");
        }
        if (string.IsNullOrWhiteSpace(llmOptions.ApiKey))
        {
            throw new InvalidOperationException("LiteLLM:ApiKey configuration is missing");
        }

        var endpoint = new Uri(llmOptions.Endpoint);

        var http = serviceProvider?.GetService<IHttpClientFactory>()?.CreateClient(LiteLLMHttpClient);
        if (http is null)
        {
            logger.LogWarning("Could not resolve named HttpClient '{Name}' from factory; falling back to default HttpClient", LiteLLMHttpClient);
            http = new HttpClient { Timeout = TimeSpan.FromMinutes(llmOptions.TimeoutMinutes) };
        }

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = endpoint,
            Transport = new HttpClientPipelineTransport(http),
        };

        if (cfOptions.IsConfigured)
        {
            clientOptions.AddPolicy(
                new CloudflareAccessHeadersPolicy(cfOptions.ClientId, cfOptions.ClientSecret, llmOptions.UserAgent),
                PipelinePosition.PerCall);
            logger.LogInformation("LiteLLM client configured with Cloudflare Access headers");
        }
        else
        {
            logger.LogInformation("LiteLLM client configured without Cloudflare Access (local/dev endpoint)");
        }

        var openAIClient = new OpenAIClient(new ApiKeyCredential(llmOptions.ApiKey), clientOptions);

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(loggerFactory);
        kernelBuilder
            .AddOpenAIChatCompletion(modelId: llmOptions.ChatModel, openAIClient: openAIClient)
            .AddOpenAIEmbeddingGenerator(modelId: llmOptions.EmbedModel, openAIClient: openAIClient, dimensions: llmOptions.EmbeddingDimensions);

        var kernel = kernelBuilder.Build();

        kernel.RegisterPlugins(configuration, loggerFactory, serviceProvider);

        return kernel;
#pragma warning restore SKEXP0010
    }

    public static Kernel RegisterPlugins(
        this Kernel kernel,
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        IServiceProvider? serviceProvider = null
    )
    {
#pragma warning disable SKEXP0010
        var logger = loggerFactory.CreateLogger(typeof(KernelExtensions));
        int registeredCount;

        if (serviceProvider != null)
        {
            try
            {
                var dynamicService = serviceProvider.GetRequiredService<DynamicPluginDiscoveryService>();
                var pluginDiscovery = new DatabaseAwarePluginDiscoveryService(
                    configuration,
                    loggerFactory,
                    dynamicService,
                    serviceProvider);

                registeredCount = pluginDiscovery.RegisterAllPlugins(kernel);
                logger.LogInformation("Database-aware plugin registration completed. Registered {Count} plugins", registeredCount);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to use database-aware plugin discovery, falling back to dynamic discovery");
                var fallbackDiscovery = new DynamicPluginDiscoveryService(configuration, loggerFactory);
                registeredCount = fallbackDiscovery.RegisterAllPlugins(kernel);
                logger.LogInformation("Fallback dynamic plugin registration completed. Registered {Count} plugins", registeredCount);
            }
        }
        else
        {
            var pluginDiscovery = new DynamicPluginDiscoveryService(configuration, loggerFactory);
            registeredCount = pluginDiscovery.RegisterAllPlugins(kernel);
            logger.LogInformation("Dynamic plugin registration completed. Registered {Count} plugins", registeredCount);
        }

        if (serviceProvider != null)
        {
            var pluginsToSetup = new[]
            {
                ("DropboxPlugin", "Dropbox.Plugin"),
                ("DocumentInterpreterPlugin", "DocumentInterpreter.Plugin"),
                ("GoogleWorkspacePlugin", "GoogleWorkspace.Plugin"),
                ("MicrosoftOfficePlugin", "MicrosoftOffice.Plugin"),
                ("MailPlugin", "Mail.Plugin"),
                ("FortnoxPlugin", "Fortnox.Plugin"),
                ("AnalysisPlugin", "AnalysisPlugin.AnalysisPlugin")
            };

            foreach (var (assemblyName, typeName) in pluginsToSetup)
            {
                try
                {
                    var assembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == assemblyName);

                    if (assembly is null)
                    {
                        logger.LogWarning("Assembly {AssemblyName} not found", assemblyName);
                        continue;
                    }

                    var pluginType = assembly.GetType(typeName);
                    if (pluginType is null)
                    {
                        logger.LogWarning("Type {TypeName} not found in assembly {AssemblyName}", typeName, assemblyName);
                        continue;
                    }

                    var serviceProviderProperty = pluginType.GetProperty("ServiceProvider", BindingFlags.Public | BindingFlags.Static);
                    if (serviceProviderProperty is null)
                    {
                        logger.LogWarning("ServiceProvider property not found for {TypeName}", typeName);
                        continue;
                    }

                    serviceProviderProperty.SetValue(null, serviceProvider);
                    logger.LogInformation("ServiceProvider set for {PluginName} via reflection", assemblyName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to set ServiceProvider for {PluginName} via reflection", assemblyName);
                }
            }
        }

        return kernel;
#pragma warning restore SKEXP0010
    }
}
