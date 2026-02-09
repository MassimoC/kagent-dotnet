using System.Net;
using System.Net.Http;
using System.Text.Json;
using KAgent.Core.A2A;
using Moq;
using Moq.Protected;

namespace KAgent.Core.Tests.A2A;

/// <summary>
/// Tests for KAgentTaskStore validation.
/// </summary>
public class TaskStoreTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly KAgentTaskStore _taskStore;

    public TaskStoreTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };
        _taskStore = new KAgentTaskStore(_httpClient);
    }

    [Fact]
    public async Task SaveAsync_WithNullTask_ThrowsArgumentNullException()
    {
        // Arrange
        A2ATask? task = null;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _taskStore.SaveAsync(task!, CancellationToken.None));
        Assert.Equal("task", exception.ParamName);
    }

    [Fact]
    public async Task SaveAsync_WithEmptyTaskId_ThrowsArgumentException()
    {
        // Arrange
        var task = new A2ATask
        {
            Id = ""
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => 
            _taskStore.SaveAsync(task, CancellationToken.None));
        Assert.Equal("task", exception.ParamName);
        Assert.Contains("Task ID cannot be null, empty, or whitespace", exception.Message);
    }

    [Fact]
    public async Task SaveAsync_WithWhitespaceTaskId_ThrowsArgumentException()
    {
        // Arrange
        var task = new A2ATask
        {
            Id = "   "
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => 
            _taskStore.SaveAsync(task, CancellationToken.None));
        Assert.Equal("task", exception.ParamName);
        Assert.Contains("Task ID cannot be null, empty, or whitespace", exception.Message);
    }

    [Fact]
    public async Task SaveAsync_WithValidTaskId_CallsHttpClient()
    {
        // Arrange
        var task = new A2ATask
        {
            Id = "valid-task-id",
            Kind = "task"
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Post && 
                    req.RequestUri!.ToString().EndsWith("/api/tasks")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}")
            });

        // Act
        await _taskStore.SaveAsync(task, CancellationToken.None);

        // Assert - verify HTTP call was made
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => 
                req.Method == HttpMethod.Post && 
                req.RequestUri!.ToString().EndsWith("/api/tasks")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_WithValidTask_SerializesCorrectly()
    {
        // Arrange
        var task = new A2ATask
        {
            Id = "task-123",
            Kind = "task",
            ContextId = "context-456",
            Status = new A2ATaskStatus
            {
                State = "working",
                Timestamp = "2026-02-08T12:00:00Z"
            }
        };

        HttpRequestMessage? capturedRequest = null;
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) =>
            {
                capturedRequest = req;
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{}")
                };
            });

        // Act
        await _taskStore.SaveAsync(task, CancellationToken.None);

        // Assert - verify request was captured and contains expected data
        Assert.NotNull(capturedRequest);
        var content = await capturedRequest.Content!.ReadAsStringAsync();
        Assert.Contains("task-123", content);
        Assert.Contains("task", content);
        Assert.Contains("context-456", content);
    }
}
