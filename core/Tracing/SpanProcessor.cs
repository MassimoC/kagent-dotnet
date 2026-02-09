using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace KAgent.Core.Tracing;

/// <summary>
/// Custom activity processor to add kagent attributes to all activities (spans).
/// Note: This is a simplified version. Full implementation would integrate with
/// OpenTelemetry SDK when available for .NET.
/// </summary>
public class KagentAttributesActivityProcessor
{
    private static readonly AsyncLocal<Dictionary<string, object>?> _kagentAttributes = new();

    /// <summary>
    /// Set kagent span attributes in the current context.
    /// </summary>
    /// <param name="attributes">Dictionary of kagent span attributes to store in context.</param>
    public static void SetKagentSpanAttributes(Dictionary<string, object> attributes)
    {
        _kagentAttributes.Value = attributes;
    }

    /// <summary>
    /// Clear kagent span attributes from the current context.
    /// </summary>
    public static void ClearKagentSpanAttributes()
    {
        _kagentAttributes.Value = null;
    }

    /// <summary>
    /// Add kagent attributes to an activity if present in context.
    /// </summary>
    /// <param name="activity">The activity to add attributes to.</param>
    public static void OnActivityStarted(Activity activity)
    {
        var attributes = _kagentAttributes.Value;
        if (attributes != null)
        {
            foreach (var (key, value) in attributes)
            {
                if (value != null)
                {
                    activity.SetTag(key, value);
                }
            }
        }
    }
}
