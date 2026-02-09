using Microsoft.Extensions.Logging;

namespace KAgent.Core.A2A;

/// <summary>
/// Configuration utilities for a2a-sdk integration.
/// </summary>
public static class A2AConfig
{
    private const string A2A_MAX_CONTENT_LENGTH_ENV_VAR = "A2A_MAX_CONTENT_LENGTH";
    private const int DEFAULT_A2A_MAX_CONTENT_LENGTH = 10 * 1024 * 1024; // 10MB (a2a-sdk default)

    /// <summary>
    /// Get the a2a max content length from environment variable.
    /// 
    /// Returns the configured max content length to be passed to
    /// A2A application constructors.
    /// 
    /// Environment variable:
    ///     A2A_MAX_CONTENT_LENGTH: Maximum payload size in bytes.
    ///                             Default: 10485760 (10MB, a2a-sdk default)
    ///                             Example: 52428800 (50MB)
    ///                             Set to "0" or "none" for unlimited.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic messages.</param>
    /// <returns>The max content length in bytes, or null for unlimited/default.</returns>
    public static int? GetA2AMaxContentLength(ILogger? logger = null)
    {
        var maxContentLengthStr = Environment.GetEnvironmentVariable(A2A_MAX_CONTENT_LENGTH_ENV_VAR);
        
        if (string.IsNullOrEmpty(maxContentLengthStr))
        {
            // Return null to use the a2a-sdk default (10MB)
            return null;
        }

        // Handle special case for unlimited
        if (maxContentLengthStr.Equals("0", StringComparison.OrdinalIgnoreCase) ||
            maxContentLengthStr.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            maxContentLengthStr.Equals("unlimited", StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogInformation("Set a2a MAX_CONTENT_LENGTH to unlimited");
            return null;
        }

        if (int.TryParse(maxContentLengthStr, out var maxContentLength))
        {
            if (maxContentLength < 0)
            {
                logger?.LogWarning(
                    "Invalid {EnvVar} value: {Value} (must be non-negative), using default {Default}",
                    A2A_MAX_CONTENT_LENGTH_ENV_VAR, maxContentLengthStr, DEFAULT_A2A_MAX_CONTENT_LENGTH);
                return DEFAULT_A2A_MAX_CONTENT_LENGTH;
            }
            logger?.LogInformation("Set a2a MAX_CONTENT_LENGTH to {MaxContentLength} bytes", maxContentLength);
            return maxContentLength;
        }

        logger?.LogWarning(
            "Invalid {EnvVar} value: {Value}, using default {Default}",
            A2A_MAX_CONTENT_LENGTH_ENV_VAR, maxContentLengthStr, DEFAULT_A2A_MAX_CONTENT_LENGTH);
        return DEFAULT_A2A_MAX_CONTENT_LENGTH;
    }
}
