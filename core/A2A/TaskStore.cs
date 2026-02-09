using System.Text.Json;
using A2A;

namespace KAgent.Core.A2A;

/// <summary>
/// Represents a task status for persistence.
/// </summary>
public class A2ATaskStatus
{
    /// <summary>
    /// The state of the task.
    /// Valid values: "working", "completed", "failed", "submitted", "auth_required", "input_required".
    /// </summary>
    public string? State { get; set; }
    
    /// <summary>
    /// The timestamp when the status was set, in ISO 8601 format (e.g., "2026-02-03T15:10:34.5039968Z").
    /// </summary>
    public string? Timestamp { get; set; }
}

/// <summary>
/// Represents a task in the A2A protocol.
/// This aligns with the A2A SDK Task structure for proper persistence to KAgent backend.
/// </summary>
public class A2ATask
{
    public required string Id { get; init; }
    public string? Kind { get; init; } = "task";
    public string? ContextId { get; init; }
    public List<AgentMessage>? History { get; set; }
    public List<Artifact>? Artifacts { get; set; }
    public A2ATaskStatus? Status { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Response wrapper for KAgent controller API responses.
/// The KAgent Go controller wraps all task responses in a StandardResponse envelope.
/// </summary>
public class KAgentTaskResponse
{
    public bool Error { get; set; }
    public A2ATask? Data { get; set; }
    public string? Message { get; set; }
}

public class KAgentSessionTasksResponse
{
    public bool Error { get; set; }
    public List<A2ATask>? Data { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Interface for task store implementations.
/// </summary>
public interface ITaskStore
{
    /// <summary>
    /// Save a task to the store.
    /// </summary>
    /// <param name="task">The task to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(A2ATask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve a task from the store.
    /// </summary>
    /// <param name="taskId">The ID of the task to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The task if found, null otherwise.</returns>
    Task<A2ATask?> GetAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a task from the store.
    /// </summary>
    /// <param name="taskId">The ID of the task to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Wait for a task to be saved (event-based sync).
    /// This method is used to synchronize with the save operation instead of
    /// using arbitrary sleep delays. It's particularly useful after interrupts
    /// to ensure the task state is persisted before resuming.
    /// </summary>
    /// <param name="taskId">The ID of the task to wait for.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WaitForSaveAsync(string taskId, TimeSpan timeout, CancellationToken cancellationToken = default);
}

/// <summary>
/// A task store that persists A2A tasks to KAgent via REST API.
/// </summary>
public class KAgentTaskStore : ITaskStore
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, TaskCompletionSource<bool>> _saveEvents = new();

    public KAgentTaskStore(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Check if a history item is a partial ADK streaming event.
    /// </summary>
    private static bool IsPartialEvent(AgentMessage message)
    {
        // Check metadata for adk_partial flag
        if (message.Metadata != null && message.Metadata.TryGetValue("adk_partial", out var value))
        {
            // Handle JsonElement case
            if (value is JsonElement element)
            {
                return element.ValueKind == JsonValueKind.True;
            }
        }
        return false;
    }

    /// <summary>
    /// Remove partial streaming events from history.
    /// </summary>
    private static List<AgentMessage>? CleanPartialEvents(List<AgentMessage>? history)
    {
        if (history == null)
            return null;

        return history.Where(item => !IsPartialEvent(item)).ToList();
    }

    public async Task SaveAsync(A2ATask task, CancellationToken cancellationToken = default)
    {
        // Validate task data before serialization
        if (task == null)
            throw new ArgumentNullException(nameof(task));
        
        if (string.IsNullOrWhiteSpace(task.Id))
            throw new ArgumentException("Task ID cannot be null, empty, or whitespace.", nameof(task));

        // Clean any partial events from history before saving
        task.History = CleanPartialEvents(task.History);

        var json = JsonSerializer.Serialize(task, KAgentJsonSerializerOptions.Default);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync("/api/tasks", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        // Signal that save completed (event-based sync)
        if (_saveEvents.TryGetValue(task.Id, out var tcs))
        {
            tcs.TrySetResult(true);
        }
    }

    public async Task<A2ATask?> GetAsync(string taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/tasks/{taskId}", cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var wrapped = JsonSerializer.Deserialize<KAgentTaskResponse>(json, KAgentJsonSerializerOptions.Default);
            
            return wrapped?.Data;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"/api/tasks/{taskId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task WaitForSaveAsync(string taskId, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<bool>();
        _saveEvents[taskId] = tcs;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            var timeoutTask = Task.Delay(timeout);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                throw new TimeoutException($"Task save timeout after {timeout.TotalSeconds} seconds");
            }
        }
        finally
        {
            _saveEvents.Remove(taskId);
        }
    }
}
