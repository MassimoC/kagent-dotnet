using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace KAgent.Core.A2A;

/// <summary>
/// Provides centralized JSON serialization options for KAgent.
/// All KAgent components should use these options for consistent JSON serialization.
/// </summary>
public static class KAgentJsonSerializerOptions
{
    /// <summary>
    /// Gets the default JSON serialization options used by KAgent.
    /// Uses camel case naming policy and non-indented formatting.
    /// </summary>
    public static JsonSerializerOptions Default { get; }

    static KAgentJsonSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
        options.MakeReadOnly();
        Default = options;
    }
}
