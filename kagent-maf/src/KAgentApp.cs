using A2A;
using A2A.AspNetCore;
using KAgent.Core;
using KAgent.Core.Tracing;
using KAgent.MAF.Abstractions;
using KAgent.MAF.ConversationHistory;
using Microsoft.Agents.AI;
using System.Text.Json;
using KAgentTaskStore = KAgent.Core.A2A.KAgentTaskStore;
using IKAgentTaskStore = KAgent.Core.A2A.ITaskStore;
using IKAgentRequestContextBuilder = KAgent.Core.A2A.IRequestContextBuilder;
using KAgentRequestContextBuilder = KAgent.Core.A2A.KAgentRequestContextBuilder;
using A2ATaskStatus = KAgent.Core.A2A.A2ATaskStatus;

namespace KAgent.MAF;

/// <summary>
/// KAgent application builder for Microsoft Agent Framework agents.
/// Provides A2A protocol integration for MAF agents with session and task persistence.
/// Uses the official A2A SDK for .NET.
/// </summary>
public class KAgentApp
{
    private readonly AIAgent _agent;
    private readonly AgentCard _agentCard;
    private readonly KAgentConfig _config;
    private readonly MAFAgentExecutorConfig _executorConfig;
    private readonly bool _tracing;

    /// <summary>
    /// Initializes a new instance of the KAgentApp class with an IMAFAgent.
    /// </summary>
    /// <param name="agent">The Microsoft Agent Framework agent to host.</param>
    /// <param name="agentCard">The A2A agent card describing the agent's capabilities.</param>
    /// <param name="config">Optional KAgent configuration. If null, reads from environment variables.</param>
    /// <param name="executorConfig">Optional executor configuration.</param>
    /// <param name="tracing">Whether to enable OpenTelemetry tracing. Default is true.</param>
    public KAgentApp(
        AIAgent agent,
        AgentCard agentCard,
        KAgentConfig? config = null,
        MAFAgentExecutorConfig? executorConfig = null,
        bool tracing = true)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _agentCard = agentCard ?? throw new ArgumentNullException(nameof(agentCard));
        _config = config ?? new KAgentConfig();
        _executorConfig = executorConfig ?? new MAFAgentExecutorConfig();
        _tracing = tracing;
    }

    /// <summary>
    /// Builds and configures the ASP.NET Core web application with A2A integration.
    /// </summary>
    /// <returns>A configured WebApplication ready to run.</returns>
    public WebApplication Build()
    {
        var builder = WebApplication.CreateBuilder();

        // Configure services
        builder.Services.AddSingleton(_config);
        builder.Services.AddSingleton(_agentCard);
        builder.Services.AddSingleton(_executorConfig);
        builder.Services.AddSingleton(_agent);
        builder.Services.AddSingleton<IKAgentRequestContextBuilder, KAgentRequestContextBuilder>();

        // Configure HttpClient for KAgent backend
        builder.Services.AddHttpClient("KAgent", client =>
        {
            client.BaseAddress = new Uri(_config.Url);
            client.Timeout = TimeSpan.FromSeconds(_executorConfig.HttpClientTimeout);
        });

        // Register KAgent core services
        builder.Services.AddSingleton<IKAgentTaskStore>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("KAgent");
            return new KAgentTaskStore(httpClient);
        });

        //// Register IMAFAgent - wrap it with AIAgentAdapter that has conversation history support
        builder.Services.AddSingleton<IMAFAgent>(sp =>
        {
            IConversationHistory? conversationHistory = sp.GetService<IConversationHistory>();
            return new AIAgentAdapter(_agent, conversationHistory);
        });

        //Use KAgent backend conversation history
        builder.Services.AddSingleton<IConversationHistory>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("KAgent");
            var logger = sp.GetRequiredService<ILogger<KAgentConversationHistory>>();
            return new KAgentConversationHistory(httpClient, _config.AppName, logger, defaultUserId: "anonymous");
        });

        // Configure logging
        builder.Services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Information);
        });

        // Configure OpenTelemetry tracing if enabled (before building the app)
        if (_tracing)
        {
            TracingConfiguration.ConfigureServices(builder.Services, _config.AppName);
        }

        var app = builder.Build();

        // Create TaskManager and attach the agent using A2A pattern
        var taskManager = new TaskManager();
        
        // Attach message processing handler
        taskManager.OnMessageReceived = async (messageSendParams, cancellationToken) =>
        {
            var logger = app.Services.GetRequiredService<ILogger<KAgentApp>>();
            var taskStore = app.Services.GetRequiredService<IKAgentTaskStore>();
            var contextBuilder = app.Services.GetRequiredService<IKAgentRequestContextBuilder>();
            var httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
            var conversationHistory = app.Services.GetService<IConversationHistory>();
            
            try
            {
                var taskId = Guid.NewGuid().ToString();
                var contextId = messageSendParams.Message.ContextId ?? Guid.NewGuid().ToString();
                
                // Extract user input from message
                var userInput = string.Empty;
                var textPart = messageSendParams.Message.Parts.OfType<TextPart>().FirstOrDefault();
                if (textPart != null)
                {
                    userInput = textPart.Text;
                }
                
                logger.LogInformation("Request context for task {TaskId}, context {ContextId} : {request}", taskId, contextId, userInput);

                // Extract headers for user context
                // Note: In the TaskManager pattern with MapA2A, HTTP request headers are not directly
                // available in OnMessageReceived. Clients should pass user context through 
                // MessageSendParams.Metadata if user identification is needed.
                // Example: client sends metadata like { "x-user-id": "user123" }
                var headers = new Dictionary<string, string>();
                
                // Parse MessageSendParams.Metadata to extract user context
                if (messageSendParams.Metadata != null)
                {
                    foreach (var kvp in messageSendParams.Metadata)
                    {
                        // Convert metadata values to strings for headers dictionary
                        // Handle common header keys like x-user-id, x-request-id, etc.
                        string? value = null;
                        
                        if (kvp.Value is JsonElement element)
                        {
                            // Handle JsonElement type (common when deserializing JSON)
                            value = element.ValueKind == JsonValueKind.String 
                                ? element.GetString() 
                                : element.GetRawText();
                        }
                        else
                        {
                            // Handle other types (string, int, etc.)
                            // Note: Assumes kvp.Value is not null. If null, ToString() will throw,
                            // which is acceptable as it indicates invalid metadata from the caller.
                            value = kvp.Value.ToString();
                        }
                        
                        if (!string.IsNullOrEmpty(value))
                        {
                            headers[kvp.Key] = value;
                        }
                    }
                    
                    logger.LogDebug("Extracted {Count} headers from metadata", headers.Count);
                }

                // Build request context with user information
                logger.LogInformation("Building request context for task {TaskId}, context {ContextId}", taskId, contextId);
                var requestContext = await contextBuilder.BuildAsync(
                    taskId: taskId,
                    contextId: contextId,
                    headers: headers
                );

                // Create context for MAF executor
                logger.LogInformation("Creating A2A request context for task {TaskId}, context {ContextId}", taskId, contextId);    
                var context = new A2ARequestContext
                {
                    TaskId = taskId,
                    ContextId = contextId,
                    UserInput = userInput,
                    UserId = requestContext.User?.UserId,
                    Headers = headers
                };

                // Create event queue that persists to task store
                logger.LogInformation("Creating task-persisting event queue for task {TaskId}", taskId);

                IA2AEventQueue eventQueue =
                    _executorConfig.EnableInMemoryEventQueue
                        ? new InMemoryEventQueue()
                        : new TaskPersistingEventQueue(taskStore, taskId, logger);



                // Execute agent
                logger.LogInformation("Executing agent {AgentName} for task {TaskId}", _agentCard.Name, taskId);    
                
                // Get the agent from the service provider
                var agent = app.Services.GetRequiredService<IMAFAgent>();
                
                var executor = new MAFAgentExecutor(
                    agent,
                    _config.AppName,
                    _executorConfig,
                    httpClientFactory.CreateClient("KAgent"),
                    app.Services.GetRequiredService<ILogger<MAFAgentExecutor>>(),
                    taskStore,
                    conversationHistory
                );

                await executor.ExecuteAsync(context, eventQueue);

                // Return response as AgentMessage
                logger.LogInformation("Agent execution completed for task {TaskId}", taskId);
                var result = eventQueue.GetResult();

                return new AgentMessage
                {
                    Role = MessageRole.Agent,
                    MessageId = Guid.NewGuid().ToString(),
                    ContextId = contextId,
                    Parts = new List<Part>
                    {
                        new TextPart { Text = result ?? "No response generated." }
                    }
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing message");
                return new AgentMessage
                {
                    Role = MessageRole.Agent,
                    MessageId = Guid.NewGuid().ToString(),
                    ContextId = messageSendParams.Message.ContextId ?? Guid.NewGuid().ToString(),
                    Parts = new List<Part>
                    {
                        new TextPart { Text = $"Error: {ex.Message}" }
                    }
                };
            }
        };

        // Attach agent card handler
        taskManager.OnAgentCardQuery = (agentUrl, cancellationToken) =>
        {
            // Update the agent card URL dynamically if needed
            var card = _agentCard;
            if (!string.IsNullOrEmpty(agentUrl))
            {
                card.Url = agentUrl;
            }
            return Task.FromResult(card);
        };

        // Map health endpoint
        app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTimeOffset.UtcNow }));
        // Map A2A endpoints using TaskManager
        app.MapA2A(taskManager, "/");
        app.MapWellKnownAgentCard(taskManager, "/");
        app.MapHttpA2A(taskManager, "/");

        app.Logger.LogInformation("KAgent MAF application configured for agent: {AgentName}", _agentCard.Name);
        app.Logger.LogInformation("KAgent backend URL: {Url}", _config.Url);
        app.Logger.LogInformation("App name: {AppName}", _config.AppName);
        app.Logger.LogInformation("A2A endpoints mapped using TaskManager");

        if (_tracing)
        {
            app.Logger.LogInformation("OpenTelemetry tracing is enabled");
        }

        return app;
    }
}

