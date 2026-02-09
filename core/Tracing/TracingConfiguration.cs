using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace KAgent.Core.Tracing;

/// <summary>
/// Configuration for OpenTelemetry tracing and logging in KAgent applications.
/// Uses the recommended builder.Services.AddOpenTelemetry() pattern for ASP.NET Core.
/// </summary>
public static class TracingConfiguration
{
    /// <summary>
    /// Configures OpenTelemetry tracing using the hosting extensions pattern.
    /// This is the recommended approach for ASP.NET Core applications.
    /// Environment variables:
    ///   - OTEL_TRACING_ENABLED: Set to "true" to enable tracing
    ///   - OTEL_LOGGING_ENABLED: Set to "true" to enable logging
    ///   - OTEL_TRACING_EXPORTER_OTLP_ENDPOINT: OTLP endpoint for traces
    ///   - OTEL_LOGGING_EXPORTER_OTLP_ENDPOINT: OTLP endpoint for logs
    ///   - OTEL_EXPORTER_OTLP_ENDPOINT: Fallback OTLP endpoint
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="serviceName">Name of the service (defaults to "kagent").</param>
    public static void ConfigureServices(IServiceCollection services, string serviceName = "kagent")
    {
        var tracingEnabled = GetBooleanEnvironmentVariable("OTEL_TRACING_ENABLED", false);

        if (!tracingEnabled)
        {
            return;
        }

        // Check new env var first, fall back to old one for backward compatibility
        var traceEndpoint = Environment.GetEnvironmentVariable("OTEL_TRACING_EXPORTER_OTLP_ENDPOINT") ??
                           Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

        // Configure OpenTelemetry using the hosting extensions pattern
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder
                    .AddSource(serviceName)
                    .AddSource("A2A")  // A2A SDK tracing
                    .AddSource("A2A.AspNetCore")  // A2A ASP.NET Core integration tracing
                    .AddSource("System.Diagnostics.Activity")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                // Configure OTLP exporter if endpoint is provided
                if (!string.IsNullOrEmpty(traceEndpoint))
                {
                    tracerProviderBuilder.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(traceEndpoint);
                    });
                }
                else
                {
                    // Default OTLP endpoint
                    tracerProviderBuilder.AddOtlpExporter();
                }
            });
    }

    private static bool GetBooleanEnvironmentVariable(string variableName, bool defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrEmpty(value))
        {
            return defaultValue;
        }

        return value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }
}
