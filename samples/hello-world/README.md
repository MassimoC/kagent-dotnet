# Hello World MAF Agent Sample

This is a simple "Hello World" sample demonstrating the KAgent integration for Microsoft Agent Framework using **Azure OpenAI with direct AIAgent integration**.

## Overview

This sample creates a joke-telling agent using Azure OpenAI that demonstrates:
- Direct `AIAgent` integration from a chat client (no adapter classes needed)
- A2A protocol endpoints (standard Agent-to-Agent communication)
- KAgent backend integration (optional task persistence)

## Prerequisites

- .NET 10.0 SDK
- Azure OpenAI service with a deployed model (e.g., gpt-4o-mini)
- Azure OpenAI API key
- (Optional) Docker for containerized deployment
- (Optional) KAgent backend running for full functionality

## Quick Start

### 1. Set Environment Variables

```bash
# Required: Azure OpenAI Configuration
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o-mini"
export AZURE_FOUNDRY_OPENAI_API_KEY="your-api-key"

# Optional: KAgent backend configuration (not required for basic operation)
# If not set, the agent will still work but without task persistence
export KAGENT_URL="http://localhost:3000"
export KAGENT_NAME="hello-world"
export KAGENT_NAMESPACE="samples"

# Optional: Feature flags
# Use in-memory event queue instead of persistent storage
export ENABLE_INMEMORYEVENTQUEUE="1"
```

**Authentication**: The sample uses `ApiKeyCredential` for Azure OpenAI authentication.

> **Note**: For production deployments, use Managed Identity instead of API keys. You can switch to managed identity by replacing `ApiKeyCredential` with `DefaultAzureCredential` from the `Azure.Identity` package:

### 2. Run the Sample

```bash
cd samples/hello-world
dotnet run
```

The agent will start on `http://localhost:8080`.

### 3. Test the Agent

Get agent information:
```bash
curl http://localhost:8080/.well-known/agent-card.json
```

Send a message:
```bash
curl -X POST http://localhost:8080/ \
  -H "Content-Type: application/json" \
  -d '{
	"jsonrpc": "2.0",
	"id": "1",
	"method": "message/send",
	"params": {
		"message": {
			"kind": "message",
			"role": "user",
			"messageId": "123456789",
			"parts": [
				{
					"kind": "text",
					"text": "tell me a joke about kubernetes"
				}
			]
		}
	}
}'
```

## Docker Deployment

Build the Docker image from the repository root:

```bash
docker build -f Dockerfile -t hello-world-agent .
```

Run the container with Azure credentials:

```bash
docker run -p 8080:8080 \
  -e AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/" \
  -e AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o-mini" \
  -e AZURE_FOUNDRY_OPENAI_API_KEY="your-api-key" \
  -e KAGENT_URL="http://localhost:3000"  \
  -e KAGENT_NAME="hello-world"  \
  -e KAGENT_NAMESPACE="samples" \
  -e OTEL_TRACING_ENABLED="true" \
  -e OTEL_TRACING_EXPORTER_OTLP_ENDPOINT="http://localhost:4317" \
  hello-world-agent
```

> **Note**: For production deployments, use Managed Identity instead of API keys.

## Code Overview

The key code that creates and runs the agent:

```csharp
// Get Azure OpenAI configuration from environment variables
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") 
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") 
    ?? "gpt-4o-mini";
var apiKey = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_OPENAI_API_KEY")
    ?? "api-key-not-set";

// Get optional configuration flags
var enableInMemoryEventQueue =
    Environment.GetEnvironmentVariable("ENABLE_INMEMORYEVENTQUEUE") == "1";

// Create Microsoft.Agents.AI agent using ChatClientBuilder
AIAgent agent = new ChatClientBuilder(
        new AzureOpenAIClient(
            new Uri(endpoint),
            new ApiKeyCredential(apiKey))
        .GetChatClient(deploymentName)
        .AsIChatClient())
    .BuildAIAgent(
        instructions: "You answer to any generic question, keep in mind you are good at telling jokes.",
        name: "Joker",
        tools: null);

// Configure the agent card using official A2A SDK AgentCard
var agentCard = new AgentCard
{
    Name = "hello-world-agent",
    Description = "Hello world always answers your questions, with a bit of humor.",
    Version = "0.1.0",
    DefaultInputModes = ["text"],
    DefaultOutputModes = ["text"],
    Capabilities = new AgentCapabilities
    {
        Streaming = true
    },
    Skills = new List<AgentSkill>
    {
        new AgentSkill
        {
            Name = "say-hello",
            Description = "Returns a friendly greeting message",
            InputModes = ["text"],
            OutputModes = ["text"]
        },
        new AgentSkill
        {
            Name = "ask-anything",
            Description = "Returns a very generic answer to every question you have",
            InputModes = ["text"],
            OutputModes = ["text"]
        }
    }
};

// Build the KAgent application with configurable features
var app = new KAgentApp(
    agent: agent,
    agentCard: agentCard,
    executorConfig: new MAFAgentExecutorConfig
    {
        EnableInMemoryEventQueue = enableInMemoryEventQueue
    },
    tracing: true
);

await app.Build().RunAsync();
```

## Key Features

- **Direct AIAgent Integration**: Uses `ChatClientBuilder` to create agents from Azure OpenAI chat clients
- **A2A Protocol Support**: Exposes standard endpoints like `/.well-known/agent-card` and `/` for messages
- **Agent Card**: Defines agent capabilities and skills using the official A2A SDK types
- **Skills Definition**: Declares what the agent can do (say-hello, ask-anything)
- **Configurable Features**:
  - Event queue persistence (in-memory or TaskStore)
  - OpenTelemetry tracing
- **Session Management**: Supports ContextId-based conversation sessions via AgentThread
- **Conversation History**: Automatic conversation history tracking and restoration via KAgent backend

## Learn More

- [kagent-maf README](../../kagent-maf/src/README.md) - Complete documentation for KAgent.MAF
- [core README](../../core/README.md) - Documentation for KAgent.Core components
