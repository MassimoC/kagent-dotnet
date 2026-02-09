namespace KAgent.Core.A2A;

/// <summary>
/// Constants for A2A data part metadata and HITL (Human-in-the-Loop) support.
/// </summary>
public static class Constants
{
    // A2A Data Part Metadata Constants
    public const string A2A_DATA_PART_METADATA_TYPE_KEY = "type";
    public const string A2A_DATA_PART_METADATA_IS_LONG_RUNNING_KEY = "is_long_running";
    public const string A2A_DATA_PART_METADATA_TYPE_FUNCTION_CALL = "function_call";
    public const string A2A_DATA_PART_METADATA_TYPE_FUNCTION_RESPONSE = "function_response";
    public const string A2A_DATA_PART_METADATA_TYPE_CODE_EXECUTION_RESULT = "code_execution_result";
    public const string A2A_DATA_PART_METADATA_TYPE_EXECUTABLE_CODE = "executable_code";

    public const string KAGENT_METADATA_KEY_PREFIX = "kagent_";

    // Human-in-the-Loop (HITL) Constants
    public const string KAGENT_HITL_INTERRUPT_TYPE_TOOL_APPROVAL = "tool_approval";
    public const string KAGENT_HITL_DECISION_TYPE_KEY = "decision_type";
    public const string KAGENT_HITL_DECISION_TYPE_APPROVE = "approve";
    public const string KAGENT_HITL_DECISION_TYPE_DENY = "deny";
    public const string KAGENT_HITL_DECISION_TYPE_REJECT = "reject";

    public static readonly string[] KAGENT_HITL_RESUME_KEYWORDS_APPROVE = 
        ["approved", "approve", "proceed", "yes", "continue"];

    public static readonly string[] KAGENT_HITL_RESUME_KEYWORDS_DENY = 
        ["denied", "deny", "reject", "no", "cancel", "stop"];

    /// <summary>
    /// Gets the A2A event metadata key for the given key.
    /// </summary>
    /// <param name="key">The metadata key to prefix.</param>
    /// <returns>The prefixed metadata key.</returns>
    /// <exception cref="ArgumentException">Thrown when key is null or empty.</exception>
    public static string GetKagentMetadataKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Metadata key cannot be empty or null", nameof(key));
        }
        return $"{KAGENT_METADATA_KEY_PREFIX}{key}";
    }
}
