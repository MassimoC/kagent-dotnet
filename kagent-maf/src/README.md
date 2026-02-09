# KAgent.MAF -  KAgent Integration for Microsoft Agent Framework

This package provides Microsoft Agent Framework integration for KAgent with A2A (Agent-to-Agent) server support and session-aware memory storage.

## Current Status

**Using Preview Packages**

This library now uses the official preview packages:
- **Microsoft.Agents.AI** v1.0.0-preview.260108.1 - For creating AI agents
- **A2A** v0.3.3-preview - Core A2A protocol implementation
- **A2A.AspNetCore** v0.3.3-preview - ASP.NET Core integration

## Features:
- **A2A Server Integration**: Compatible with KAgent's Agent-to-Agent protocol
- **Direct AIAgent Integration** - Pass Microsoft.Agents.AI agents directly
- **Session and Task Persistence** via KAgent TaskStore. Every task is saved to KAgent backend with metadata
- **Conversation History** - Automatic conversation history tracking via KAgent backend

## Available Endpoints

When you run a KAgent.MAF application, it exposes the following endpoints:

- `GET /health` - Health check endpoint
- `GET /.well-known/agent-card.json` - Agent card with capabilities (A2A standard)
- `POST /` - A2A interactions

These endpoints are automatically mapped using the A2A SDK's `MapA2A`, `MapWellKnownAgentCard`, and `MapHttpA2A` extension methods.


## Quick Start

### Direct Integration

The simplest way to integrate Microsoft.Agents.AI agents using Azure OpenAI.

```csharp
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI;
using A2A;
using KAgent.MAF;
using System.ClientModel;

// Get configuration from environment variables
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") 
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") 
    ?? "gpt-4o-mini";
var apiKey = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_OPENAI_API_KEY")
    ?? throw new InvalidOperationException("AZURE_FOUNDRY_OPENAI_API_KEY is not set.");

// Create AIAgent using ChatClientBuilder
var agent = new ChatClientBuilder(
        new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey))
            .GetChatClient(deploymentName)
            .AsIChatClient())
    .BuildAIAgent("You are a helpful assistant.", "my-agent");


// Configure agent card using official A2A SDK AgentCard
var agentCard = new AgentCard
{
    Name = "my-agent",
    Description = "A helpful AI assistant",
    Version = "1.0.0",
    Capabilities = new AgentCapabilities { Streaming = true }
};

// Pass AIAgent directly
var app = new KAgentApp(
    agent,
    agentCard,
    new MAFAgentExecutorConfig
    {
        ExecutionTimeout = 300,      // Overall execution timeout in seconds (default: 300)
        HttpClientTimeout = 100,     // HTTP request timeout in seconds (default: 100)
        EnableInMemoryEventQueue = false
    },
    tracing: true);

await app.Build().RunAsync();
```


### Conversation History

KAgent.MAF leverages **Microsoft Agent Framework's built-in conversation history** through AgentThread objects. The framework automatically:

1. Maintains AgentThread per ContextId - Each unique session gets its own thread
2. Preserves conversation context - All messages in the thread are automatically included in subsequent requests
3. Reload conversation history via KAgent backend for durability across restarts

See [Microsoft's documentation](https://learn.microsoft.com/en-us/agent-framework/user-guide/agents/agent-memory) for more details on custom memory providers.

#### How History Restoration Works

When an application restarts and a user sends a message with an existing ContextId:

1. The system checks if the AgentThread for that session exists in memory
2. If not (first request after restart), it queries the KAgent backend for historical messages
3. Historical messages are prepended to the user's current message as context
4. The agent processes the request with full awareness of previous conversation
5. Future messages in the same session continue normally without re-loading history

### Timeout Configuration

KAgent.MAF provides configurable timeouts to ensure reliable operation:

#### ExecutionTimeout (default: 300 seconds)
Controls the overall timeout for agent execution, including all operations (API calls, processing, etc.). If the agent execution exceeds this timeout, it will be cancelled and a timeout error will be reported.

#### HttpClientTimeout (default: 100 seconds)
Controls the timeout for individual HTTP requests to the KAgent backend. This should typically be less than ExecutionTimeout to allow for retries.

Example configuration:
```csharp
new MAFAgentExecutorConfig
{
    ExecutionTimeout = 600,      // 10 minutes for overall execution
    HttpClientTimeout = 120,     // 2 minutes for each HTTP request
    EnableInMemoryEventQueue = false
}
```

### Event Queues

KAgent.MAF uses event queues to stream A2A protocol events during agent execution. Two implementations are available:

- TaskPersistingEventQueue (Default) : Persists events to the KAgent backend TaskStore
- InMemoryEventQueue. Stores events in memory without persistence. Useful for local development without KAgent backend:
