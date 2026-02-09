namespace KAgent.Core.A2A;

/// <summary>
/// A simple user implementation for KAgent integration.
/// </summary>
public class KAgentUser
{
    public required string UserId { get; init; }

    public bool IsAuthenticated => false;

    public string UserName => UserId;
}

/// <summary>
/// Represents the context for an A2A request.
/// This is a simplified version focusing on user information.
/// </summary>
public class RequestContext
{
    public KAgentUser? User { get; set; }
    public string? TaskId { get; set; }
    public string? ContextId { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
}

/// <summary>
/// Interface for building request contexts.
/// </summary>
public interface IRequestContextBuilder
{
    /// <summary>
    /// Build a request context from available parameters.
    /// </summary>
    Task<RequestContext> BuildAsync(
        string? taskId = null,
        string? contextId = null,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A request context builder that extracts user_id from headers.
/// </summary>
public class KAgentRequestContextBuilder : IRequestContextBuilder
{
    private readonly ITaskStore _taskStore;

    public KAgentRequestContextBuilder(ITaskStore taskStore)
    {
        _taskStore = taskStore ?? throw new ArgumentNullException(nameof(taskStore));
    }

    public Task<RequestContext> BuildAsync(
        string? taskId = null,
        string? contextId = null,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var context = new RequestContext
        {
            TaskId = taskId,
            ContextId = contextId,
            Headers = headers
        };

        // Extract user ID from headers if available
        if (headers != null && headers.TryGetValue("x-user-id", out var userId))
        {
            context.User = new KAgentUser { UserId = userId };
        }

        return Task.FromResult(context);
    }
}
