namespace KAgent.Core.A2A;

/// <summary>
/// Type for user decisions in HITL workflows.
/// </summary>
public enum DecisionType
{
    Approve,
    Deny,
    Reject
}

/// <summary>
/// Generic structure for a tool call requiring approval.
/// Any agent framework can map their tool calls to this structure.
/// </summary>
public class ToolApprovalRequest
{
    /// <summary>
    /// The name of the tool/function being called.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Dictionary of arguments to pass to the tool.
    /// </summary>
    public required Dictionary<string, object> Args { get; init; }

    /// <summary>
    /// Optional unique identifier for this specific tool call.
    /// </summary>
    public string? Id { get; init; }
}

/// <summary>
/// Human-in-the-Loop (HITL) support utilities for kagent executors.
/// This module provides types and utilities for implementing
/// human-in-the-loop workflows in kagent agent executors using A2A protocol primitives.
/// </summary>
public static class HitlUtilities
{
    /// <summary>
    /// Escape backticks in text to prevent markdown formatting issues.
    /// Used when displaying code, tool names, or arguments in markdown-formatted
    /// approval messages.
    /// </summary>
    /// <param name="text">Text that may contain backticks.</param>
    /// <returns>Text with all backticks escaped with backslash.</returns>
    public static string EscapeMarkdownBackticks(string text)
    {
        return text.Replace("`", "\\`");
    }

    /// <summary>
    /// Extract decision from text using keyword matching.
    /// Searches for approval or denial keywords in the text (case-insensitive).
    /// Denial keywords take priority if both are present (to avoid accidental approval).
    /// </summary>
    /// <param name="text">User input text.</param>
    /// <returns>Decision type if found, null otherwise.</returns>
    public static DecisionType? ExtractDecisionFromText(string text)
    {
        var textLower = text.ToLowerInvariant();

        // Check deny keywords first (safer - prevents accidental approval)
        if (Constants.KAGENT_HITL_RESUME_KEYWORDS_DENY.Any(keyword => textLower.Contains(keyword)))
        {
            return DecisionType.Deny;
        }

        // Check approve keywords
        if (Constants.KAGENT_HITL_RESUME_KEYWORDS_APPROVE.Any(keyword => textLower.Contains(keyword)))
        {
            return DecisionType.Approve;
        }

        return null;
    }

    /// <summary>
    /// Extract decision type from structured data dictionary.
    /// Looks for the decision_type key in the data dictionary and validates
    /// it's a known decision value.
    /// </summary>
    /// <param name="data">Data dictionary.</param>
    /// <returns>Decision type if found and valid, null otherwise.</returns>
    public static DecisionType? ExtractDecisionFromData(Dictionary<string, object> data)
    {
        if (data.TryGetValue(Constants.KAGENT_HITL_DECISION_TYPE_KEY, out var decisionObj) &&
            decisionObj is string decision)
        {
            return decision switch
            {
                Constants.KAGENT_HITL_DECISION_TYPE_APPROVE => DecisionType.Approve,
                Constants.KAGENT_HITL_DECISION_TYPE_DENY => DecisionType.Deny,
                Constants.KAGENT_HITL_DECISION_TYPE_REJECT => DecisionType.Reject,
                _ => null
            };
        }
        return null;
    }

    /// <summary>
    /// Format tool approval requests as human-readable markdown text.
    /// Creates a formatted approval message listing all tools and their arguments
    /// with proper markdown escaping to prevent rendering issues.
    /// </summary>
    /// <param name="actionRequests">List of tool approval request objects.</param>
    /// <returns>Formatted approval message string.</returns>
    public static string FormatToolApprovalText(IEnumerable<ToolApprovalRequest> actionRequests)
    {
        var parts = new List<string>
        {
            "**Approval Required**\n\n",
            "The following actions require your approval:\n\n"
        };

        foreach (var action in actionRequests)
        {
            var escapedToolName = EscapeMarkdownBackticks(action.Name);
            parts.Add($"**Tool**: `{escapedToolName}`\n");
            parts.Add("**Arguments**:\n");

            foreach (var (key, value) in action.Args)
            {
                var escapedKey = EscapeMarkdownBackticks(key);
                var escapedValue = EscapeMarkdownBackticks(value?.ToString() ?? "null");
                parts.Add($"  • {escapedKey}: `{escapedValue}`\n");
            }

            parts.Add("\n");
        }

        return string.Concat(parts);
    }
}
