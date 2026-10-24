using OpenGE.Logging;
using Xunit;

namespace Tests.Logging;

public class MessageTests
{
    [Fact]
    public void Constructor_PreservesProvidedValues()
    {
        var metadata = new Dictionary<string, object?> { ["attempt"] = 3 };

        var message = new LogMessage("failed", "loader", LogMessageKind.Warn, metadata);

        Assert.Equal("failed", message.Content);
        Assert.Equal("loader", message.Source);
        Assert.Equal(LogMessageKind.Warn, message.Kind);
        Assert.Same(metadata, message.Metadata);
    }

    [Fact]
    public void Constructor_WithoutMetadata_UsesNullMetadata()
    {
        var message = new LogMessage("ready", "engine", LogMessageKind.Information);

        Assert.Null(message.Metadata);
    }

    [Fact]
    public void RecordEquality_UsesAllMessageValues()
    {
        var first = new LogMessage("ready", "engine", LogMessageKind.Information);
        var equal = new LogMessage("ready", "engine", LogMessageKind.Information);
        var different = new LogMessage("ready", "engine", LogMessageKind.Debug);

        Assert.Equal(first, equal);
        Assert.NotEqual(first, different);
    }
}
