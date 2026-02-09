using KAgent.Core;

namespace KAgent.Core.Tests;

/// <summary>
/// Tests for KAgentConfig validation and sanitization.
/// </summary>
public class KAgentConfigTests : IDisposable
{
    private readonly Dictionary<string, string?> _originalEnvVars = new();

    public KAgentConfigTests()
    {
        // Save original environment variable values
        _originalEnvVars["KAGENT_URL"] = Environment.GetEnvironmentVariable("KAGENT_URL");
        _originalEnvVars["KAGENT_NAME"] = Environment.GetEnvironmentVariable("KAGENT_NAME");
        _originalEnvVars["KAGENT_NAMESPACE"] = Environment.GetEnvironmentVariable("KAGENT_NAMESPACE");
    }

    public void Dispose()
    {
        // Restore original environment variable values
        foreach (var kvp in _originalEnvVars)
        {
            Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
        }
    }

    #region URL Validation Tests

    [Fact]
    public void Constructor_WithValidHttpUrl_Succeeds()
    {
        // Arrange & Act
        var config = new KAgentConfig(
            url: "http://localhost:8080",
            name: "test-agent",
            @namespace: "test-ns"
        );

        // Assert
        Assert.Equal("http://localhost:8080", config.Url);
    }

    [Fact]
    public void Constructor_WithValidHttpsUrl_Succeeds()
    {
        // Arrange & Act
        var config = new KAgentConfig(
            url: "https://api.example.com",
            name: "test-agent",
            @namespace: "test-ns"
        );

        // Assert
        Assert.Equal("https://api.example.com", config.Url);
    }

