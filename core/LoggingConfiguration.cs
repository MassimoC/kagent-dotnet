using Microsoft.Extensions.Logging;

namespace KAgent.Core;

/// <summary>
/// Logging configuration for KAgent applications.
/// </summary>
public static class LoggingConfiguration
{
    private static bool _loggingConfigured = false;

    /// <summary>
    /// Configures logging based on LOG_LEVEL environment variable.
    /// Defaults to Information level if not specified.
    /// This method logs the configuration using the provided logger.
    /// </summary>
    /// <param name="logger">Optional logger to use for logging configuration messages.</param>
    public static void ConfigureLogging(ILogger? logger = null)
    {
        var logLevelStr = Environment.GetEnvironmentVariable("LOG_LEVEL") ?? "INFO";

        if (!_loggingConfigured)
        {
            logger?.LogInformation("Logging configured with level: {LogLevel}", logLevelStr);
            _loggingConfigured = true;
        }
    }

    /// <summary>
    /// Gets the log level from the LOG_LEVEL environment variable.
    /// Defaults to Information level if not specified.
    /// </summary>
    public static LogLevel GetLogLevel()
    {
        var logLevelStr = Environment.GetEnvironmentVariable("LOG_LEVEL") ?? "INFO";
        return ParseLogLevel(logLevelStr.ToUpperInvariant());
    }

    private static LogLevel ParseLogLevel(string logLevelStr)
    {
        return logLevelStr switch
        {
            "DEBUG" => LogLevel.Debug,
            "INFO" => LogLevel.Information,
            "WARNING" => LogLevel.Warning,
            "ERROR" => LogLevel.Error,
            "CRITICAL" => LogLevel.Critical,
            _ => LogLevel.Information
        };
    }
}
