using Microsoft.Extensions.Logging;
using A2A;
using KAgent.MAF.Abstractions;
using KAgent.MAF.ConversationHistory;
using KAgent.Core.A2A;
using KAgentTaskStore = KAgent.Core.A2A.KAgentTaskStore;
using IKAgentTaskStore = KAgent.Core.A2A.ITaskStore;
using A2ATask = KAgent.Core.A2A.A2ATask;

namespace KAgent.MAF;

/// <summary>
/// Constants for task metadata.
/// </summary>
internal static class TaskMetadataConstants
{
    public const string StatusCompleted = "completed";
    public const string StatusFailed = "failed";
    public const string TaskKind = "task";
}

/// <summary>
/// Configuration for MAF Agent Executor.
/// </summary>
public class MAFAgentExecutorConfig
{
    /// <summary>
    /// Gets or sets the execution timeout in seconds.
    /// This timeout applies to the entire agent execution including all API calls.
    /// Default is 300 seconds (5 minutes).
    /// </summary>
    public int ExecutionTimeout { get; set; } = 300;

    /// <summary>
    /// Gets or sets the HTTP client timeout in seconds.
    /// This timeout applies to individual HTTP requests to the KAgent backend.
    /// Default is 100 seconds to match HttpClient default.
    /// Should be less than ExecutionTimeout to allow for retries.
    /// </summary>
    public int HttpClientTimeout { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether to enable use the InMemoryEventQueue.
    /// </summary>
    public bool EnableInMemoryEventQueue { get; set; } = false;

}

/// <summary>
/// Executes Microsoft Agent Framework agents within the A2A protocol.
/// This is the core component that bridges MAF agents with the A2A (Agent-to-Agent) protocol.
/// Implements session and task persistence via KAgent backend.
/// Uses the official A2A SDK for .NET.
/// </summary>
public class MAFAgentExecutor : IA2AAgentExecutor
{
    private readonly IMAFAgent _agent;
    private readonly string _appName;
    private readonly MAFAgentExecutorConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<MAFAgentExecutor> _logger;
    private readonly IKAgentTaskStore? _taskStore;
    private readonly IConversationHistory? _conversationHistory;

    public MAFAgentExecutor(
        IMAFAgent agent,
        string appName,
        MAFAgentExecutorConfig? config = null,
        HttpClient? httpClient = null,
        ILogger<MAFAgentExecutor>? logger = null,
        IKAgentTaskStore? taskStore = null,
        IConversationHistory? conversationHistory = null)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _appName = appName ?? throw new ArgumentNullException(nameof(appName));
        _config = config ?? new MAFAgentExecutorConfig();
        _httpClient = httpClient ?? new HttpClient();
        
