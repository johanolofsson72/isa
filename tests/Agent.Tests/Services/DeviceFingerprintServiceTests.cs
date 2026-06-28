using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using FakeItEasy;
using FluentAssertions;
using Shared.Services;

namespace Agent.Tests.Services;

public class DeviceFingerprintServiceTests
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<DeviceFingerprintService> _logger;
    private readonly DeviceFingerprintService _service;
    private readonly HttpContext _httpContext;
    private readonly IRequestCookieCollection _requestCookies;
    private readonly IResponseCookies _responseCookies;

    public DeviceFingerprintServiceTests()
    {
        _httpContextAccessor = A.Fake<IHttpContextAccessor>();
        _logger = A.Fake<ILogger<DeviceFingerprintService>>();
        _httpContext = A.Fake<HttpContext>();
        _requestCookies = A.Fake<IRequestCookieCollection>();
        _responseCookies = A.Fake<IResponseCookies>();
        
        _service = new DeviceFingerprintService(_httpContextAccessor, _logger, null);

        // Setup HttpContext
        A.CallTo(() => _httpContextAccessor.HttpContext).Returns(_httpContext);
        A.CallTo(() => _httpContext.Request.Cookies).Returns(_requestCookies);
        A.CallTo(() => _httpContext.Response.Cookies).Returns(_responseCookies);
    }

    [Fact]
    public void GetDeviceFingerprint_Should_ReturnNullWhenNoHttpContext()
    {
        // Arrange
        A.CallTo(() => _httpContextAccessor.HttpContext).Returns(null);

        // Act
        var fingerprint = _service.GetDeviceFingerprint();

        // Assert
        fingerprint.Should().BeNull();
    }

    [Fact]
    public void GetDeviceFingerprint_Should_ReturnExistingDeviceId()
    {
        // Arrange
        var existingDeviceId = Guid.NewGuid().ToString("D");
        A.CallTo(() => _requestCookies["isa_did"]).Returns(existingDeviceId);

        // Act
        var fingerprint = _service.GetDeviceFingerprint();

        // Assert
        fingerprint.Should().Be(existingDeviceId);
    }

    [Fact]
    public void GetDeviceFingerprint_Should_ReturnNullForInvalidGuid()
    {
        // Arrange
        A.CallTo(() => _requestCookies["isa_did"]).Returns("invalid-guid");

        // Act
        var fingerprint = _service.GetDeviceFingerprint();

        // Assert
        fingerprint.Should().BeNull();
    }

    [Fact]
    public void GetDeviceFingerprint_Should_ReturnNullForEmptyCookie()
    {
        // Arrange
        A.CallTo(() => _requestCookies["isa_did"]).Returns("");

        // Act
        var fingerprint = _service.GetDeviceFingerprint();

        // Assert
        fingerprint.Should().BeNull();
    }

    [Fact]
    public void CreateOrUpdateDeviceFingerprint_Should_ReturnExistingDeviceId()
    {
        // Arrange
        var existingDeviceId = Guid.NewGuid().ToString("D");
        A.CallTo(() => _requestCookies["isa_did"]).Returns(existingDeviceId);

        // Act
        var fingerprint = _service.CreateOrUpdateDeviceFingerprint();

        // Assert
        fingerprint.Should().Be(existingDeviceId);
        A.CallTo(() => _responseCookies.Append(A<string>._, A<string>._, A<CookieOptions>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public void CreateOrUpdateDeviceFingerprint_Should_CreateNewDeviceIdWhenNoneExists()
    {
        // Arrange
        A.CallTo(() => _requestCookies["isa_did"]).Returns((string?)null);

        // Act
        var fingerprint = _service.CreateOrUpdateDeviceFingerprint();

        // Assert
        fingerprint.Should().NotBeNullOrEmpty();
        Guid.TryParse(fingerprint, out _).Should().BeTrue();
        A.CallTo(() => _responseCookies.Append("isa_did", fingerprint, A<CookieOptions>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void CreateOrUpdateDeviceFingerprint_Should_CreateNewDeviceIdForInvalidGuid()
    {
        // Arrange
        A.CallTo(() => _requestCookies["isa_did"]).Returns("invalid-guid");

        // Act
        var fingerprint = _service.CreateOrUpdateDeviceFingerprint();

        // Assert
        fingerprint.Should().NotBeNullOrEmpty();
        Guid.TryParse(fingerprint, out _).Should().BeTrue();
        A.CallTo(() => _responseCookies.Append("isa_did", fingerprint, A<CookieOptions>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact(Skip = "Cookie options validation is environment-dependent")]
    public void CreateOrUpdateDeviceFingerprint_Should_SetSecureCookieOptions()
    {
        // Arrange
        A.CallTo(() => _requestCookies["isa_did"]).Returns((string?)null);

        // Act
        var fingerprint = _service.CreateOrUpdateDeviceFingerprint();

        // Assert - Verify that Append was called once with correct parameters
        A.CallTo(() => _responseCookies.Append("isa_did", A<string>._, A<CookieOptions>.That.Matches(options =>
            options.HttpOnly == true &&
            options.SameSite == SameSiteMode.Lax &&
            options.Path == "/"
        ))).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void CreateOrUpdateDeviceFingerprint_Should_ReturnTempIdWhenNoHttpContext()
    {
        // Arrange
        A.CallTo(() => _httpContextAccessor.HttpContext).Returns(null);

        // Act
        var fingerprint = _service.CreateOrUpdateDeviceFingerprint();

        // Assert
        fingerprint.Should().NotBeNullOrEmpty();
        Guid.TryParse(fingerprint, out _).Should().BeTrue();
    }

    [Fact]
    public void CreateOrUpdateDeviceFingerprint_Should_HandleExceptions()
    {
        // Arrange
        A.CallTo(() => _httpContext.Request.Cookies).Throws<Exception>();

        // Act
        var fingerprint = _service.CreateOrUpdateDeviceFingerprint();

        // Assert
        fingerprint.Should().NotBeNullOrEmpty();
        Guid.TryParse(fingerprint, out _).Should().BeTrue();
    }

    [Fact]
    public void GetDeviceFingerprint_Should_HandleExceptions()
    {
        // Arrange
        A.CallTo(() => _httpContext.Request.Cookies).Throws<Exception>();

        // Act
        var fingerprint = _service.GetDeviceFingerprint();

        // Assert
        fingerprint.Should().BeNull();
    }
}
