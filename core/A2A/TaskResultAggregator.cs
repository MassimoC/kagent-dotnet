using A2A;
using TaskState = A2A.TaskState;
using TaskStatusUpdateEvent = A2A.TaskStatusUpdateEvent;
using A2AEvent = A2A.A2AEvent;
using AgentMessage = A2A.AgentMessage;

namespace KAgent.Core.A2A;

/// <summary>
/// Aggregates the task status updates from streaming events and provides the final task state.
/// This class processes events during agent execution to track the overall task status,
/// implementing priority-based state management to ensure critical states (like failures)
/// are not overwritten by less important ones.
/// 
/// Event Streaming implementation to aggregate events and determine
/// the final task state that should be reported back to the A2A server.
/// </summary>
public class TaskResultAggregator
{
    private TaskState _taskState = TaskState.Working;
    private AgentMessage? _taskStatusMessage;

    /// <summary>
    /// Process an event from the agent run and detect signals about the task status.
    /// Only processes non-final TaskStatusUpdateEvents to aggregate intermediate states.
    /// 
    /// Priority of task state (highest to lowest):
    /// - Failed (terminal - cannot be overridden)
    /// - AuthRequired (requires authentication)
    /// - InputRequired (requires user input)
    /// - Working (default state)
    /// </summary>
    /// <param name="event">The A2A event to process.</param>
    public void ProcessEvent(A2AEvent @event)
    {
        // Only process TaskStatusUpdateEvent types for state aggregation
        if (@event is TaskStatusUpdateEvent statusEvent)
        {
            // Extract the status from the event
            var status = statusEvent.Status;

            // Process based on state priority
            if (status.State == TaskState.Failed)
            {
                // Failed is the highest priority - always update
                _taskState = TaskState.Failed;
                _taskStatusMessage = status.Message;
            }
            else if (status.State == TaskState.AuthRequired && _taskState != TaskState.Failed)
            {
                // AuthRequired is second priority - update if not already failed
                _taskState = TaskState.AuthRequired;
                _taskStatusMessage = status.Message;
            }
            else if (status.State == TaskState.InputRequired && 
                     _taskState != TaskState.Failed && 
                     _taskState != TaskState.AuthRequired)
            {
                // InputRequired is third priority
                _taskState = TaskState.InputRequired;
                _taskStatusMessage = status.Message;
            }
            // For Working or Completed states, only update message if we're still in Working state
            // This preserves higher-priority states while still tracking progress messages
            else if (_taskState == TaskState.Working)
            {
                // Keep the current state (Working) but update message if provided
                if (status.Message != null)
                {
                    _taskStatusMessage = status.Message;
                }
            }
        }
    }

    /// <summary>
    /// Gets the current aggregated task state.
    /// This represents the highest-priority state encountered across all processed events.
    /// </summary>
    public TaskState TaskState => _taskState;

    /// <summary>
    /// Gets the task status message associated with the current state.
    /// This is typically the message from the last status update that changed the state.
    /// </summary>
    public AgentMessage? TaskStatusMessage => _taskStatusMessage;
}
