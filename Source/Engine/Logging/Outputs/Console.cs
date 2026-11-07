namespace OpenGE.Logging.Outputs;

/// <summary>
/// Represents a console logging output that formats and writes
/// log messages to standard output using ANSI colors and timestamps.
/// </summary>
public sealed class ConsoleOutput() : LogOutput(LogOutputPriority.Critical)
{
    private const string AnsiCyan = "\e[36m";
    private const string AnsiGray = "\e[90m";
    private const string AnsiGreen = "\e[32m";
    private const string AnsiReset = "\e[0m";
    private const string AnsiYellow = "\e[33m";

    private static string GetColor(LogMessageKind kind)
    {
        return kind switch
        {
            LogMessageKind.Debug => AnsiCyan,
            LogMessageKind.Information => AnsiGreen,
            LogMessageKind.Warn => AnsiYellow,
            _ => AnsiGreen,
        };
    }

    private static string GetKindName(LogMessageKind kind)
    {
        return kind switch
        {
            LogMessageKind.Debug => "DEBUG",
            LogMessageKind.Information => "INFO",
            LogMessageKind.Warn => "WARN",
            _ => "INFO",
        };
    }

    /// <inheritdoc />
    protected override void OnLog(LogMessage message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var color = GetColor(message.Kind);

        var prefix =
            $"{AnsiGray}[{timestamp}]{AnsiReset} "
            + $"{color}[{GetKindName(message.Kind)}]{AnsiReset}";

        var source = string.IsNullOrWhiteSpace(message.Source) ? "Unknown" : message.Source;

        var sourceTag = $"{AnsiGray}[{source}]{AnsiReset}";
        var output = $"{prefix} {sourceTag} {message.Content}";

        Console.WriteLine(output);

        if (message.Metadata is not null)
            Console.WriteLine($"{AnsiGray}{message.Metadata}{AnsiReset}");
    }
}
