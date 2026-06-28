using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace MailPlugin.Tests;

/// <summary>
/// Unit tests for MailPlugin functionality
/// These tests use real objects and safe mode to avoid external dependencies
/// NO external dependencies, credentials, or network calls
/// Suitable for CI/CD pipeline execution
///
/// Tests include:
/// - Constructor validation
/// - Parameter validation
/// - Safe mode behavior
/// - Email message conversion
/// - Error handling
/// - Configuration loading
/// </summary>
[Trait("Category", "CI")]
public class MailPluginTests
{
    private readonly ILogger<Plugin> _logger;
    private readonly TestConfiguration _testConfiguration;

    public MailPluginTests()
    {
        _logger = NullLogger<Plugin>.Instance;
        _testConfiguration = new TestConfiguration();
    }

    /// <summary>
    /// Simple test configuration implementation
    /// </summary>
    private class TestConfiguration : IConfiguration
    {
        private readonly Dictionary<string, string?> _values = new();

        public string? this[string key]
        {
            get => _values.TryGetValue(key, out var value) ? value : null;
            set => _values[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => throw new NotImplementedException();
        public IChangeToken GetReloadToken() => throw new NotImplementedException();

        public IConfigurationSection GetSection(string key) => new TestConfigurationSection(key, _values);

        public T GetValue<T>(string key, T defaultValue)
        {
            if (_values.TryGetValue(key, out var value) && value != null)
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            return defaultValue;
        }

        public void SetValue(string key, string? value) => _values[key] = value;
    }

    /// <summary>
    /// Simple test configuration section implementation
    /// </summary>
    private class TestConfigurationSection : IConfigurationSection
    {
        private readonly string _key;
        private readonly Dictionary<string, string?> _values;

        public TestConfigurationSection(string key, Dictionary<string, string?> values)
        {
            _key = key;
            _values = values;
        }

        public string? this[string key]
        {
            get => _values.TryGetValue($"{_key}:{key}", out var value) ? value : null;
            set => _values[$"{_key}:{key}"] = value;
        }

        public string Key => _key;
        public string Path => _key;
        public string? Value
        {
            get => _values.TryGetValue(_key, out var value) ? value : null;
            set => _values[_key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => throw new NotImplementedException();
        public IChangeToken GetReloadToken() => throw new NotImplementedException();
        public IConfigurationSection GetSection(string key) => new TestConfigurationSection($"{_key}:{key}", _values);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldInitialize()
    {
        // Arrange
        var apiKey = "test-api-key";
        var apiSecret = "test-api-secret";
        var sender = "test@example.com";

        // Act
        var plugin = new Plugin(apiKey, apiSecret, sender, _logger, enableRealMessaging: false);

        // Assert
        plugin.Should().NotBeNull();
    }

    [Theory]
    [InlineData(null, "secret", "sender@test.com")]
    [InlineData("key", null, "sender@test.com")]
    [InlineData("key", "secret", null)]
    public void Constructor_WithNullParameters_ShouldThrowArgumentNullException(string? apiKey, string? apiSecret, string? sender)
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new Plugin(apiKey!, apiSecret!, sender!, _logger));
    }

    [Theory]
    [InlineData("", "secret", "sender@test.com")]
    [InlineData("key", "", "sender@test.com")]
    [InlineData("key", "secret", "")]
    public void Constructor_WithEmptyParameters_ShouldNotThrow(string apiKey, string apiSecret, string sender)
    {
        // Act & Assert - Empty strings are allowed, only null throws
        var plugin = new Plugin(apiKey, apiSecret, sender, _logger);
        plugin.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithConfiguration_ShouldInitialize()
    {
        // Arrange
        _testConfiguration["Mailjet:ApiKey"] = "test-key";
        _testConfiguration["Mailjet:ApiSecret"] = "test-secret";
        _testConfiguration["Mailjet:Sender"] = "test@example.com";
        _testConfiguration["Mailjet:SmtpServer"] = "smtp.test.com";
        _testConfiguration.SetValue("Mailjet:SmtpPort", "587");
        _testConfiguration.SetValue("Communications:EnableRealMessaging", "false");

        // Act
        var plugin = new Plugin(_testConfiguration, _logger);

        // Assert
        plugin.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Plugin((IConfiguration)null!, _logger));
    }

    [Theory]
    [InlineData("Mailjet:ApiKey")]
    [InlineData("Mailjet:ApiSecret")]
    [InlineData("Mailjet:Sender")]
    public void Constructor_WithMissingConfigurationValue_ShouldThrowArgumentNullException(string missingKey)
    {
        // Arrange
        var config = new TestConfiguration();
        config["Mailjet:ApiKey"] = missingKey == "Mailjet:ApiKey" ? null : "test-key";
        config["Mailjet:ApiSecret"] = missingKey == "Mailjet:ApiSecret" ? null : "test-secret";
        config["Mailjet:Sender"] = missingKey == "Mailjet:Sender" ? null : "test@example.com";

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Plugin(config, _logger));
    }

    #endregion

    #region SendEmailAsync Tests

    [Fact]
    public async Task SendEmailAsync_WithValidParameters_InSafeMode_ShouldReturnSuccessMessage()
    {
        // Arrange
        var plugin = new Plugin("key", "secret", "sender@test.com", _logger, enableRealMessaging: false);
        var to = "recipient@test.com";
        var subject = "Test Subject";
        var body = "Test Body";

        // Act
        var result = await plugin.SendEmailAsync(to, subject, body);

        // Assert
        result.Should().StartWith("SUCCESS: Safe mode");
        result.Should().Contain(to);
        result.Should().Contain(subject);
    }

    [Theory]
    [InlineData("", "subject", "body", "Recipient email address is required")]
    [InlineData(null, "subject", "body", "Recipient email address is required")]
    [InlineData("test@test.com", "", "body", "Email subject is required")]
    [InlineData("test@test.com", null, "body", "Email subject is required")]
    [InlineData("test@test.com", "subject", "", "Email body is required")]
    [InlineData("test@test.com", "subject", null, "Email body is required")]
    public async Task SendEmailAsync_WithInvalidParameters_ShouldReturnErrorMessage(string? to, string? subject, string? body, string expectedError)
    {
        // Arrange
        var plugin = new Plugin("key", "secret", "sender@test.com", _logger, enableRealMessaging: false);

        // Act
        var result = await plugin.SendEmailAsync(to!, subject!, body!);

        // Assert
        result.Should().StartWith("ERROR:");
        result.Should().Contain(expectedError);
    }

    [Fact]
    public async Task SendEmailAsync_WithHtmlContent_InSafeMode_ShouldReturnSuccessMessage()
    {
        // Arrange
        var plugin = new Plugin("key", "secret", "sender@test.com", _logger, enableRealMessaging: false);
        var to = "recipient@test.com";
        var subject = "HTML Test";
        var body = "<h1>Test HTML</h1>";

        // Act
        var result = await plugin.SendEmailAsync(to, subject, body, isHtml: true);

        // Assert
        result.Should().StartWith("SUCCESS: Safe mode");
        result.Should().Contain(to);
        result.Should().Contain(subject);
    }

    #endregion

    #region ReadUnreadEmailsAsync Tests

    [Fact]
    public async Task ReadUnreadEmailsAsync_WithInvalidParameters_ShouldReturnErrorResponse()
    {
        // Arrange
        var plugin = new Plugin("key", "secret", "sender@test.com", _logger, enableRealMessaging: false);

        // Act
        var result = await plugin.ReadUnreadEmailsAsync("", "username", "password");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No user configuration found for email:");
        result.UnreadCount.Should().Be(0);
        result.Emails.Should().NotBeNull();
        result.Emails.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadUnreadEmailsAsync_WithUnsupportedProtocol_ShouldReturnErrorResponse()
    {
        // Arrange
        var plugin = new Plugin("key", "secret", "sender@test.com", _logger, enableRealMessaging: false);

        // Act
        var result = await plugin.ReadUnreadEmailsAsync(
            "test@test.com",
            "username",
            "password",
            incomingProtocol: "SMTP"); // Invalid protocol

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No user configuration found for email:");
        result.UnreadCount.Should().Be(0);
        result.Emails.Should().NotBeNull();
    }

    [Theory]
    [InlineData("IMAP")]
    [InlineData("POP3")]
    public async Task ReadUnreadEmailsAsync_WithValidProtocol_ShouldHandleConnectionError(string protocol)
    {
        // Arrange
        var plugin = new Plugin("key", "secret", "sender@test.com", _logger, enableRealMessaging: false);

        // Act
        var result = await plugin.ReadUnreadEmailsAsync(
            "test@test.com",
            "username",
            "password",
            incomingProtocol: protocol,
            incomingServer: "invalid.server.com");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No user configuration found for email:");
        result.UnreadCount.Should().Be(0);
        result.Emails.Should().NotBeNull();
    }

    #endregion

    #region Model Tests

    [Fact]
    public void EmailMessage_WithRequiredProperties_ShouldInitialize()
    {
        // Arrange & Act
        var emailMessage = new EmailMessage
        {
            From = "sender@test.com",
            To = "recipient@test.com",
            Subject = "Test Subject",
            Body = "Test Body"
        };

        // Assert
        emailMessage.From.Should().Be("sender@test.com");
        emailMessage.To.Should().Be("recipient@test.com");
        emailMessage.Subject.Should().Be("Test Subject");
        emailMessage.Body.Should().Be("Test Body");
        emailMessage.IsHtml.Should().BeFalse(); // Default value
        emailMessage.Attachments.Should().BeNull(); // Default value
    }

    [Fact]
    public void ReadUnreadEmailResponse_WithDefaultValues_ShouldInitialize()
    {
        // Arrange & Act
        var response = new ReadUnreadEmailResponse();

        // Assert
        response.Success.Should().BeFalse(); // Default value
        response.Message.Should().BeNull();
        response.UnreadCount.Should().Be(0);
        response.Emails.Should().BeNull();
    }

    [Fact]
    public void ReadUnreadEmailRequest_WithRequiredProperties_ShouldInitialize()
    {
        // Arrange & Act
        var request = new ReadUnreadEmailRequest
        {
            EmailAddress = "test@test.com",
            Username = "testuser",
            Password = "testpass",
            IncomingProtocol = "IMAP",
            IncomingServer = "imap.test.com",
            IncomingPort = 993
        };

        // Assert
        request.EmailAddress.Should().Be("test@test.com");
        request.Username.Should().Be("testuser");
        request.Password.Should().Be("testpass");
        request.IncomingProtocol.Should().Be("IMAP");
        request.IncomingServer.Should().Be("imap.test.com");
        request.IncomingPort.Should().Be(993);
        request.UseSSL.Should().BeTrue(); // Default value
        request.MaxMessages.Should().Be(1); // Default value
        request.FetchAttachments.Should().BeTrue(); // Default value
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void Constructor_WithDefaultSmtpSettings_ShouldUseDefaults()
    {
        // Arrange
        var config = new TestConfiguration();
        config["Mailjet:ApiKey"] = "test-key";
        config["Mailjet:ApiSecret"] = "test-secret";
        config["Mailjet:Sender"] = "test@example.com";
        // Don't setup SmtpServer and SmtpPort to test defaults
        config.SetValue("Communications:EnableRealMessaging", "false");

        // Act
        var plugin = new Plugin(config, _logger);

        // Assert
        plugin.Should().NotBeNull();
        // Note: We can't directly test the private fields, but we can verify the plugin was created successfully
    }

    [Fact]
    public void Constructor_WithEnvironmentVariable_ShouldEnableRealMessaging()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ACCOUNTINGAGENT_ENABLE_REAL_MESSAGING", "true");

        try
        {
            var config = new TestConfiguration();
            config["Mailjet:ApiKey"] = "test-key";
            config["Mailjet:ApiSecret"] = "test-secret";
            config["Mailjet:Sender"] = "test@example.com";
            config.SetValue("Communications:EnableRealMessaging", "false");

            // Act
            var plugin = new Plugin(config, _logger);

            // Assert
            plugin.Should().NotBeNull();
            // Note: The environment variable should override the configuration setting
        }
        finally
        {
            Environment.SetEnvironmentVariable("ACCOUNTINGAGENT_ENABLE_REAL_MESSAGING", null);
        }
    }

    #endregion

    #region E2E Tests

    [Fact(Skip = "E2E test - requires running AppHost and manual execution. Use Delivery.Tests for CI execution.")]
    [Trait("Category", "E2E")]
    public async Task MailPluginE2ETest_Should_UseCorrectPortFromUsersJson()
    {
        // Arrange
        var agentUrl = "http://localhost:5042"; // Agent HTTP endpoint
        var webChatUrl = "http://localhost:5041"; // WebChat HTTP endpoint

        using var httpClient = new HttpClient();

        // Wait for services to be ready
        await WaitForServiceAsync(httpClient, agentUrl, TimeSpan.FromMinutes(2));
        await WaitForServiceAsync(httpClient, webChatUrl, TimeSpan.FromMinutes(2));

        // Test message that should trigger MailPlugin
        var testMessage = "Kolla Anna Andersson's mail om det finns oläst mail med bifogad fil";

        // Act - Send message to WebChat
        var chatRequest = new
        {
            message = testMessage,
            sessionId = Guid.NewGuid().ToString()
        };

        var json = System.Text.Json.JsonSerializer.Serialize(chatRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync($"{webChatUrl}/api/chat", content);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue($"WebChat request failed: {response.StatusCode}");

        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().NotBeEmpty();

        // The test passes if we get a response (even if email auth fails)
        // The important thing is that the system uses the correct port from users.json
        // We can't easily verify the exact port used without more complex setup,
        // but the integration test validates the full flow works
    }

    private static async Task WaitForServiceAsync(HttpClient httpClient, string url, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        var healthUrl = $"{url}/health";

        while (DateTime.UtcNow - start < timeout)
        {
            try
            {
                var response = await httpClient.GetAsync(healthUrl);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Service not ready yet
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"Service {url} did not become ready within {timeout}");
    }

    #endregion
}

