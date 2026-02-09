using A2A;
using KAgent.MAF.ConversationHistory;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace KAgent.MAF.Tests.ConversationHistory;

public class KAgentConversationHistoryTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<KAgentConversationHistory>> _mockLogger;
    private readonly string _appName = "test-app";

    public KAgentConversationHistoryTests()
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };
        _mockLogger = new Mock<ILogger<KAgentConversationHistory>>();
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsEmptyListWhenSessionNotFound()
    {
        // Arrange
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains("/api/sessions/test-session/messages")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var history = new KAgentConversationHistory(_httpClient, _appName, _mockLogger.Object);

        // Act
        var messages = await history.GetHistoryAsync("test-session");

        // Assert
        Assert.Empty(messages);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsMessagesFromBackend()
    {
        // Arrange
        var agentMessages = new List<AgentMessage>
        {
            new AgentMessage
            {
                MessageId = "msg-1",
                Role = MessageRole.User,
                Parts = new List<Part> { new TextPart { Text = "Hello" } }
            },
            new AgentMessage
            {
                MessageId = "msg-2",
                Role = MessageRole.Agent,
                Parts = new List<Part> { new TextPart { Text = "Hi there!" } }
            }
        };

        var json = JsonSerializer.Serialize(agentMessages, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains("/api/sessions/test-session/messages")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        var history = new KAgentConversationHistory(_httpClient, _appName, _mockLogger.Object);

        // Act
        var messages = await history.GetHistoryAsync("test-session");

        // Assert
        Assert.Equal(2, messages.Count);
        Assert.Equal("msg-1", messages[0].Id);
        Assert.Equal(MessageRole.User, messages[0].Role);
        Assert.Equal("Hello", ((TextPart)messages[0].Parts[0]).Text);
        Assert.Equal("msg-2", messages[1].Id);
        Assert.Equal(MessageRole.Agent, messages[1].Role);
        Assert.Equal("Hi there!", ((TextPart)messages[1].Parts[0]).Text);
    }

    [Fact]
    public async Task GetHistoryAsync_RespectsLimitParameter()
    {
        // Arrange
        var agentMessages = new List<AgentMessage>
        {
            new AgentMessage
            {
                MessageId = "msg-1",
                Role = MessageRole.User,
                Parts = new List<Part> { new TextPart { Text = "Message 1" } }
            }
        };

        var json = JsonSerializer.Serialize(agentMessages, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains("/api/sessions/test-session/messages?limit=5")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        var history = new KAgentConversationHistory(_httpClient, _appName, _mockLogger.Object);

        // Act
        var messages = await history.GetHistoryAsync("test-session", limit: 5);

        // Assert
        Assert.Single(messages);
    }

    [Fact]
    public async Task AddMessageAsync_CreatesSessionIfNotExists()
    {
        // Arrange
        // First call to check if session exists - returns NotFound
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Equals("http://localhost:8080/api/sessions/test-session")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        // Second call to create session - returns OK
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Equals("http://localhost:8080/api/sessions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        // Third call to add message - returns OK
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Equals("http://localhost:8080/api/sessions/test-session/messages")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var history = new KAgentConversationHistory(_httpClient, _appName, _mockLogger.Object);
        var message = new ConversationMessage
        {
            Id = "msg-1",
            Role = MessageRole.User,
            Parts = new List<Part> { new TextPart { Text = "Hello" } },
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        await history.AddMessageAsync("test-session", message);

        // Assert - verify session was created
        _mockHttpHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString().Equals("http://localhost:8080/api/sessions")),
            ItExpr.IsAny<CancellationToken>());

        // Assert - verify message was added
        _mockHttpHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString().Equals("http://localhost:8080/api/sessions/test-session/messages")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task HasHistoryAsync_ReturnsTrueWhenSessionExists()
    {
        // Arrange
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains("/api/sessions/test-session")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var history = new KAgentConversationHistory(_httpClient, _appName, _mockLogger.Object);

        // Act
        var hasHistory = await history.HasHistoryAsync("test-session");

        // Assert
        Assert.True(hasHistory);
    }

    [Fact]
    public async Task HasHistoryAsync_ReturnsFalseWhenSessionNotFound()
    {
        // Arrange
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains("/api/sessions/test-session")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var history = new KAgentConversationHistory(_httpClient, _appName, _mockLogger.Object);

        // Act
        var hasHistory = await history.HasHistoryAsync("test-session");

        // Assert
        Assert.False(hasHistory);
    }

    [Fact]
    public async Task ClearHistoryAsync_DeletesSessionMessages()
    {
        // Arrange
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Delete &&
                    req.RequestUri!.ToString().Contains("/api/sessions/test-session/messages")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var history = new KAgentConversationHistory(_httpClient, _appName, _mockLogger.Object);

        // Act
        await history.ClearHistoryAsync("test-session");

        // Assert
        _mockHttpHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Delete &&
                req.RequestUri!.ToString().Contains("/api/sessions/test-session/messages")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ClearHistoryAsync_HandlesNotFoundGracefully()
    {
        // Arrange
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Delete &&
                    req.RequestUri!.ToString().Contains("/api/sessions/test-session/messages")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var history = new KAgentConversationHistory(_httpClient, _appName, _mockLogger.Object);

        // Act & Assert - Should not throw
        await history.ClearHistoryAsync("test-session");
    }

    [Fact]
    public async Task GetHistoryAsync_ThrowsWhenSessionIdIsNull()
    {
        // Arrange
        var history = new KAgentConversationHistory(_httpClient, _appName, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            history.GetHistoryAsync(null!));
    }

    [Fact]
    public async Task AddMessageAsync_ThrowsWhenSessionIdIsNull()
    {
        // Arrange
        var history = new KAgentConversationHistory(_httpClient, _appName, _mockLogger.Object);
        var message = new ConversationMessage
        {
            Id = "msg-1",
            Role = MessageRole.User,
            Parts = new List<Part> { new TextPart { Text = "Hello" } },
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            history.AddMessageAsync(null!, message));
    }

    [Fact]
    public async Task AddMessageAsync_ThrowsWhenMessageIsNull()
    {
        // Arrange
        var history = new KAgentConversationHistory(_httpClient, _appName, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            history.AddMessageAsync("test-session", null!));
    }
}
