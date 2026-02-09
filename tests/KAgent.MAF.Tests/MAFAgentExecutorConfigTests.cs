using KAgent.MAF;
using Xunit;

namespace KAgent.MAF.Tests;

public class MAFAgentExecutorConfigTests
{
    [Fact]
    public void MAFAgentExecutorConfig_DefaultValues()
    {
        // Arrange & Act
        var config = new MAFAgentExecutorConfig();

        // Assert - Verify current default values
        Assert.Equal(300.0, config.ExecutionTimeout);
        Assert.Equal(100.0, config.HttpClientTimeout);
        Assert.False(config.EnableInMemoryEventQueue);
    }

    [Fact]
    public void MAFAgentExecutorConfig_CustomValues()
    {
        // Arrange & Act
        var config = new MAFAgentExecutorConfig
        {
            ExecutionTimeout = 600,
            HttpClientTimeout = 120,
            EnableInMemoryEventQueue = true
        };

        // Assert
        Assert.Equal(600.0, config.ExecutionTimeout);
        Assert.Equal(120.0, config.HttpClientTimeout);
        Assert.True(config.EnableInMemoryEventQueue);
    }

    [Fact]
    public void MAFAgentExecutorConfig_HttpClientTimeout_ShouldBeLessThanExecutionTimeout()
    {
        // Arrange & Act
        var config = new MAFAgentExecutorConfig
        {
            ExecutionTimeout = 300,
            HttpClientTimeout = 100
        };

        // Assert - HttpClientTimeout should be less than ExecutionTimeout for proper retry behavior
        Assert.True(config.HttpClientTimeout < config.ExecutionTimeout,
            "HttpClientTimeout should be less than ExecutionTimeout to allow for retries");
    }
}
