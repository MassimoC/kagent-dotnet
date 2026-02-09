using KAgent.Core.A2A;

namespace KAgent.Core.Tests.A2A;

/// <summary>
/// Tests for HITL utility functions.
/// </summary>
public class HitlUtilitiesTests
{
    [Fact]
    public void EscapeMarkdownBackticks_EscapesAllCases()
    {
        // Test backtick escaping for all cases
        Assert.Equal("foo\\`bar", HitlUtilities.EscapeMarkdownBackticks("foo`bar"));
        Assert.Equal("\\`code\\` and \\`more\\`", HitlUtilities.EscapeMarkdownBackticks("`code` and `more`"));
        Assert.Equal("plain text", HitlUtilities.EscapeMarkdownBackticks("plain text"));
        Assert.Equal("", HitlUtilities.EscapeMarkdownBackticks(""));
    }

    [Fact]
    public void ExtractDecisionFromText_DetectsApprovalKeywords()
    {
        // Approve keyword
        Assert.Equal(DecisionType.Approve, HitlUtilities.ExtractDecisionFromText("I have approved this action"));

        // Case insensitive
        Assert.Equal(DecisionType.Approve, HitlUtilities.ExtractDecisionFromText("APPROVED"));
    }

    [Fact]
    public void ExtractDecisionFromText_DetectsDenialKeywords()
    {
        // Deny keyword
        Assert.Equal(DecisionType.Deny, HitlUtilities.ExtractDecisionFromText("Request denied, do not proceed"));
    }

    [Fact]
    public void ExtractDecisionFromText_ReturnsNull_WhenNoKeywordsFound()
    {
        // No decision found
        Assert.Null(HitlUtilities.ExtractDecisionFromText("This is just a comment"));
    }

    [Fact]
    public void ExtractDecisionFromData_ExtractsApproveDecision()
    {
        // Approve
        var data = new Dictionary<string, object>
        {
            { Constants.KAGENT_HITL_DECISION_TYPE_KEY, Constants.KAGENT_HITL_DECISION_TYPE_APPROVE }
        };
        Assert.Equal(DecisionType.Approve, HitlUtilities.ExtractDecisionFromData(data));
    }

    [Fact]
    public void ExtractDecisionFromData_ExtractsDenyDecision()
    {
        // Deny
        var data = new Dictionary<string, object>
        {
            { Constants.KAGENT_HITL_DECISION_TYPE_KEY, Constants.KAGENT_HITL_DECISION_TYPE_DENY }
        };
        Assert.Equal(DecisionType.Deny, HitlUtilities.ExtractDecisionFromData(data));
    }

    [Fact]
    public void ExtractDecisionFromData_ReturnsNull_WhenKeyMissing()
    {
        // No decision_type key
        var data = new Dictionary<string, object>
        {
            { "other_key", "value" }
        };
        Assert.Null(HitlUtilities.ExtractDecisionFromData(data));
    }

    [Fact]
    public void ExtractDecisionFromData_ReturnsNull_WhenInvalidValue()
    {
        // Invalid decision value
        var data = new Dictionary<string, object>
        {
            { Constants.KAGENT_HITL_DECISION_TYPE_KEY, "invalid_decision" }
        };
        Assert.Null(HitlUtilities.ExtractDecisionFromData(data));
    }

    [Fact]
    public void FormatToolApprovalText_FormatsWithAllEdgeCases()
    {
        // Test formatting tool approval requests with all edge cases
        var requests = new List<ToolApprovalRequest>
        {
            new() { Name = "search", Args = new Dictionary<string, object> { { "query", "test" } } },
            new() { Name = "run`code`", Args = new Dictionary<string, object> { { "cmd", "echo `test`" } } },
            new() { Name = "reset", Args = new Dictionary<string, object>() }
        };

        var result = HitlUtilities.FormatToolApprovalText(requests);

        // Check structure and content
        Assert.Contains("Approval Required", result);
        Assert.Contains("search", result);
        Assert.Contains("reset", result);
        // Check backticks are escaped
        Assert.Contains("\\`", result);
    }
}