/// <summary>
/// Event queue that persists events to the task store.
/// This implementation updates the task in the TaskStore based on enqueued events,
/// similar to how the A2A SDK's EventQueue works in Python implementations.
/// </summary>
internal class TaskPersistingEventQueue : IA2AEventQueue
{
    private readonly List<A2AEvent> _events = new();
    private readonly IKAgentTaskStore _taskStore;
    private readonly string _taskId;
    private readonly ILogger _logger;
    private string? _result;

    public TaskPersistingEventQueue(IKAgentTaskStore taskStore, string taskId, ILogger logger)
    {
        _taskStore = taskStore ?? throw new ArgumentNullException(nameof(taskStore));
        _taskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task EnqueueAsync(A2AEvent ev)
    {
        _events.Add(ev);
        
        try
        {
            // Get the current task
            var task = await _taskStore.GetAsync(_taskId);
            if (task == null)
            {
                _logger.LogWarning("Task {TaskId} not found when enqueueing event", _taskId);
                return;
            }

            // Update task based on event type
            if (ev is TaskArtifactUpdateEvent artifactEvent && artifactEvent.Artifact != null)
            {
                // Add artifact to task
                task.Artifacts ??= new List<Artifact>();
                task.Artifacts.Add(artifactEvent.Artifact);
                
                // Extract text result for response
                foreach (var part in artifactEvent.Artifact.Parts ?? new List<Part>())
                {
                    if (part is TextPart textPart)
                    {
                        _result = textPart.Text;
                    }
                }
                
                await _taskStore.SaveAsync(task);
                _logger.LogDebug("Added artifact to task {TaskId}", _taskId);
            }
            else if (ev is TaskStatusUpdateEvent statusEvent)
            {
                var status = statusEvent.Status;
                
                // Update task status
                task.Status = new A2ATaskStatus
                {
                    State = status.State.ToString().ToLowerInvariant(),
                    Timestamp = status.Timestamp.ToString("O")
                };
                
                // If there's a message in the status, add it to history
                if (status.Message != null)
                {
                    task.History ??= new List<AgentMessage>();
                    task.History.Add(status.Message);
                }
                
                await _taskStore.SaveAsync(task);
                _logger.LogDebug("Updated task {TaskId} status to {State}", _taskId, task.Status.State);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error persisting event to task {TaskId}", _taskId);
            // Don't throw - event is still queued in memory
        }
    }

    public List<A2AEvent> GetEvents() => _events;
    public string? GetResult() => _result;
}

/// <summary>
/// Internal adapter that wraps a Microsoft.Agents.AI AIAgent to implement IMAFAgent.
/// This adapter is automatically used when AIAgent is passed directly to KAgentApp.
/// Manages agent threads per session ID to maintain conversation context.
/// </summary>
internal class AIAgentAdapter : IMAFAgent, IDisposable
{
    private readonly AIAgent _aiAgent;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, AgentThread> _threads;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _restoredSessions;
    private readonly IConversationHistory? _conversationHistory;
    private bool _disposed;

    public AIAgentAdapter(AIAgent aiAgent, IConversationHistory? conversationHistory = null)
    {
        _aiAgent = aiAgent ?? throw new ArgumentNullException(nameof(aiAgent));
        _threads = new System.Collections.Concurrent.ConcurrentDictionary<string, AgentThread>();
        _restoredSessions = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>();
        _conversationHistory = conversationHistory;
    }

    public string Id => _aiAgent.Id ?? string.Empty;
    public string Name => _aiAgent.Name ?? string.Empty;
    // Note: AIAgent.Description is used for Instructions. In Microsoft.Agents.AI,
    // the instructions are set during agent creation and stored in the Description property.
    public string? Instructions => _aiAgent.Description;

    public async Task<string> ExecuteAsync(string input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "Input cannot be null, empty, or whitespace.";
        }

        try
        {
            // For backward compatibility, create a new thread when no session ID is provided
            var thread = _aiAgent.GetNewThread();
            var response = await _aiAgent.RunAsync(input, thread, options: null, cancellationToken);
            
            // Handle dynamic response type - extract text content
            if (response == null)
            {
                return "Agent execution completed but no response was generated.";
            }
            
            // Try to extract text content from the response
            // The response object should have a way to get the text output
            var responseText = response.ToString();
            
            return !string.IsNullOrEmpty(responseText) 
                ? responseText 
                : "Agent execution completed but no response text was generated.";
        }
        catch (Exception ex)
        {
            return $"Agent execution failed: {ex.Message}";
        }
    }

    public async Task<string> ExecuteAsync(string input, string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "Input cannot be null, empty, or whitespace.";
        }

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            // Fall back to non-session execution
            return await ExecuteAsync(input, cancellationToken);
        }

