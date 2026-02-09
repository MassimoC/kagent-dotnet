using A2A;

namespace KAgent.MAF.Abstractions;

/// <summary>
/// Represents a Microsoft Agent Framework agent adapter.
/// This interface allows wrapping Microsoft.Agents.AI agents for use with KAgent.
/// When using Microsoft.Agents.AI, create an adapter class that implements this interface
/// and delegates to the AIAgent.
/// </summary>
public interface IMAFAgent
{
    /// <summary>
    /// Gets the unique identifier for the agent.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the human-readable name of the agent.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the instructions or system prompt for the agent.
    /// </summary>
    string? Instructions { get; }

    /// <summary>
    /// Executes the agent with the given input.
    /// </summary>
    /// <param name="input">The user input to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent's response.</returns>
    Task<string> ExecuteAsync(string input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the agent with the given input and session context.
    /// This allows the agent to maintain conversation history across multiple requests.
    /// </summary>
    /// <param name="input">The user input to process.</param>
    /// <param name="sessionId">The session identifier for maintaining context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent's response.</returns>
    Task<string> ExecuteAsync(string input, string sessionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an A2A request context for MAF agent execution.
/// This extends the basic RequestContext with specific fields needed for agent execution.
/// </summary>
public class A2ARequestContext
{
    public required string TaskId { get; init; }
    public required string ContextId { get; init; }
    public required string UserInput { get; init; }
    public string? UserId { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
}

/// <summary>
/// Interface for event queue to stream A2A events.
/// Uses the official A2A SDK event types.
/// </summary>
public interface IA2AEventQueue
{
    Task EnqueueAsync(A2AEvent ev);
    string? GetResult();
}

/// <summary>
/// Interface for agent executor within A2A protocol.
/// This provides a bridge between KAgent and the agent implementation.
/// </summary>
public interface IA2AAgentExecutor
{
    Task ExecuteAsync(A2ARequestContext context, IA2AEventQueue eventQueue);
    Task CancelAsync(A2ARequestContext context, IA2AEventQueue eventQueue);
}
