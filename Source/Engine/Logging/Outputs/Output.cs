using OpenGE.Common.Lifecycle;

namespace OpenGE.Logging.Outputs;

/// <summary>
/// Defines the processing priority levels for log outputs.
/// Higher values indicate higher priority during message distribution.
/// </summary>
public enum LogOutputPriority
{
    /// <summary>
    /// Represents the lowest priority for a log output.
    /// </summary>
    Low = 25,

    /// <summary>
    /// Represents the default priority for a log output.
    /// </summary>
    Normal = 50,

    /// <summary>
    /// Represents a high priority for a log output.
    /// </summary>
    High = 75,

    /// <summary>
    /// Represents the highest priority for a log output.
    /// </summary>
    Critical = 100,
}

/// <summary>
/// Represents an output responsible for processing or persisting log messages.
/// </summary>
public abstract class LogOutput(LogOutputPriority priority = LogOutputPriority.Normal) : Destroyable
{
    /// <summary>
    /// Gets the priority assigned to the output, which determines its
    /// processing order among registered outputs.
    /// </summary>
    public readonly LogOutputPriority Priority = priority;

    /// <summary>
    /// Processes an incoming log message.
    /// </summary>
    /// <param name="message">The message to process.</param>
    protected abstract void OnLog(LogMessage message);

    internal void Log(LogMessage message)
    {
        ThrowIfDestroyed();
        OnLog(message);
    }
}
