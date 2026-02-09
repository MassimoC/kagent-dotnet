using A2A;
using KAgent.Core.A2A;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace KAgent.MAF.ConversationHistory;

/// <summary>
/// Implementation of conversation history using KAgent sessions API.
/// Stores conversation messages as events in KAgent sessions.
/// </summary>
public class KAgentConversationHistory : IConversationHistory
{
    private readonly HttpClient _httpClient;
    private readonly string _appName;
    private readonly string _defaultUserId;
    private readonly ILogger<KAgentConversationHistory> _logger;

    public KAgentConversationHistory(
        HttpClient httpClient,
        string appName,
        ILogger<KAgentConversationHistory>? logger = null,
        string defaultUserId = "anonymous")
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _appName = appName ?? throw new ArgumentNullException(nameof(appName));
        _defaultUserId = defaultUserId ?? "anonymous";
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<KAgentConversationHistory>.Instance;
    }

    public async Task AddMessageAsync(string sessionId, ConversationMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty.", nameof(sessionId));
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        try
        {
            // Ensure session exists
            await EnsureSessionExistsAsync(sessionId, cancellationToken);

            // Convert ConversationMessage to AgentMessage for storage
            var agentMessage = new AgentMessage
            {
                MessageId = message.Id,
                Role = message.Role,
                Parts = message.Parts
            };
            
            // Convert metadata if present
            if (message.Metadata != null)
            {
                agentMessage.Metadata = new Dictionary<string, JsonElement>();
                foreach (var kvp in message.Metadata)
                {
                    agentMessage.Metadata[kvp.Key] = JsonSerializer.SerializeToElement(kvp.Value, KAgentJsonSerializerOptions.Default);
                }
            }

            // Add message to session
            var messageJson = JsonSerializer.Serialize(agentMessage, KAgentJsonSerializerOptions.Default);
            var content = new StringContent(messageJson, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"/api/sessions/{sessionId}/messages", content, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            _logger.LogDebug("Added message {MessageId} to session {SessionId}", message.Id, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add message to session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<List<ConversationMessage>> GetHistoryAsync(string sessionId, int? limit = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty.", nameof(sessionId));

        try
        {
            // Build query parameters
            var queryParams = limit.HasValue ? $"?limit={limit.Value}" : "";
            
            var response = await _httpClient.GetAsync($"/api/sessions/{sessionId}/tasks{queryParams}", cancellationToken);
            
            // If session doesn't exist, return empty list
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogDebug("No history found for session {SessionId}", sessionId);
                return new List<ConversationMessage>();
            }
            
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var kagentHistory = JsonSerializer.Deserialize<KAgentSessionTasksResponse>(json, KAgentJsonSerializerOptions.Default);

            // Convert agentMessages.Data.History to ConversationMessage
            var messages = new List<ConversationMessage>();

            if (kagentHistory?.Data != null)
            {
                foreach (var task in kagentHistory.Data)
                {
                    if (task.History == null)
                        continue;

                    foreach (var item in task.History)
                    {
                        messages.Add(new ConversationMessage
                        {
                            Id = item.MessageId ?? Guid.NewGuid().ToString(),
                            Role = item.Role,
                            Parts = item.Parts ?? new(),
                            Timestamp = DateTimeOffset.UtcNow
                        });
                    }
                }
            }
            
            _logger.LogDebug("Retrieved {Count} messages from session {SessionId}", messages.Count, sessionId);
            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get history for session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task ClearHistoryAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty.", nameof(sessionId));

        try
        {
            var response = await _httpClient.DeleteAsync($"/api/sessions/{sessionId}/messages", cancellationToken);
            
            // Ignore 404 errors - session might not exist
            if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                response.EnsureSuccessStatusCode();
            }
            
            _logger.LogDebug("Cleared history for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear history for session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<bool> HasHistoryAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty.", nameof(sessionId));

        try
        {
            var response = await _httpClient.GetAsync($"/api/sessions/{sessionId}", cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
            
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check history for session {SessionId}", sessionId);
            return false;
        }
    }

    private async Task EnsureSessionExistsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to get the session
            var response = await _httpClient.GetAsync($"/api/sessions/{sessionId}", cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                await CreateSessionAsync(sessionId, cancellationToken);
            }
            else
            {
                response.EnsureSuccessStatusCode();
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await CreateSessionAsync(sessionId, cancellationToken);
        }
    }

    private async Task CreateSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = new
        {
            id = sessionId,
            appName = _appName,
            userId = _defaultUserId,
            createdAt = DateTimeOffset.UtcNow.ToString("O")
        };
        
        var json = JsonSerializer.Serialize(session, KAgentJsonSerializerOptions.Default);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync("/api/sessions", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        _logger.LogDebug("Created session {SessionId} for user {UserId}", sessionId, _defaultUserId);
    }
}
