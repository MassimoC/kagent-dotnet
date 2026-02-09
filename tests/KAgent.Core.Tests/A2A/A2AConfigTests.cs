using KAgent.Core.A2A;
using Microsoft.Extensions.Logging;
using Moq;

namespace KAgent.Core.Tests.A2A;

/// <summary>
/// Tests for A2A configuration utilities.
/// </summary>
public class A2AConfigTests : IDisposable
{
    private const string EnvVarName = "A2A_MAX_CONTENT_LENGTH";
    private string? _originalValue;

    public A2AConfigTests()
    {
        // Save original environment variable value
        _originalValue = Environment.GetEnvironmentVariable(EnvVarName);
    }

    public void Dispose()
    {
        // Restore original environment variable value
        if (_originalValue != null)
        {
            Environment.SetEnvironmentVariable(EnvVarName, _originalValue);
        }
        else
        {
            Environment.SetEnvironmentVariable(EnvVarName, null);
        }
    }

    [Fact]
    public void GetA2AMaxContentLength_WithEnvVar_ReturnsConfiguredValue()
    {
        // Test that setting A2A_MAX_CONTENT_LENGTH env var returns the configured value
        Environment.SetEnvironmentVariable(EnvVarName, "52428800");
        
        var result = A2AConfig.GetA2AMaxContentLength();
        
        Assert.Equal(52428800, result); // 50MB
    }

    [Fact]
    public void GetA2AMaxContentLength_WithoutEnvVar_ReturnsNull()
    {
        // Test that without env var, null is returned (use a2a-sdk default)
        Environment.SetEnvironmentVariable(EnvVarName, null);
        
        var result = A2AConfig.GetA2AMaxContentLength();
        
        Assert.Null(result);
    }

    [Fact]
    public void GetA2AMaxContentLength_WithZero_ReturnsNull()
    {
        // Test that setting env var to '0' returns null (unlimited)
        Environment.SetEnvironmentVariable(EnvVarName, "0");
        
        var result = A2AConfig.GetA2AMaxContentLength();
        
        Assert.Null(result);
    }

    [Fact]
    public void GetA2AMaxContentLength_WithNoneString_ReturnsNull()
    {
        // Test that setting env var to 'none' returns null (unlimited)
        Environment.SetEnvironmentVariable(EnvVarName, "none");
        
        var result = A2AConfig.GetA2AMaxContentLength();
        
        Assert.Null(result);
    }

    [Fact]
    public void GetA2AMaxContentLength_WithUnlimitedString_ReturnsNull()
    {
        // Test that setting env var to 'unlimited' returns null
        Environment.SetEnvironmentVariable(EnvVarName, "unlimited");
        
        var result = A2AConfig.GetA2AMaxContentLength();
        
        Assert.Null(result);
    }

    [Fact]
    public void GetA2AMaxContentLength_WithInvalidValue_LogsWarningAndReturnsDefault()
    {
        // Test that invalid env var value logs a warning and returns default
        var mockLogger = new Mock<ILogger>();
        Environment.SetEnvironmentVariable(EnvVarName, "not_a_number");
        
        var result = A2AConfig.GetA2AMaxContentLength(mockLogger.Object);
        
        Assert.Equal(10 * 1024 * 1024, result); // 10MB default
        
        // Verify warning was logged
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid") && v.ToString()!.Contains("not_a_number")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GetA2AMaxContentLength_WithNegativeValue_LogsWarningAndReturnsDefault()
    {
        // Test that negative env var value logs a warning and returns default
        var mockLogger = new Mock<ILogger>();
        Environment.SetEnvironmentVariable(EnvVarName, "-1");
        
        var result = A2AConfig.GetA2AMaxContentLength(mockLogger.Object);
        
        Assert.Equal(10 * 1024 * 1024, result); // 10MB default
        
        // Verify warning was logged
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid") && v.ToString()!.Contains("-1") && v.ToString()!.Contains("must be non-negative")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