        // Configure HttpClient timeout if not already set
        if (_httpClient.Timeout == System.Threading.Timeout.InfiniteTimeSpan)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_config.HttpClientTimeout);
        }
        
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MAFAgentExecutor>.Instance;
        _taskStore = taskStore;
        _conversationHistory = conversationHistory;
    }

    /// <summary>
    /// Execute the MAF agent within the A2A protocol.
    /// </summary>
    public async Task ExecuteAsync(A2ARequestContext context, IA2AEventQueue eventQueue)
    {
        // Create a CancellationTokenSource with execution timeout
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.ExecutionTimeout));
        var cancellationToken = timeoutCts.Token;
        
        //Create TaskResultAggregator to track task state across execution
        var taskResultAggregator = new TaskResultAggregator();
        
        try
        {
            _logger.LogInformation("MAF Agent execution started for **TASK** {TaskId}", context.TaskId);

            // Initialize task in KAgent backend if TaskStore is available
            if (_taskStore != null)
            {
                _logger.LogInformation("Initialize **TASK** in KAgent backend :: {ContextId} -> {TaskId}", context.ContextId, context.TaskId);

                // Create initial user message for history
                var userMessage = new AgentMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Role = MessageRole.User,
                    Parts = new List<Part> { new TextPart { Text = context.UserInput } },
                    ContextId = context.ContextId
                };

                var task = new A2ATask
                {
                    Id = context.TaskId,
                    Kind = TaskMetadataConstants.TaskKind,
                    ContextId = context.ContextId,
                    History = new List<AgentMessage> { userMessage },
                    Artifacts = new List<Artifact>(),
                    Status = new A2ATaskStatus
                    {
                        State = "working",
                        Timestamp = DateTimeOffset.UtcNow.ToString("O")
                    },
                    Metadata = new Dictionary<string, object>
                    {
                        ["kagent_app_name"] = _appName,
                        ["kagent_author"] = _agent.Id,
                        ["kagent_user_id"] = context.UserId ?? "anonymous",
                        ["kagent_session_id"] = context.ContextId
                    }
                };
                
                await _taskStore.SaveAsync(task, cancellationToken);
                _logger.LogInformation("**TASK** {TaskId} initialized in KAgent backend", context.TaskId);
            }

            // Send initial "working" status using official A2A TaskStatusUpdateEvent
            var initialEvent = new TaskStatusUpdateEvent
            {
                TaskId = context.TaskId,
                ContextId = context.ContextId,
                Status = new AgentTaskStatus
                {
                    State = TaskState.Working,
                    Timestamp = DateTimeOffset.UtcNow
                },
                Final = false
            };
            
            // Process event through aggregator
            taskResultAggregator.ProcessEvent(initialEvent);
            await eventQueue.EnqueueAsync(initialEvent);

            // Execute the MAF agent
            _logger.LogInformation("Executing **AGENT** '{AgentId}' with input: {Input}", 
                _agent.Id, context.UserInput);

            string result = await _agent.ExecuteAsync(context.UserInput, context.ContextId, cancellationToken);

            _logger.LogInformation("**AGENT**  '{AgentId}' execution completed successfully", _agent.Id);
            _logger.LogDebug(result);

            // Send result as artifact using official A2A TaskArtifactUpdateEvent
            await eventQueue.EnqueueAsync(new TaskArtifactUpdateEvent
            {
                TaskId = context.TaskId,
                ContextId = context.ContextId,
                Artifact = new Artifact
                {
                    Parts = new List<Part>
                    {
                        new TextPart { Text = result ?? "No response generated." }
                    }
                }
            });

            // Use aggregated state to determine final task status
            // If the aggregated state is still "Working", it means no errors occurred
            // and we can mark the task as completed
            var finalState = taskResultAggregator.TaskState == TaskState.Working 
                ? TaskState.Completed 
                : taskResultAggregator.TaskState;

            var finalEvent = new TaskStatusUpdateEvent
            {
                TaskId = context.TaskId,
                ContextId = context.ContextId,
                Status = new AgentTaskStatus
                {
                    State = finalState,
                    Message = taskResultAggregator.TaskStatusMessage,
                    Timestamp = DateTimeOffset.UtcNow
                },
                Final = true
            };
            
            await eventQueue.EnqueueAsync(finalEvent);

            // Update task with final result if TaskStore is available
            if (_taskStore != null)
            {
                var task = await _taskStore.GetAsync(context.TaskId, cancellationToken);
                if (task != null)
                {
                    // Add agent response message to history
                    var agentMessage = new AgentMessage
                    {
                        MessageId = Guid.NewGuid().ToString(),
                        Role = MessageRole.Agent,
                        Parts = new List<Part> { new TextPart { Text = result ?? "No response generated." } }
                    };
                    task.History ??= new List<AgentMessage>();
                    task.History.Add(agentMessage);

                    // Add artifact with the final result
                    var artifact = new Artifact
                    {
                        ArtifactId = Guid.NewGuid().ToString(),
                        Parts = new List<Part> { new TextPart { Text = result ?? "No response generated." } }
                    };
                    task.Artifacts ??= new List<Artifact>();
                    task.Artifacts.Add(artifact);

                    // Update status to completed or failed based on aggregated state
                    var stateString = finalState == TaskState.Completed 
                        ? TaskMetadataConstants.StatusCompleted
                        : finalState == TaskState.Failed
                            ? TaskMetadataConstants.StatusFailed
                            : finalState.ToString().ToLowerInvariant();
                    
                    task.Status = new A2ATaskStatus
                    {
                        State = stateString,
                        Timestamp = DateTimeOffset.UtcNow.ToString("O")
                    };

                    // Update metadata (keeping kagent_ prefix)
                    task.Metadata ??= new Dictionary<string, object>();
                    
                    await _taskStore.SaveAsync(task, cancellationToken);
                    _logger.LogDebug("Task {TaskId} updated with final result in KAgent backend", context.TaskId);
                }
            }
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            // Handle execution timeout specifically
            var timeoutSeconds = _config.ExecutionTimeout;
            var timeoutMessage = $"Agent execution timed out after {timeoutSeconds} {(timeoutSeconds == 1.0 ? "second" : "seconds")}";
            _logger.LogError("Execution timeout for task {TaskId}: {Message}", context.TaskId, timeoutMessage);
            
            // Create and process timeout event through aggregator
            var timeoutEvent = new TaskStatusUpdateEvent
            {
                TaskId = context.TaskId,
                ContextId = context.ContextId,
                Status = new AgentTaskStatus
                {
                    State = TaskState.Failed,
                    Message = new AgentMessage
                    {
                        Role = MessageRole.Agent,
                        Parts = new List<Part>
                        {
                            new TextPart { Text = timeoutMessage }
                        }
                    },
                    Timestamp = DateTimeOffset.UtcNow
                },
                Final = true
            };
            
            taskResultAggregator.ProcessEvent(timeoutEvent);
            await eventQueue.EnqueueAsync(timeoutEvent);

            // Update task with timeout error if TaskStore is available
            if (_taskStore != null)
            {
                try
                {
                    // Use a fresh cancellation token for cleanup operations
                    using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var task = await _taskStore.GetAsync(context.TaskId, cleanupCts.Token);
                    if (task != null)
                    {
                        // Add timeout message to history
                        var timeoutMsg = new AgentMessage
                        {
                            MessageId = Guid.NewGuid().ToString(),
                            Role = MessageRole.Agent,
                            Parts = new List<Part> { new TextPart { Text = timeoutMessage } }
                        };
                        task.History ??= new List<AgentMessage>();
                        task.History.Add(timeoutMsg);

                        // Update status to failed
                        task.Status = new A2ATaskStatus
                        {
                            State = TaskMetadataConstants.StatusFailed,
                            Timestamp = DateTimeOffset.UtcNow.ToString("O")
                        };
                        
                        await _taskStore.SaveAsync(task, cleanupCts.Token);
                    }
                }
                catch (Exception saveEx)
                {
                    _logger.LogWarning(saveEx, "Failed to update task {TaskId} with timeout status", context.TaskId);
                }
            }

            throw new TimeoutException(timeoutMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during MAF agent execution for task {TaskId}", context.TaskId);
            
            // Create and process failure event through aggregator
            var failureEvent = new TaskStatusUpdateEvent
            {
                TaskId = context.TaskId,
                ContextId = context.ContextId,
                Status = new AgentTaskStatus
                {
                    State = TaskState.Failed,
                    Message = new AgentMessage
                    {
                        Role = MessageRole.Agent,
                        Parts = new List<Part>
                        {
                            new TextPart { Text = $"Execution failed: {ex.Message}" }
                        }
                    },
                    Timestamp = DateTimeOffset.UtcNow
                },
                Final = true
            };
            
            taskResultAggregator.ProcessEvent(failureEvent);
            await eventQueue.EnqueueAsync(failureEvent);

            // Update task with error if TaskStore is available
            if (_taskStore != null)
            {
                try
                {
                    // Use a fresh cancellation token for cleanup operations
                    using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var task = await _taskStore.GetAsync(context.TaskId, cleanupCts.Token);
                    if (task != null)
                    {
                        // Add error message to history
                        var errorMessage = new AgentMessage
                        {
                            MessageId = Guid.NewGuid().ToString(),
                            Role = MessageRole.Agent,
                            Parts = new List<Part> { new TextPart { Text = $"Execution failed: {ex.Message}" } }
                        };
                        task.History ??= new List<AgentMessage>();
                        task.History.Add(errorMessage);

                        // Update status to failed
                        task.Status = new A2ATaskStatus
                        {
                            State = TaskMetadataConstants.StatusFailed,
                            Timestamp = DateTimeOffset.UtcNow.ToString("O")
                        };
                        
                        await _taskStore.SaveAsync(task, cleanupCts.Token);
                    }
                }
                catch (Exception saveEx)
                {
                    _logger.LogWarning(saveEx, "Failed to update task {TaskId} with error status", context.TaskId);
                }
            }

            throw;
        }
    }

    /// <summary>
    /// Cancel the agent execution.
    /// </summary>
    public Task CancelAsync(A2ARequestContext context, IA2AEventQueue eventQueue)
    {
        _logger.LogWarning("Cancellation requested for task {TaskId} but not yet implemented", context.TaskId);
        throw new NotImplementedException("Cancellation is not yet implemented.");
    }
}
