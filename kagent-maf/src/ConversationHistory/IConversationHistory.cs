using A2A;

namespace KAgent.MAF.ConversationHistory;

/// <summary>
/// Represents a conversation message in the history.
/// </summary>
public class ConversationMessage
{
    /// <summary>
    /// Gets or sets the unique identifier for this message.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or sets the role of the message sender (User or Agent).
    /// </summary>
    public required MessageRole Role { get; init; }

    /// <summary>
    /// Gets or sets the content parts of the message.
    /// </summary>
    public required List<Part> Parts { get; init; }

    /// <summary>
    /// Gets or sets the timestamp when the message was created.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets or sets optional metadata for the message.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Interface for managing conversation history in agent sessions.
/// Enables storing and retrieving message history for continuous conversations.
/// </summary>
public interface IConversationHistory
{
    /// <summary>
    /// Adds a message to the conversation history.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="message">The message to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddMessageAsync(string sessionId, ConversationMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves conversation history for a session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="limit">Optional maximum number of messages to retrieve. If null, retrieves all messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of conversation messages in chronological order.</returns>
    Task<List<ConversationMessage>> GetHistoryAsync(string sessionId, int? limit = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all conversation history for a session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ClearHistoryAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a session has conversation history.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the session has history, false otherwise.</returns>
    Task<bool> HasHistoryAsync(string sessionId, CancellationToken cancellationToken = default);
}
