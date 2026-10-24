using OpenGE.Common;
using OpenGE.Common.Lifecycle;
using OpenGE.Logging;
using OpenGE.Logging.Outputs;
using Xunit;

namespace Tests.Logging;

public class LoggerTests
{
    [Fact]
    public void Log_DispatchesEachMessageToOutputsInDescendingPriority()
    {
        var calls = new List<string>();
        var low = new RecordingOutput("low", LogOutputPriority.Low, calls);
        var critical = new RecordingOutput("critical", LogOutputPriority.Critical, calls);
        var normal = new RecordingOutput("normal", LogOutputPriority.Normal, calls);
        var logger = new Logger([low, critical, normal]);

        logger.Log(
            new LogMessage("first", "source", LogMessageKind.Debug),
            new LogMessage("second", "source", LogMessageKind.Warn)
        );

        Assert.Equal(
            new[]
            {
                "critical:first",
                "normal:first",
                "low:first",
                "critical:second",
                "normal:second",
                "low:second",
            },
            calls
        );
    }

    [Fact]
    public void Log_StringOverload_UsesDefaultSourceAndInformationKind()
    {
        var output = new CollectingOutput();
        var logger = new Logger([output]);

        logger.Log("started");

        Assert.Equal(
            new[] { new LogMessage("started", "", LogMessageKind.Information) },
            output.Messages
        );
    }

    [Fact]
    public void Log_StringOverload_ForwardsKindAndMetadata()
    {
        var output = new CollectingOutput();
        var logger = new Logger([output]);
        var metadata = new Dictionary<string, object?> { ["id"] = 7 };

        logger.Log("failed", "worker", LogMessageKind.Warn, metadata);

        Assert.Equal("failed", Assert.Single(output.Messages).Content);
        Assert.Equal("worker", output.Messages[0].Source);
        Assert.Equal(LogMessageKind.Warn, output.Messages[0].Kind);
        Assert.Same(metadata, output.Messages[0].Metadata);
    }

    [Fact]
    public void Log_CallsOnLogAfterDispatchingToOutputs()
    {
        var calls = new List<string>();
        var output = new RecordingOutput("output", LogOutputPriority.Normal, calls);
        var logger = new ObservingLogger(calls, output);

        logger.Log("message");

        Assert.Equal(new[] { "output:message", "logger:message" }, calls);
        Assert.Equal("message", Assert.Single(logger.ObservedMessages).Content);
    }

    [Fact]
    public void From_CreatesLoggerWithFixedSource()
    {
        var output = new CollectingOutput();
        var logger = new Logger([output]);
        var sourcedLogger = logger.From("physics");
        var metadata = new Dictionary<string, object?> { ["step"] = 4 };

        sourcedLogger.Log("collision", LogMessageKind.Debug, metadata);

        var message = Assert.Single(output.Messages);
        Assert.Equal("physics", sourcedLogger.Source);
        Assert.Equal("collision", message.Content);
        Assert.Equal("physics", message.Source);
        Assert.Equal(LogMessageKind.Debug, message.Kind);
        Assert.Same(metadata, message.Metadata);
    }

    [Fact]
    public void Start_ComposesAndConfiguresOutputsOnce()
    {
        var calls = new List<string>();
        var output = new RecordingOutput("output", LogOutputPriority.Normal, calls);
        var logger = new ComposingLogger(output, calls);
        var engine = new TestEngine(logger);

        engine.Start();
        logger.Log("one");
        engine.Stop();
        engine.Start();
        logger.Log("two");
        engine.Stop();

        Assert.Equal(1, calls.Count(call => call == "compose"));
        Assert.Equal(1, calls.Count(call => call == "configure"));
        Assert.Equal(
            new[] { "output:one", "output:two" },
            calls.Where(call => call.StartsWith("output:"))
        );
    }

    [Fact]
    public void Destroy_DestroysOwnedOutputsAndRejectsFurtherLogging()
    {
        var output = new CollectingOutput();
        var logger = new Logger([output]);

        logger.Destroy();

        Assert.True(output.Destroyed);
        Assert.Throws<DestroyedObjectException>(() => logger.Log("after destruction"));
        Assert.Throws<DestroyedObjectException>(() => logger.From("source"));
    }

    private sealed class RecordingOutput(
        string name,
        LogOutputPriority priority,
        List<string> calls
    ) : LogOutput(priority)
    {
        protected override void OnLog(LogMessage message)
        {
            calls.Add($"{name}:{message.Content}");
        }
    }

    private sealed class CollectingOutput : LogOutput
    {
        public List<LogMessage> Messages { get; } = [];

        protected override void OnLog(LogMessage message)
        {
            Messages.Add(message);
        }
    }

    private sealed class ObservingLogger(List<string> calls, LogOutput output) : Logger([output])
    {
        public List<LogMessage> ObservedMessages { get; } = [];

        protected override void OnLog(LogMessage message)
        {
            calls.Add($"logger:{message.Content}");
            ObservedMessages.Add(message);
        }
    }

    private sealed class ComposingLogger(LogOutput output, List<string> calls) : Logger
    {
        protected override IEnumerable<LogOutput> Compose()
        {
            calls.Add("compose");
            yield return output;
        }

        protected override void Configure()
        {
            Assert.Contains(output, Outputs);
            calls.Add("configure");
        }
    }

    private sealed class TestEngine(params IEnumerable<Module> modules) : Engine(modules);
}
