using A2A;
using KAgent.Core.A2A;
using Xunit;

namespace KAgent.Core.Tests.A2A;

/// <summary>
/// Tests for TaskResultAggregator to verify Event Streaming implementation.
/// </summary>
public class TaskResultAggregatorTests
{
    [Fact]
    public void TaskResultAggregator_InitialState_IsWorking()
    {
        // Arrange & Act
        var aggregator = new TaskResultAggregator();

        // Assert
        Assert.Equal(TaskState.Working, aggregator.TaskState);
        Assert.Null(aggregator.TaskStatusMessage);
    }

    [Fact]
    public void ProcessEvent_WithWorkingStatus_UpdatesMessage()
    {
        // Arrange
        var aggregator = new TaskResultAggregator();
        var message = new AgentMessage
        {
            Role = MessageRole.Agent,
            Parts = new List<Part> { new TextPart { Text = "Processing..." } }
        };

        var statusEvent = new TaskStatusUpdateEvent
        {
            TaskId = "test-task",
            Status = new AgentTaskStatus
            {
                State = TaskState.Working,
                Message = message,
                Timestamp = DateTimeOffset.UtcNow
            },
            Final = false
        };

        // Act
        aggregator.ProcessEvent(statusEvent);

        // Assert
        Assert.Equal(TaskState.Working, aggregator.TaskState);
        Assert.Equal(message, aggregator.TaskStatusMessage);
    }

    [Fact]
    public void ProcessEvent_WithFailedStatus_UpdatesStateAndMessage()
    {
        // Arrange
        var aggregator = new TaskResultAggregator();
        var errorMessage = new AgentMessage
        {
            Role = MessageRole.Agent,
            Parts = new List<Part> { new TextPart { Text = "Error occurred" } }
        };

        var statusEvent = new TaskStatusUpdateEvent
        {
            TaskId = "test-task",
            Status = new AgentTaskStatus
            {
                State = TaskState.Failed,
                Message = errorMessage,
                Timestamp = DateTimeOffset.UtcNow
            },
            Final = true
        };

        // Act
        aggregator.ProcessEvent(statusEvent);

        // Assert
        Assert.Equal(TaskState.Failed, aggregator.TaskState);
        Assert.Equal(errorMessage, aggregator.TaskStatusMessage);
    }

    [Fact]
    public void ProcessEvent_FailedStateTakesPriorityOverWorking()
    {
        // Arrange
        var aggregator = new TaskResultAggregator();
        
        // First, process a working event
        var workingEvent = new TaskStatusUpdateEvent
        {
            TaskId = "test-task",
            Status = new AgentTaskStatus
            {
                State = TaskState.Working,
                Message = new AgentMessage
                {
                    Role = MessageRole.Agent,
                    Parts = new List<Part> { new TextPart { Text = "Working..." } }
                },
                Timestamp = DateTimeOffset.UtcNow
            },
            Final = false
        };
        aggregator.ProcessEvent(workingEvent);

        // Then process a failed event
        var failedMessage = new AgentMessage
        {
            Role = MessageRole.Agent,
            Parts = new List<Part> { new TextPart { Text = "Failed!" } }
        };
        var failedEvent = new TaskStatusUpdateEvent
        {
            TaskId = "test-task",
            Status = new AgentTaskStatus
            {
                State = TaskState.Failed,
                Message = failedMessage,
                Timestamp = DateTimeOffset.UtcNow
            },
            Final = true
        };
        aggregator.ProcessEvent(failedEvent);

        // Assert
        Assert.Equal(TaskState.Failed, aggregator.TaskState);
        Assert.Equal(failedMessage, aggregator.TaskStatusMessage);
    }

    [Fact]
    public void ProcessEvent_FailedStateCannotBeOverriddenByWorking()
    {
        // Arrange
        var aggregator = new TaskResultAggregator();
        
        // First, set to failed state
        var failedMessage = new AgentMessage
        {
            Role = MessageRole.Agent,
            Parts = new List<Part> { new TextPart { Text = "Failed!" } }
        };
        var failedEvent = new TaskStatusUpdateEvent
        {
            TaskId = "test-task",
            Status = new AgentTaskStatus
            {
                State = TaskState.Failed,
                Message = failedMessage,
                Timestamp = DateTimeOffset.UtcNow
            },
            Final = true
        };
        aggregator.ProcessEvent(failedEvent);

        // Try to process a working event after failure
        var workingEvent = new TaskStatusUpdateEvent
        {
            TaskId = "test-task",
            Status = new AgentTaskStatus
            {
                State = TaskState.Working,
                Message = new AgentMessage
                {
                    Role = MessageRole.Agent,
                    Parts = new List<Part> { new TextPart { Text = "Still working?" } }
                },
                Timestamp = DateTimeOffset.UtcNow
            },
            Final = false
        };
        aggregator.ProcessEvent(workingEvent);

        // Assert - state should remain Failed
        Assert.Equal(TaskState.Failed, aggregator.TaskState);
        Assert.Equal(failedMessage, aggregator.TaskStatusMessage);
    }

