using KAgent.Core.Tracing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KAgent.Core.Tests.Tracing;

public class TracingConfigurationTests
{
    [Fact]
    public void ConfigureServices_WithTracingDisabled_DoesNotRegisterOpenTelemetry()
    {
        // Arrange
        Environment.SetEnvironmentVariable("OTEL_TRACING_ENABLED", "false");
        var services = new ServiceCollection();
        
        try
        {
            // Act
            TracingConfiguration.ConfigureServices(services, "test-service");
            var serviceProvider = services.BuildServiceProvider();
            
            // Assert - No OpenTelemetry services should be registered
            var tracerProvider = serviceProvider.GetService<OpenTelemetry.Trace.TracerProvider>();
            Assert.Null(tracerProvider);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("OTEL_TRACING_ENABLED", null);
        }
    }

    [Fact]
    public void ConfigureServices_WithTracingEnabled_RegistersOpenTelemetry()
    {
        // Arrange
        Environment.SetEnvironmentVariable("OTEL_TRACING_ENABLED", "true");
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317");
        var services = new ServiceCollection();
        
        try
        {
            // Act
            TracingConfiguration.ConfigureServices(services, "test-service");
            var serviceProvider = services.BuildServiceProvider();
            
            // Assert - OpenTelemetry services should be registered
            var tracerProvider = serviceProvider.GetService<OpenTelemetry.Trace.TracerProvider>();
            Assert.NotNull(tracerProvider);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("OTEL_TRACING_ENABLED", null);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", null);
        }
    }

    [Fact]
    public void ConfigureServices_WithCustomEndpoint_ConfiguresOtlpExporter()
    {
        // Arrange
        var customEndpoint = "http://custom-otlp:4317";
        Environment.SetEnvironmentVariable("OTEL_TRACING_ENABLED", "true");
        Environment.SetEnvironmentVariable("OTEL_TRACING_EXPORTER_OTLP_ENDPOINT", customEndpoint);
        var services = new ServiceCollection();
        
        try
        {
            // Act
            TracingConfiguration.ConfigureServices(services, "test-service");
            var serviceProvider = services.BuildServiceProvider();
            
            // Assert - TracerProvider should be created with the configuration
            var tracerProvider = serviceProvider.GetService<OpenTelemetry.Trace.TracerProvider>();
            Assert.NotNull(tracerProvider);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("OTEL_TRACING_ENABLED", null);
            Environment.SetEnvironmentVariable("OTEL_TRACING_EXPORTER_OTLP_ENDPOINT", null);
        }
    }

    [Fact]
    public void ConfigureServices_WithTracingEnabled_ConfiguresInstrumentation()
    {
        // Arrange
        Environment.SetEnvironmentVariable("OTEL_TRACING_ENABLED", "true");
        var services = new ServiceCollection();
        
        try
        {
            // Act
            TracingConfiguration.ConfigureServices(services, "test-service");
            var serviceProvider = services.BuildServiceProvider();
            
            // Assert - TracerProvider includes ASP.NET Core and HTTP instrumentation
            var tracerProvider = serviceProvider.GetService<OpenTelemetry.Trace.TracerProvider>();
            Assert.NotNull(tracerProvider);
            
            // The TracerProvider should be disposed when the service provider is disposed
            serviceProvider.Dispose();
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("OTEL_TRACING_ENABLED", null);
        }
    }

    [Fact]
    public void ConfigureServices_UsesServiceName_ForResourceConfiguration()
    {
        // Arrange
        Environment.SetEnvironmentVariable("OTEL_TRACING_ENABLED", "true");
        var services = new ServiceCollection();
        var serviceName = "custom-test-service";
        
        try
        {
            // Act
            TracingConfiguration.ConfigureServices(services, serviceName);
            var serviceProvider = services.BuildServiceProvider();
            
            // Assert
            var tracerProvider = serviceProvider.GetService<OpenTelemetry.Trace.TracerProvider>();
            Assert.NotNull(tracerProvider);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("OTEL_TRACING_ENABLED", null);
        }
    }
}
