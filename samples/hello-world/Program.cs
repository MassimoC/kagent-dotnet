using A2A;
using Azure.AI.OpenAI;
using Azure.Identity;
using KAgent.MAF;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.AI;
using System.ClientModel;
using System;
using OpenTelemetry;
using OpenTelemetry.Trace;

// Get Azure OpenAI configuration from environment variables
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") 
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") 
    ?? "gpt-4o-mini";

var apiKey = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_OPENAI_API_KEY")
      ?? "api-key-not-set";

var urls =
    Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
    ?? Environment.GetEnvironmentVariable("DOTNET_URLS")
    ?? "http://0.0.0.0:8080";

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
    Description = "Hello world always answer to your questions, with a bit of humor.",
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

// Build the KAgent application
var app = new KAgentApp(
    agent: agent,
    agentCard: agentCard,
    executorConfig: new MAFAgentExecutorConfig
    {
        EnableInMemoryEventQueue = enableInMemoryEventQueue
    },
    tracing: true
);

var webApp = app.Build();

Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Hello World MAF Agent - Azure OpenAI Integration        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine($"---> Agent started on {urls} <---" );
Console.WriteLine();
Console.WriteLine("Azure OpenAI Configuration:");
Console.WriteLine($"  Endpoint: {endpoint}");
Console.WriteLine($"  Deployment: {deploymentName}");
Console.WriteLine();
Console.WriteLine();
Console.WriteLine("Press Ctrl+C to stop");
Console.WriteLine();

await webApp.RunAsync();