    [Fact]
    public void ProcessEvent_AuthRequiredTakesPriorityOverWorking()
    {
        // Arrange
        var aggregator = new TaskResultAggregator();
        
        // First, process working event
        aggregator.ProcessEvent(new TaskStatusUpdateEvent
        {
            TaskId = "test-task",
            Status = new AgentTaskStatus
            {
                State = TaskState.Working,
                Timestamp = DateTimeOffset.UtcNow
            },
            Final = false
        });

        // Then process auth required event
        var authMessage = new AgentMessage
        {
            Role = MessageRole.Agent,
            Parts = new List<Part> { new TextPart { Text = "Authentication needed" } }
        };
        var authEvent = new TaskStatusUpdateEvent
        {
            TaskId = "test-task",
            Status = new AgentTaskStatus
            {
                State = TaskState.AuthRequired,
                Message = authMessage,
                Timestamp = DateTimeOffset.UtcNow
            },
            Final = false
        };
        aggregator.ProcessEvent(authEvent);

        // Assert
        Assert.Equal(TaskState.AuthRequired, aggregator.TaskState);
        Assert.Equal(authMessage, aggregator.TaskStatusMessage);
    }

    [Fact]
    public void ProcessEvent_InputRequiredTakesPriorityOverWorking()
    {
        // Arrange
        var aggregator = new TaskResultAggregator();
        
        var inputMessage = new AgentMessage
        {
            Role = MessageRole.Agent,
            Parts = new List<Part> { new TextPart { Text = "User input needed" } }
        };
        var inputEvent = new TaskStatusUpdateEvent
        {
            TaskId = "test-task",
            Status = new AgentTaskStatus
            {
                State = TaskState.InputRequired,
                Message = inputMessage,
                Timestamp = DateTimeOffset.UtcNow
            },
            Final = false
        };

        // Act
        aggregator.ProcessEvent(inputEvent);

        // Assert
        Assert.Equal(TaskState.InputRequired, aggregator.TaskState);
        Assert.Equal(inputMessage, aggregator.TaskStatusMessage);
    }

    [Fact]
    public void ProcessEvent_StatePriorityHierarchy()
    {
        // Arrange
        var aggregator = new TaskResultAggregator();

        // Process in order of lowest to highest priority
        // Working -> InputRequired -> AuthRequired -> Failed

        // 1. Working
        aggregator.ProcessEvent(new TaskStatusUpdateEvent
        {
            TaskId = "test-task",
            Status = new AgentTaskStatus { State = TaskState.Working, Timestamp = DateTimeOffset.UtcNow },
            Final = false
        });
        Assert.Equal(TaskState.Working, aggregator.TaskState);

        // 2. InputRequired (should override Working)
        aggregator.ProcessEvent(new TaskStatusUpdateEvent
        {
            TaskId = "test-task",
            Status = new AgentTaskStatus { State = TaskState.InputRequired, Timestamp = DateTimeOffset.UtcNow },
            Final = false
        });
        Assert.Equal(TaskState.InputRequired, aggregator.TaskState);

        // 3. AuthRequired (should override InputRequired)
        aggregator.ProcessEvent(new TaskStatusUpdateEvent
        {
            TaskId = "test-task",
            Status = new AgentTaskStatus { State = TaskState.AuthRequired, Timestamp = DateTimeOffset.UtcNow },
            Final = false
        });
        Assert.Equal(TaskState.AuthRequired, aggregator.TaskState);

        // 4. Failed (should override AuthRequired)
        aggregator.ProcessEvent(new TaskStatusUpdateEvent
        {
            TaskId = "test-task",
            Status = new AgentTaskStatus { State = TaskState.Failed, Timestamp = DateTimeOffset.UtcNow },
            Final = true
        });
        Assert.Equal(TaskState.Failed, aggregator.TaskState);
    }

    [Fact]
    public void ProcessEvent_IgnoresNonTaskStatusUpdateEvents()
    {
        // Arrange
        var aggregator = new TaskResultAggregator();
        
        // Process a TaskArtifactUpdateEvent (not a TaskStatusUpdateEvent)
        var artifactEvent = new TaskArtifactUpdateEvent
        {
            TaskId = "test-task",
            Artifact = new Artifact
            {
                Parts = new List<Part> { new TextPart { Text = "Some result" } }
            }
        };

        // Act
        aggregator.ProcessEvent(artifactEvent);

        // Assert - state should remain in initial state
        Assert.Equal(TaskState.Working, aggregator.TaskState);
        Assert.Null(aggregator.TaskStatusMessage);
    }
}
