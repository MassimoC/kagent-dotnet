# KAgent.Core

Core components for building KAgent-enabled .NET applications with A2A (Agent-to-Agent) protocol support.

## Overview

KAgent.Core provides the foundational infrastructure for building agents that integrate with the KAgent backend and communicate using the A2A protocol. This is the .NET implementation of [kagent-core](https://github.com/kagent-dev/kagent/tree/main/python/packages/kagent-core).

## Key Components

### Configuration

**KAgentConfig** - Central configuration management for KAgent applications.

```csharp
using KAgent.Core;

// Reads from environment variables: KAGENT_URL, KAGENT_NAME, KAGENT_NAMESPACE
var config = new KAgentConfig();

// Or override with explicit values
var config = new KAgentConfig(
    url: "http://localhost:3000",
    name: "my-agent",
    @namespace: "demo"
);

```

**Environment Variables:**
- `KAGENT_URL` - KAgent backend URL (required)
- `KAGENT_NAME` - Application name (required)
- `KAGENT_NAMESPACE` - Application namespace (required)
- `A2A_MAX_CONTENT_LENGTH` - Maximum A2A payload size in bytes (optional, default: 10MB)

### A2A Components

Located in `core/A2A/`:

#### TaskStore

**KAgentTaskStore** - Persists A2A tasks to KAgent backend via REST API.

```csharp
using KAgent.Core.A2A;

var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:3000") };
ITaskStore taskStore = new KAgentTaskStore(httpClient);

// Create and save a task
var task = new A2ATask
{
    Id = "task-123",
    Kind = "task",
    ContextId = "session-456",
    Status = new A2ATaskStatus
    {
        State = "working",
        Timestamp = DateTimeOffset.UtcNow.ToString("O")
    },
    Metadata = new Dictionary<string, object>
    {
        ["kagent_app_name"] = "demo__NS__my_agent",
        ["kagent_user_id"] = "user123"
    }
};

await taskStore.SaveAsync(task);

// Retrieve a task
var retrievedTask = await taskStore.GetAsync("task-123");
```

**Key Types:**
- `A2ATask` - Represents a task with history, artifacts, status, and metadata
- `A2ATaskStatus` - Task state and timestamp
- `ITaskStore` - Interface for task persistence implementations

#### RequestContextBuilder

**KAgentRequestContextBuilder** - Extracts user context from HTTP headers.

```csharp
using KAgent.Core.A2A;

var builder = new KAgentRequestContextBuilder();

var headers = new Dictionary<string, string>
{
    ["x-user-id"] = "user123"
};

var context = await builder.BuildAsync(
    taskId: "task-123",
    contextId: "session-456",
    headers: headers
);

Console.WriteLine(context.User?.UserId); // user123
```

**Supported Headers:**
- `x-user-id` - User identification

#### TaskResultAggregator

**TaskResultAggregator** - Aggregates task state and events across execution.

```csharp
using KAgent.Core.A2A;

var aggregator = new TaskResultAggregator();

// Process events to track task state
aggregator.ProcessEvent(statusUpdateEvent);
aggregator.ProcessEvent(artifactUpdateEvent);

// Get aggregated state
var currentState = aggregator.TaskState; // TaskState enum
var statusMessage = aggregator.TaskStatusMessage; // AgentMessage or null
```

### Tracing

**TracingConfiguration** - OpenTelemetry tracing setup with OTLP exporter using the recommended ASP.NET Core hosting pattern.

```csharp
using KAgent.Core.Tracing;
using Microsoft.Extensions.DependencyInjection;

// Set environment variables to enable tracing
Environment.SetEnvironmentVariable("OTEL_TRACING_ENABLED", "true");
Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317");

// Configure services during application builder setup
var builder = WebApplication.CreateBuilder();
TracingConfiguration.ConfigureServices(builder.Services, "my-agent-app");
```

**Environment Variables:**
- `OTEL_TRACING_ENABLED` - Set to "true" to enable tracing
- `OTEL_TRACING_EXPORTER_OTLP_ENDPOINT` - OTLP endpoint for traces (primary)
- `OTEL_EXPORTER_OTLP_ENDPOINT` - Fallback OTLP endpoint (default: http://localhost:4317)

## Architecture

```
┌─────────────────────────────────────────┐
│  Your Application                       │
│  (e.g., KAgent.MAF)                     │
└──────────────┬──────────────────────────┘
               │
               v
┌─────────────────────────────────────────┐
│  KAgent.Core                            │
│                                         │
│  ┌────────────────────────────────────┐ │
│  │ KAgentConfig                       │ │
│  │ - Environment variable reading     │ │
│  │ - App name formatting              │ │
│  └────────────────────────────────────┘ │
│                                         │
│  ┌────────────────────────────────────┐ │
│  │ A2A Components                     │ │
│  │                                    │ │
│  │  • KAgentTaskStore                 │ │
│  │    - Task persistence              │ │
│  │    - HTTP API integration          │ │
│  │                                    │ │
│  │  • RequestContextBuilder           │ │
│  │    - User context extraction       │ │
│  │    - Header parsing                │ │
│  │                                    │ │
│  │  • TaskResultAggregator            │ │
│  │    - Event processing              │ │
│  │    - State tracking                │ │
│  └────────────────────────────────────┘ │
│                                         │
│  ┌────────────────────────────────────┐ │
│  │ Tracing                            │ │
│  │ - OpenTelemetry setup              │ │
│  │ - A2A protocol tracing             │ │
│  │ - Activity tracking                │ │
│  └────────────────────────────────────┘ │
└──────────────┬──────────────────────────┘
               │
               v
┌─────────────────────────────────────────┐
│  KAgent Backend (Go)                    │
│  - POST /api/tasks                      │
│  - GET /api/tasks/{id}                  │
│  - DELETE /api/tasks/{id}               │
└─────────────────────────────────────────┘
```


## Usage Example

See [kagent-maf](../kagent-maf/src/README.md) for a complete integration example using Microsoft Agent Framework.
