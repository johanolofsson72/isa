using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace WebChat.Tests;

/// <summary>
/// E2E tests for device fingerprint + memory integration
/// Tests the complete flow: device fingerprint → user identification → memory storage/retrieval
/// </summary>
public class DeviceFingerprintMemoryE2ETests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public DeviceFingerprintMemoryE2ETests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _client = _factory.CreateClient();
    }

    [Fact(Skip = "E2E test - requires running AppHost and manual execution. Use Delivery.Tests for CI execution.")]
    [Trait("Category", "E2E")]
    [Trait("TestType", "Integration")]
    public async Task DeviceFingerprint_Should_PersistAcrossRequests_And_RememberMiddleName()
    {
        // Arrange
        var deviceFingerprint = Guid.NewGuid().ToString("D");
        _output.WriteLine($"🧪 TEST: Using device fingerprint: {deviceFingerprint}");

        // Step 1: First message - introduce middle name
        var firstMessage = new
        {
            Message = "Mitt mellannamn är Patrick",
            SessionId = Guid.NewGuid().ToString(),
            DeviceFingerprint = deviceFingerprint
        };

        _output.WriteLine("🧪 TEST: Step 1 - Sending first message with middle name");
        var firstResponse = await SendChatMessage(firstMessage);
        Assert.True(firstResponse.IsSuccessStatusCode, "First message should succeed");

        var firstContent = await firstResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"🧪 TEST: First response: {firstContent}");

        // Wait for memory processing
        await Task.Delay(2000);

        // Step 2: Second message - ask about middle name with SAME device fingerprint
        var secondMessage = new
        {
            Message = "Vad heter jag i mellannamn?",
            SessionId = Guid.NewGuid().ToString(), // Different session, same device
            DeviceFingerprint = deviceFingerprint // SAME device fingerprint
        };

        _output.WriteLine("🧪 TEST: Step 2 - Asking about middle name with same device fingerprint");
        var secondResponse = await SendChatMessage(secondMessage);
        Assert.True(secondResponse.IsSuccessStatusCode, "Second message should succeed");

        var secondContent = await secondResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"🧪 TEST: Second response: {secondContent}");

        // Step 3: Verify that the response contains "Patrick"
        Assert.Contains("Patrick", secondContent, StringComparison.OrdinalIgnoreCase);
        _output.WriteLine("✅ TEST: SUCCESS - Agent remembered middle name Patrick!");
    }

    [Fact(Skip = "E2E test - requires running AppHost and manual execution. Use Delivery.Tests for CI execution.")]
    [Trait("Category", "E2E")]
    [Trait("TestType", "Integration")]
    public async Task DeviceFingerprint_Should_CreateSeparateMemories_ForDifferentDevices()
    {
        // Arrange
        var device1 = Guid.NewGuid().ToString("D");
        var device2 = Guid.NewGuid().ToString("D");
        
        _output.WriteLine($"🧪 TEST: Device 1: {device1}");
        _output.WriteLine($"🧪 TEST: Device 2: {device2}");

        // Step 1: Device 1 introduces middle name
        var message1 = new
        {
            Message = "Mitt mellannamn är Patrick",
            SessionId = Guid.NewGuid().ToString(),
            DeviceFingerprint = device1
        };

        await SendChatMessage(message1);
        await Task.Delay(1000);

        // Step 2: Device 2 introduces different middle name
        var message2 = new
        {
            Message = "Mitt mellannamn är Erik",
            SessionId = Guid.NewGuid().ToString(),
            DeviceFingerprint = device2
        };

        await SendChatMessage(message2);
        await Task.Delay(1000);

        // Step 3: Device 1 asks about middle name - should get Patrick
        var query1 = new
        {
            Message = "Vad heter jag i mellannamn?",
            SessionId = Guid.NewGuid().ToString(),
            DeviceFingerprint = device1
        };

        var response1 = await SendChatMessage(query1);
        var content1 = await response1.Content.ReadAsStringAsync();
        
        _output.WriteLine($"🧪 TEST: Device 1 response: {content1}");
        Assert.Contains("Patrick", content1, StringComparison.OrdinalIgnoreCase);

        // Step 4: Device 2 asks about middle name - should get Erik
        var query2 = new
        {
            Message = "Vad heter jag i mellannamn?",
            SessionId = Guid.NewGuid().ToString(),
            DeviceFingerprint = device2
        };

        var response2 = await SendChatMessage(query2);
        var content2 = await response2.Content.ReadAsStringAsync();
        
        _output.WriteLine($"🧪 TEST: Device 2 response: {content2}");
        Assert.Contains("Erik", content2, StringComparison.OrdinalIgnoreCase);

        _output.WriteLine("✅ TEST: SUCCESS - Different devices have separate memories!");
    }

    private async Task<HttpResponseMessage> SendChatMessage(object message)
    {
        var json = JsonSerializer.Serialize(message);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        _output.WriteLine($"🧪 TEST: Sending: {json}");
        return await _client.PostAsync("/api/chat", content);
    }
}