    [Fact]
    public void Constructor_WithInvalidUrlFormat_ThrowsArgumentException()
    {
        // Arrange, Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new KAgentConfig(
                url: "not-a-valid-url",
                name: "test-agent",
                @namespace: "test-ns"
            )
        );

        Assert.Contains("Invalid URL format", exception.Message);
        Assert.Contains("not-a-valid-url", exception.Message);
    }

    [Fact]
    public void Constructor_WithRelativeUrl_ThrowsArgumentException()
    {
        // Arrange, Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new KAgentConfig(
                url: "/api/endpoint",
                name: "test-agent",
                @namespace: "test-ns"
            )
        );

        // Uri.TryCreate can parse relative URLs as "file://" scheme
        Assert.True(exception.Message.Contains("Invalid URL format") || exception.Message.Contains("must use HTTP or HTTPS scheme"));
    }

    [Fact]
    public void Constructor_WithFtpScheme_ThrowsArgumentException()
    {
        // Arrange, Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new KAgentConfig(
                url: "ftp://example.com",
                name: "test-agent",
                @namespace: "test-ns"
            )
        );

        Assert.Contains("must use HTTP or HTTPS scheme", exception.Message);
    }

    [Fact]
    public void Constructor_WithFileScheme_ThrowsArgumentException()
    {
        // Arrange, Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new KAgentConfig(
                url: "file:///path/to/file",
                name: "test-agent",
                @namespace: "test-ns"
            )
        );

        Assert.Contains("must use HTTP or HTTPS scheme", exception.Message);
    }

    [Fact]
    public void Constructor_WithUrlFromEnvironmentVariable_ValidatesUrl()
    {
        // Arrange
        Environment.SetEnvironmentVariable("KAGENT_URL", "invalid-url");
        Environment.SetEnvironmentVariable("KAGENT_NAME", "test-agent");
        Environment.SetEnvironmentVariable("KAGENT_NAMESPACE", "test-ns");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new KAgentConfig());
        Assert.Contains("Invalid URL format", exception.Message);
    }

    [Fact]
    public void Constructor_WithValidUrlFromEnvironmentVariable_Succeeds()
    {
        // Arrange
        Environment.SetEnvironmentVariable("KAGENT_URL", "https://api.example.com");
        Environment.SetEnvironmentVariable("KAGENT_NAME", "test-agent");
        Environment.SetEnvironmentVariable("KAGENT_NAMESPACE", "test-ns");

        // Act
        var config = new KAgentConfig();

        // Assert
        Assert.Equal("https://api.example.com", config.Url);
    }

    #endregion

    #region Name Sanitization Tests

    [Fact]
    public void Constructor_WithValidName_Succeeds()
    {
        // Arrange & Act
        var config = new KAgentConfig(
            url: "http://localhost:8080",
            name: "test-agent-123",
            @namespace: "test-ns"
        );

        // Assert
        Assert.Equal("test_agent_123", config.Name);
    }

    [Fact]
    public void Constructor_WithNameContainingSpecialCharacters_SanitizesName()
    {
        // Arrange & Act
        var config = new KAgentConfig(
            url: "http://localhost:8080",
            name: "test@agent#123!",
            @namespace: "test-ns"
        );

        // Assert - special characters should be removed
        Assert.Equal("testagent123", config.Name);
    }

    [Fact]
    public void Constructor_WithNameContainingOnlyInvalidCharacters_ThrowsArgumentException()
    {
        // Arrange, Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new KAgentConfig(
                url: "http://localhost:8080",
                name: "@#$%",
                @namespace: "test-ns"
            )
        );

        Assert.Contains("contains only invalid characters", exception.Message);
    }

    [Fact]
    public void Constructor_WithNameContainingDots_PreservesDots()
    {
        // Arrange & Act
        var config = new KAgentConfig(
            url: "http://localhost:8080",
            name: "test.agent.name",
            @namespace: "test-ns"
        );

        // Assert
        Assert.Equal("test.agent.name", config.Name);
    }

    [Fact]
    public void Constructor_WithNameContainingUnderscore_PreservesUnderscore()
    {
        // Arrange & Act
        var config = new KAgentConfig(
            url: "http://localhost:8080",
            name: "test_agent_name",
            @namespace: "test-ns"
        );

        // Assert
        Assert.Equal("test_agent_name", config.Name);
    }

    #endregion

    #region Namespace Sanitization Tests

    [Fact]
    public void Constructor_WithValidNamespace_Succeeds()
    {
        // Arrange & Act
        var config = new KAgentConfig(
            url: "http://localhost:8080",
            name: "test-agent",
            @namespace: "test-ns-123"
        );

        // Assert
        Assert.Equal("test_ns_123", config.Namespace);
    }

    [Fact]
    public void Constructor_WithNamespaceContainingSpecialCharacters_SanitizesNamespace()
    {
        // Arrange & Act
        var config = new KAgentConfig(
            url: "http://localhost:8080",
            name: "test-agent",
            @namespace: "test@ns#123!"
        );

        // Assert - special characters should be removed
        Assert.Equal("testns123", config.Namespace);
    }

    [Fact]
    public void Constructor_WithNamespaceContainingOnlyInvalidCharacters_ThrowsArgumentException()
    {
        // Arrange, Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new KAgentConfig(
                url: "http://localhost:8080",
                name: "test-agent",
                @namespace: "!@#$%"
            )
        );

        Assert.Contains("contains only invalid characters", exception.Message);
    }

    [Fact]
    public void Constructor_WithNamespaceContainingDots_PreservesDots()
    {
        // Arrange & Act
        var config = new KAgentConfig(
            url: "http://localhost:8080",
            name: "test-agent",
            @namespace: "test.ns.name"
        );

        // Assert
        Assert.Equal("test.ns.name", config.Namespace);
    }

    #endregion

    #region AppName Generation Tests

    [Fact]
    public void AppName_GeneratesCorrectFormat()
    {
        // Arrange & Act
        var config = new KAgentConfig(
            url: "http://localhost:8080",
            name: "my-agent",
            @namespace: "my-namespace"
        );

        // Assert
        Assert.Equal("my_namespace__NS__my_agent", config.AppName);
    }

    [Fact]
    public void AppName_WithSanitizedValues_UsesCleanedValues()
    {
        // Arrange & Act
        var config = new KAgentConfig(
            url: "http://localhost:8080",
            name: "agent@123",
            @namespace: "ns#456"
        );

        // Assert
        Assert.Equal("ns456__NS__agent123", config.AppName);
    }

    #endregion

    #region Environment Variable Tests

    [Fact]
    public void Constructor_WithMissingUrlEnvVar_ThrowsArgumentException()
    {
        // Arrange
        Environment.SetEnvironmentVariable("KAGENT_URL", null);
        Environment.SetEnvironmentVariable("KAGENT_NAME", "test-agent");
        Environment.SetEnvironmentVariable("KAGENT_NAMESPACE", "test-ns");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new KAgentConfig());
        Assert.Contains("KAGENT_URL environment variable is not set", exception.Message);
    }

    [Fact]
    public void Constructor_WithMissingNameEnvVar_ThrowsArgumentException()
    {
        // Arrange
        Environment.SetEnvironmentVariable("KAGENT_URL", "http://localhost:8080");
        Environment.SetEnvironmentVariable("KAGENT_NAME", null);
        Environment.SetEnvironmentVariable("KAGENT_NAMESPACE", "test-ns");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new KAgentConfig());
        Assert.Contains("KAGENT_NAME environment variable is not set", exception.Message);
    }

    [Fact]
    public void Constructor_WithMissingNamespaceEnvVar_ThrowsArgumentException()
    {
        // Arrange
        Environment.SetEnvironmentVariable("KAGENT_URL", "http://localhost:8080");
        Environment.SetEnvironmentVariable("KAGENT_NAME", "test-agent");
        Environment.SetEnvironmentVariable("KAGENT_NAMESPACE", null);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new KAgentConfig());
        Assert.Contains("KAGENT_NAMESPACE environment variable is not set", exception.Message);
    }

    [Fact]
    public void Constructor_WithParameterOverridingEnvVar_UsesParameter()
    {
        // Arrange
        Environment.SetEnvironmentVariable("KAGENT_URL", "http://env-url.com");
        Environment.SetEnvironmentVariable("KAGENT_NAME", "env-agent");
        Environment.SetEnvironmentVariable("KAGENT_NAMESPACE", "env-ns");

        // Act
        var config = new KAgentConfig(
            url: "http://param-url.com",
            name: "param-agent",
            @namespace: "param-ns"
        );

        // Assert
        Assert.Equal("http://param-url.com", config.Url);
        Assert.Equal("param_agent", config.Name);
        Assert.Equal("param_ns", config.Namespace);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Constructor_WithWhitespaceName_ThrowsArgumentException()
    {
        // Arrange, Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new KAgentConfig(
                url: "http://localhost:8080",
                name: "   ",
                @namespace: "test-ns"
            )
        );

        Assert.True(exception.Message.Contains("contains only invalid characters") || exception.Message.Contains("cannot be null, empty, or whitespace"));
    }

    [Fact]
    public void Constructor_WithWhitespaceNamespace_ThrowsArgumentException()
    {
        // Arrange, Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new KAgentConfig(
                url: "http://localhost:8080",
                name: "test-agent",
                @namespace: "   "
            )
        );

        Assert.True(exception.Message.Contains("contains only invalid characters") || exception.Message.Contains("cannot be null, empty, or whitespace"));
    }

    [Fact]
    public void Constructor_WithMixedCaseUrl_PreservesCase()
    {
        // Arrange & Act
        var config = new KAgentConfig(
            url: "http://API.Example.COM/Path",
            name: "test-agent",
            @namespace: "test-ns"
        );

        // Assert
        Assert.Equal("http://API.Example.COM/Path", config.Url);
    }

    #endregion
}
