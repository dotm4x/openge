using OpenGE.Logging;
using OpenGE.Logging.Outputs;
using Xunit;

namespace Tests.Logging;

public class ConsoleOutputTests
{
    [Theory]
    [InlineData(LogMessageKind.Debug, "[DEBUG]")]
    [InlineData(LogMessageKind.Information, "[INFO]")]
    [InlineData(LogMessageKind.Warn, "[WARN]")]
    public void Log_FormatsKindSourceAndContent(LogMessageKind kind, string kindLabel)
    {
        var output = new ConsoleOutput();
        var originalOutput = Console.Out;
        using var writer = new StringWriter();

        try
        {
            Console.SetOut(writer);

            var logger = new OpenGE.Logging.Logger([output]);
            logger.Log("message body", "renderer", kind);

            var written = writer.ToString();
            Assert.Contains(kindLabel, written);
            Assert.Contains("[renderer]", written);
            Assert.Contains("message body", written);
            Assert.Contains("\u001b[", written);
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Log_WithoutUsableSource_FormatsUnknownSource(string source)
    {
        var output = new ConsoleOutput();
        var originalOutput = Console.Out;
        using var writer = new StringWriter();

        try
        {
            Console.SetOut(writer);
            var logger = new OpenGE.Logging.Logger([output]);

            logger.Log("message", source);

            Assert.Contains("[Unknown]", writer.ToString());
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    [Fact]
    public void Constructor_UsesCriticalPriority()
    {
        var output = new ConsoleOutput();

        Assert.Equal(LogOutputPriority.Critical, output.Priority);
    }
}
