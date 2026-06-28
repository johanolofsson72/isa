using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Syncfusion.Blazor;
using WebChat.Components;
using WebChat.Services;
using WebChat.Endpoints;
using Blazored.LocalStorage;

Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(
    "Ngo9BigBOggjHTQxAR8/V1NNaF5cXmBCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdmWXtedXRcRGJZWUx1VkFWYUA="
);

var builder = WebApplication.CreateBuilder(args);

// Add service defaults (includes Aspire Service Discovery)
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Add authorization (but no cookie authentication)
builder.Services.AddAuthorization();

// Add cascade authentication state provider
builder.Services.AddCascadingAuthenticationState();

// Add our custom AuthenticationStateProvider
builder.Services.AddScoped<AuthenticationStateProvider, SessionAuthenticationStateProvider>();

// Add Blazored LocalStorage
builder.Services.AddBlazoredLocalStorage();

// Add device identification services
builder.Services.AddScoped<Shared.Services.IDeviceFingerprintService, Shared.Services.DeviceFingerprintService>();
builder.Services.AddHttpContextAccessor();

// Add Syncfusion services
builder.Services.AddSyncfusionBlazor();

// Add configuration to make it accessible in components
builder.Services.AddSingleton(builder.Configuration);

// Register SmartAgent services
builder.Services.AddScoped<WebChat.Services.WorkflowService>();
builder.Services.AddScoped<WebChat.Services.QuartzWorkflowService>();
builder.Services.AddScoped<WebChat.Services.HistoryService>();
builder.Services.AddScoped<WebChat.Services.ChatService>();
builder.Services.AddScoped<WebChat.Services.ThemeService>();
builder.Services.AddScoped<WebChat.Services.PluginManagementService>();
builder.Services.AddScoped<WebChat.Services.StatsService>();
builder.Services.AddScoped<WebChat.Services.FortnoxService>();
builder.Services.AddScoped<WebChat.Services.Interfaces.IMemoryQueryService, WebChat.Services.MemoryQueryService>();

// WebChat should use Agent's file handling via HTTP API calls, not duplicate database functionality

// Add SessionFileService (now using Agent HTTP API)
builder.Services.AddScoped<WebChat.Services.Interfaces.ISessionFileService, WebChat.Services.SessionFileService>();

// Add HTTP client with service discovery for Agent API
builder
    .Services.AddHttpClient(
        "agent",
        (serviceProvider, client) =>
        {
            // Get the connection string from Aspire service discovery
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var agentBaseUrl = configuration.GetConnectionString("agent");

            // Fallback to a default URL if service discovery fails
            if (string.IsNullOrEmpty(agentBaseUrl))
            {
                // Use a development fallback URL
                agentBaseUrl = builder.Environment.IsDevelopment()
                    ? "https://localhost:7042" // Correct Agent API URL
                    : "http://isa_agent:80"; // Service name in container environments

                // Log a warning
                var logger = serviceProvider.GetService<ILogger<Program>>();
                logger?.LogWarning(
                    "🔗 [WebChat] Agent service URL not found in configuration. Using fallback URL: {Url}",
                    agentBaseUrl
                );
            }
            else
            {
                var logger = serviceProvider.GetService<ILogger<Program>>();
                logger?.LogInformation(
                    "🔗 [WebChat] Using Agent service URL from configuration: {Url}",
                    agentBaseUrl
                );
            }

            client.BaseAddress = new Uri(agentBaseUrl);

            // Set longer timeout for workflow execution (5 minutes)
            client.Timeout = TimeSpan.FromMinutes(5);
        }
    )
    // 🚀 FÖRBÄTTRING: Hantera SSL-certifikat för development
    .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
    {
        var handler = new HttpClientHandler();

        // Endast för development - acceptera alla SSL-certifikat
        if (builder.Environment.IsDevelopment())
        {
            handler.ServerCertificateCustomValidationCallback = (
                sender,
                certificate,
                chain,
                sslPolicyErrors
            ) => true;

            var logger = serviceProvider.GetService<ILogger<Program>>();
            logger?.LogWarning("🔓 [WebChat] SSL certificate validation disabled for development");
        }

        return handler;
    });

// Add HttpClient as a default client
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("agent"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

// Map file endpoints
app.MapFileEndpoints();
app.MapSessionFileEndpoints();

// Add health endpoint
app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Service = "WebChat",
    Timestamp = DateTime.UtcNow
}));

// Debug endpoints to check service discovery
app.MapGet(
    "/debug/agent-url",
    (IConfiguration config) =>
    {
        var agentConnectionString = config.GetConnectionString("agent");
        return new
        {
            AgentConnectionString = agentConnectionString,
            AllConnectionStrings = config
                .GetSection("ConnectionStrings")
                .GetChildren()
                .ToDictionary(x => x.Key, x => x.Value),
        };
    }
);

// Debug endpoint to check API connectivity
app.MapGet(
    "/debug/check-api",
    async (IHttpClientFactory httpClientFactory) =>
    {
        try
        {
            var httpClient = httpClientFactory.CreateClient("agent");
            var response = await httpClient.GetAsync("api/workflows");
            var content = await response.Content.ReadAsStringAsync();
            var isSuccess = response.IsSuccessStatusCode;

            return Results.Ok(
                new
                {
                    BaseAddress = httpClient.BaseAddress?.ToString(),
                    StatusCode = (int)response.StatusCode,
                    IsSuccess = isSuccess,
                    Content = isSuccess ? content : null,
                    Error = !isSuccess ? content : null,
                }
            );
        }
        catch (Exception ex)
        {
            return Results.Ok(
                new
                {
                    Error = ex.Message,
                    InnerError = ex.InnerException?.Message,
                    StackTrace = ex.StackTrace,
                }
            );
        }
    }
);

