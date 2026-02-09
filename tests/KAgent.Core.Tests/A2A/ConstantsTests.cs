using KAgent.Core.A2A;

namespace KAgent.Core.Tests.A2A;

/// <summary>
/// Tests for HITL constants.
/// </summary>
public class ConstantsTests
{
    [Fact]
    public void HitlConstants_AreDefinedWithExpectedValues()
    {
        // Test all HITL constants are defined with expected values
        
        // Interrupt types
        Assert.Equal("tool_approval", Constants.KAGENT_HITL_INTERRUPT_TYPE_TOOL_APPROVAL);

        // Decision types
        Assert.Equal("decision_type", Constants.KAGENT_HITL_DECISION_TYPE_KEY);
        Assert.Equal("approve", Constants.KAGENT_HITL_DECISION_TYPE_APPROVE);
        Assert.Equal("deny", Constants.KAGENT_HITL_DECISION_TYPE_DENY);
        Assert.Equal("reject", Constants.KAGENT_HITL_DECISION_TYPE_REJECT);

        // Resume keywords
        Assert.Contains("approved", Constants.KAGENT_HITL_RESUME_KEYWORDS_APPROVE);
        Assert.Contains("proceed", Constants.KAGENT_HITL_RESUME_KEYWORDS_APPROVE);
        Assert.Contains("denied", Constants.KAGENT_HITL_RESUME_KEYWORDS_DENY);
        Assert.Contains("cancel", Constants.KAGENT_HITL_RESUME_KEYWORDS_DENY);
    }
}