        try
        {
            // Check if this is a new thread that needs history restoration
            bool isNewThread = !_threads.ContainsKey(sessionId);
            string actualInput = input;

            // if AgentThread is empty but the session is available in kagent, load it. 
            if (isNewThread && _conversationHistory != null && !_restoredSessions.ContainsKey(sessionId))
            {
                try
                {
                    if (await _conversationHistory.HasHistoryAsync(sessionId, cancellationToken: cancellationToken))
                    {
                        var history = await _conversationHistory.GetHistoryAsync(sessionId, cancellationToken: cancellationToken);

                        if (history != null && history.Count > 0)
                        {
                            // Build context from historical messages
                            var contextBuilder = new System.Text.StringBuilder();
                            contextBuilder.AppendLine("Previous conversation history:");
                            contextBuilder.AppendLine();

                            foreach (var msg in history)
                            {
                                var role = msg.Role == MessageRole.User ? "User" : "Assistant";
                                var text = msg.Parts.OfType<TextPart>().FirstOrDefault()?.Text ?? "[No text content]";
                                contextBuilder.AppendLine($"{role}: {text}");
                            }

                            contextBuilder.AppendLine();
                            contextBuilder.AppendLine("Current user message:");
                            contextBuilder.AppendLine(input);

                            actualInput = contextBuilder.ToString();

                            // Mark this session as restored so we don't prepend history again
                            _restoredSessions.TryAdd(sessionId, true);
                        }
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException)
                {
                    // If history restoration fails, continue without history 
                }
            }
            
            // Get or create an AgentThread for this sessionId using ConcurrentDictionary's thread-safe GetOrAdd
            // AgentThread maintains conversation history automatically via Microsoft Agent Framework
            var thread = _threads.GetOrAdd(sessionId, _ => _aiAgent.GetNewThread());

            // Run the agent with the thread - the thread maintains all conversation history
            var response = await _aiAgent.RunAsync(actualInput, thread, options: null, cancellationToken);
            
            // Handle dynamic response type - extract text content
            if (response == null)
            {
                return "Agent execution completed but no response was generated.";
            }
            
            // Try to extract text content from the response
            var responseText = response.ToString();
            
            return !string.IsNullOrEmpty(responseText) 
                ? responseText 
                : "Agent execution completed but no response text was generated.";
        }
        catch (Exception ex)
        {
            return $"Agent execution failed: {ex.Message}";
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _threads.Clear();
        _disposed = true;
    }
}

/// <summary>
/// Simple in-memory event queue for demonstration.
/// Uses the official A2A SDK event types.
/// </summary>
internal class InMemoryEventQueue : IA2AEventQueue
{
    private readonly List<A2AEvent> _events = new();
    private string? _result;

    public Task EnqueueAsync(A2AEvent ev)
    {
        _events.Add(ev);
        if (ev is TaskArtifactUpdateEvent artifactEvent && artifactEvent.Artifact?.Parts != null)
        {
            // Extract text from artifact parts
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
    public List<A2AEvent> GetEvents() => _events;
    public string? GetResult() => _result;
}