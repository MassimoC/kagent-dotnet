using System.Text.Json;
using KAgent.Core.A2A;

namespace KAgent.Core.Tests.A2A;

/// <summary>
/// Tests for KAgentJsonSerializerOptions.
/// </summary>
public class KAgentJsonSerializerOptionsTests
{
    [Fact]
    public void Default_ShouldReturnConfiguredOptions()
    {
        // Act
        var options = KAgentJsonSerializerOptions.Default;

        // Assert
        Assert.NotNull(options);
        Assert.Equal(JsonNamingPolicy.CamelCase, options.PropertyNamingPolicy);
        Assert.False(options.WriteIndented);
    }

    [Fact]
    public void Default_ShouldBeReadOnly()
    {
        // Act
        var options = KAgentJsonSerializerOptions.Default;

        // Assert
        Assert.NotNull(options);
        
        // Attempting to modify a read-only options should throw
        Assert.Throws<InvalidOperationException>(() => 
            options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
    }

    [Fact]
    public void Default_ShouldHaveTypeInfoResolver()
    {
        // Act
        var options = KAgentJsonSerializerOptions.Default;

        // Assert
        Assert.NotNull(options);
        Assert.NotNull(options.TypeInfoResolver);
    }

    [Fact]
    public void Default_ShouldSerializeObjectsCorrectly()
    {
        // Arrange
        var testObject = new { Name = "Test", Value = 123 };

        // Act
        var json = JsonSerializer.Serialize(testObject, KAgentJsonSerializerOptions.Default);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("\"name\":", json); // Verify camelCase naming
        Assert.Contains("\"value\":", json);
    }

    [Fact]
    public void Default_ShouldDeserializeObjectsCorrectly()
    {
        // Arrange
        var json = "{\"name\":\"Test\",\"value\":123}";

        // Act
        var result = JsonSerializer.Deserialize<TestClass>(json, KAgentJsonSerializerOptions.Default);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test", result.Name);
        Assert.Equal(123, result.Value);
    }

    private class TestClass
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
