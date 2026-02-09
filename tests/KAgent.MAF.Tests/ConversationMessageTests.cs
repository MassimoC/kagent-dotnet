using A2A;
using KAgent.MAF.ConversationHistory;
using Xunit;

namespace KAgent.MAF.Tests.ConversationHistory;

public class ConversationMessageTests
{
    [Fact]
    public void ConversationMessage_CreatesSuccessfully()
    {
        // Arrange & Act
        var message = new ConversationMessage
        {
            Id = Guid.NewGuid().ToString(),
            Role = MessageRole.User,
            Parts = new List<Part> { new TextPart { Text = "Hello" } },
            Timestamp = DateTimeOffset.UtcNow
        };

        // Assert
        Assert.NotNull(message);
        Assert.NotNull(message.Id);
        Assert.Equal(MessageRole.User, message.Role);
        Assert.Single(message.Parts);
    }

    [Fact]
    public void ConversationMessage_SupportsMetadata()
    {
        // Arrange & Act
        var metadata = new Dictionary<string, object>
        {
            ["custom_key"] = "custom_value"
        };

        var message = new ConversationMessage
        {
            Id = Guid.NewGuid().ToString(),
            Role = MessageRole.Agent,
            Parts = new List<Part> { new TextPart { Text = "Response" } },
            Timestamp = DateTimeOffset.UtcNow,
            Metadata = metadata
        };

        // Assert
        Assert.NotNull(message.Metadata);
        Assert.Equal("custom_value", message.Metadata["custom_key"]);
    }
}
