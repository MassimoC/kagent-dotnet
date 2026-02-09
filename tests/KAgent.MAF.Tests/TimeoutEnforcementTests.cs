using A2A;
using KAgent.MAF;
using KAgent.MAF.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KAgent.MAF.Tests;

public class TimeoutEnforcementTests
{
    [Fact]
    public void HttpClient_TimeoutIsConfigured()
    {
        // Arrange
        var mockAgent = new Mock<IMAFAgent>();
        mockAgent.Setup(a => a.Id).Returns("test-agent");
        mockAgent.Setup(a => a.Name).Returns("Test Agent");

        var config = new MAFAgentExecutorConfig
        {
            HttpClientTimeout = 60
        };

        var httpClient = new HttpClient
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan // Infinite timeout
        };

        // Act
        var executor = new MAFAgentExecutor(
            mockAgent.Object,
            "test-app",
            config,
            httpClient
        );

        // Assert - Verify the timeout was configured
        Assert.Equal(TimeSpan.FromSeconds(60.0), httpClient.Timeout);
    }

    [Fact]
    public void HttpClient_DefaultTimeoutIsSet()
    {
        // Arrange
        var mockAgent = new Mock<IMAFAgent>();
        mockAgent.Setup(a => a.Id).Returns("test-agent");
        mockAgent.Setup(a => a.Name).Returns("Test Agent");

        var config = new MAFAgentExecutorConfig();

        var httpClient = new HttpClient
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan // Infinite timeout
        };

        // Act
        var executor = new MAFAgentExecutor(
            mockAgent.Object,
            "test-app",
            config,
            httpClient
        );

        // Assert - Verify default timeout of 100 seconds is set
        Assert.Equal(TimeSpan.FromSeconds(100.0), httpClient.Timeout);
    }

    [Fact]
    public async Task ExecuteAsync_EnforcesExecutionTimeout()
    {
        // Arrange
        var mockAgent = new Mock<IMAFAgent>();
        mockAgent.Setup(a => a.Id).Returns("test-agent");
        mockAgent.Setup(a => a.Name).Returns("Test Agent");
        
        // Simulate a long-running agent that takes longer than timeout
        mockAgent.Setup(a => a.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string input, string sessionId, CancellationToken ct) =>
            {
                await Task.Delay(10000, ct); // 10 seconds delay
                return "This should timeout";
            });

        var config = new MAFAgentExecutorConfig
        {
            ExecutionTimeout = 1 // 1 second
        };

        var executor = new MAFAgentExecutor(
            mockAgent.Object,
            "test-app",
            config
        );

        var context = new A2ARequestContext
        {
            TaskId = "test-task",
            ContextId = "test-context",
            UserInput = "test input"
        };

        var eventQueue = new TestEventQueue();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TimeoutException>(
            async () => await executor.ExecuteAsync(context, eventQueue)
        );

        Assert.Contains("timed out after 1 second", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_CompletesWithinTimeout()
    {
        // Arrange
        var mockAgent = new Mock<IMAFAgent>();
        mockAgent.Setup(a => a.Id).Returns("test-agent");
        mockAgent.Setup(a => a.Name).Returns("Test Agent");
        
        // Fast executing agent
        mockAgent.Setup(a => a.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Success");

        var config = new MAFAgentExecutorConfig
        {
            ExecutionTimeout = 5 // 5 second timeout
        };

        var executor = new MAFAgentExecutor(
            mockAgent.Object,
            "test-app",
            config
        );

        var context = new A2ARequestContext
        {
            TaskId = "test-task",
            ContextId = "test-context",
            UserInput = "test input"
        };

        var eventQueue = new TestEventQueue();

        // Act - Should not throw
        await executor.ExecuteAsync(context, eventQueue);

        // Assert
        Assert.Equal("Success", eventQueue.GetResult());
    }
}

/// <summary>
/// Simple test event queue for testing
/// </summary>
internal class TestEventQueue : IA2AEventQueue
{
    private readonly List<A2AEvent> _events = new();
    private string? _result;

    public Task EnqueueAsync(A2AEvent ev)
    {
        _events.Add(ev);
        if (ev is TaskArtifactUpdateEvent artifactEvent && artifactEvent.Artifact?.Parts != null)
        {
            foreach (var part in artifactEvent.Artifact.Parts)
            {
                if (part is TextPart textPart)
                {
                    _result = textPart.Text;
                }
            }
        }
        return Task.CompletedTask;
    }

    public string? GetResult() => _result;

    public List<A2AEvent> GetEvents() => _events;
}