// Debug endpoint to test StatsService
app.MapGet(
    "/debug/stats",
    async (WebChat.Services.StatsService statsService) =>
    {
        try
        {
            var systemPromptStats = await statsService.GetSystemPromptStatsAsync();
            var usersJsonSize = await statsService.GetUsersJsonSizeBytesAsync();
            var pluginDescSize = await statsService.GetPluginDescriptionsSizeAsync();

            return Results.Ok(
                new
                {
                    SystemPromptStats = new
                    {
                        SystemPromptChars = systemPromptStats.systemPromptChars,
                        PluginCount = systemPromptStats.pluginCount,
                        FunctionCount = systemPromptStats.functionCount
                    },
                    UsersJsonSizeBytes = usersJsonSize,
                    PluginDescriptions = new
                    {
                        DescriptionChars = pluginDescSize.descChars,
                        DescriptionBytes = pluginDescSize.descBytes
                    },
                    Timestamp = DateTime.UtcNow
                }
            );
        }
        catch (Exception ex)
        {
            return Results.Ok(
                new
                {
                    Error = ex.Message,
                    InnerError = ex.InnerException?.Message,
                    StackTrace = ex.StackTrace,
                }
            );
        }
    }
);

app.MapGet("/fortnox-oauth-callback", async (HttpContext context, ILogger<Program> logger) =>
{
    logger.LogInformation("=== FORTNOX OAUTH CALLBACK RECEIVED ===");
    logger.LogInformation("Request URL: {Url}", context.Request.GetDisplayUrl());
    logger.LogInformation("Request Method: {Method}", context.Request.Method);
    logger.LogInformation("User Agent: {UserAgent}", context.Request.Headers.UserAgent.ToString());
    logger.LogInformation("Remote IP: {RemoteIP}", context.Connection.RemoteIpAddress?.ToString());

    var code = context.Request.Query["code"].ToString();
    var state = context.Request.Query["state"].ToString();
    var error = context.Request.Query["error"].ToString();
    var errorDescription = context.Request.Query["error_description"].ToString();

    logger.LogInformation("Query parameters - Code: {CodePresent}, State: {State}, Error: {Error}",
        !string.IsNullOrEmpty(code) ? "PRESENT" : "MISSING", state, error);

    if (!string.IsNullOrEmpty(code))
    {
        logger.LogInformation("Authorization code received (length: {Length}): {CodeStart}...",
            code.Length, code.Substring(0, Math.Min(10, code.Length)));
    }

    if (!string.IsNullOrEmpty(error))
    {
        logger.LogError("❌ OAuth error received: {Error}", error);
        if (!string.IsNullOrEmpty(errorDescription))
        {
            logger.LogError("Error description: {ErrorDescription}", errorDescription);
        }
        logger.LogInformation("=== FORTNOX OAUTH CALLBACK FAILED - ERROR ===");
        return Results.Redirect($"/login?error=oauth_failed&message={error}");
    }

    if (string.IsNullOrEmpty(code))
    {
        logger.LogError("❌ No authorization code received");
        logger.LogInformation("=== FORTNOX OAUTH CALLBACK FAILED - NO CODE ===");
        return Results.Redirect("/login?error=oauth_failed&message=missing_code");
    }

    try
    {
        logger.LogInformation("Getting FortnoxService from DI container...");
        FortnoxService fortnoxService = context.RequestServices.GetRequiredService<FortnoxService>();
        logger.LogInformation("✅ FortnoxService retrieved successfully");

        logger.LogInformation("Calling FetchAccessTokenFromFortnoxAsync...");
        var result = await fortnoxService.FetchAccessTokenFromFortnoxAsync(code);
        logger.LogInformation("FetchAccessTokenFromFortnoxAsync completed with result: {Result}",
            result?.Substring(0, Math.Min(50, result?.Length ?? 0)) + "...");

        if (result!.StartsWith("ERROR:"))
        {
            logger.LogError("❌ Token exchange failed: {Result}", result);
            logger.LogInformation("=== FORTNOX OAUTH CALLBACK FAILED - TOKEN EXCHANGE ===");
            return Results.Redirect($"/login?error=oauth_failed&message=token_exchange_failed");
        }

        logger.LogInformation("✅ Token exchange successful, redirecting to login page");
        logger.LogInformation("=== FORTNOX OAUTH CALLBACK SUCCESS ===");
        return Results.Redirect("/login?oauth=fortnox&success=true");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Exception in OAuth callback");
        logger.LogError("Exception type: {ExceptionType}", ex.GetType().Name);
        logger.LogError("Exception message: {Message}", ex.Message);
        logger.LogInformation("=== FORTNOX OAUTH CALLBACK FAILED - EXCEPTION ===");
        return Results.Redirect("/login?error=oauth_failed&message=internal_error");
    }
});

app.Run();

// Make Program class accessible for testing
public partial class Program { }
