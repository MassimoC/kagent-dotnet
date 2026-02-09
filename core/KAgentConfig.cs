namespace KAgent.Core;

/// <summary>
/// Configuration for KAgent applications.
/// Reads from environment variables: KAGENT_URL, KAGENT_NAME, KAGENT_NAMESPACE
/// </summary>
public class KAgentConfig
{
    private readonly string _url;
    private readonly string _name;
    private readonly string _namespace;

    /// <summary>
    /// Initializes a new instance of KAgentConfig.
    /// </summary>
    /// <param name="url">Optional URL override. If null, reads from KAGENT_URL environment variable.</param>
    /// <param name="name">Optional name override. If null, reads from KAGENT_NAME environment variable.</param>
    /// <param name="namespace">Optional namespace override. If null, reads from KAGENT_NAMESPACE environment variable.</param>
    /// <exception cref="ArgumentException">Thrown when required environment variables are not set or values are invalid.</exception>
    public KAgentConfig(string? url = null, string? name = null, string? @namespace = null)
    {
        var kagentUrl = Environment.GetEnvironmentVariable("KAGENT_URL");
        var kagentName = Environment.GetEnvironmentVariable("KAGENT_NAME");
        var kagentNamespace = Environment.GetEnvironmentVariable("KAGENT_NAMESPACE");

        if (string.IsNullOrEmpty(kagentUrl) && string.IsNullOrEmpty(url))
        {
            throw new ArgumentException("KAGENT_URL environment variable is not set");
        }

        if (string.IsNullOrEmpty(kagentName) && string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("KAGENT_NAME environment variable is not set");
        }

        if (string.IsNullOrEmpty(kagentNamespace) && string.IsNullOrEmpty(@namespace))
        {
            throw new ArgumentException("KAGENT_NAMESPACE environment variable is not set");
        }

        var finalUrl = string.IsNullOrEmpty(url) ? kagentUrl! : url;
        var finalName = string.IsNullOrEmpty(name) ? kagentName! : name;
        var finalNamespace = string.IsNullOrEmpty(@namespace) ? kagentNamespace! : @namespace;

        // Validate URL format
        if (!Uri.TryCreate(finalUrl, UriKind.Absolute, out var validatedUri))
        {
            throw new ArgumentException($"Invalid URL format: {finalUrl}", nameof(url));
        }

        // Validate URL scheme (only http and https allowed)
        if (validatedUri.Scheme != Uri.UriSchemeHttp && validatedUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException($"URL must use HTTP or HTTPS scheme: {finalUrl}", nameof(url));
        }

        // Sanitize name and namespace - remove potentially dangerous characters
        // Allow alphanumeric, underscores, hyphens, and dots
        var sanitizedName = SanitizeIdentifier(finalName, nameof(name));
        var sanitizedNamespace = SanitizeIdentifier(finalNamespace, nameof(@namespace));

        _url = finalUrl;
        _name = sanitizedName;
        _namespace = sanitizedNamespace;
    }

    /// <summary>
    /// Sanitizes an identifier by removing potentially dangerous characters.
    /// Allows only alphanumeric characters, underscores, hyphens, and dots.
    /// </summary>
    private static string SanitizeIdentifier(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null, empty, or whitespace after sanitization.", paramName);
        }

        // Remove any characters that are not alphanumeric, underscore, hyphen, or dot
        var filteredValue = new string(value.Where(c => 
            char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.').ToArray());

        if (string.IsNullOrWhiteSpace(filteredValue))
        {
            throw new ArgumentException($"Value contains only invalid characters: {value}", paramName);
        }

        return filteredValue;
    }

    /// <summary>
    /// Gets the name with hyphens replaced by underscores.
    /// </summary>
    public string Name => _name.Replace("-", "_");

    /// <summary>
    /// Gets the namespace with hyphens replaced by underscores.
    /// </summary>
    public string Namespace => _namespace.Replace("-", "_");

    /// <summary>
    /// Gets the app name in the format: namespace__NS__name
    /// </summary>
    public string AppName => $"{Namespace}__NS__{Name}";

    /// <summary>
    /// Gets the URL.
    /// </summary>
    public string Url => _url;
}
