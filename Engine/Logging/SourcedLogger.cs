using Mirage.Common.Lifecycle;

namespace Mirage.Logging;

/// <summary>
/// Provides logging operations associated with a fixed source.
/// </summary>
/// <remarks>The underlying logger is borrowed; its lifetime belongs to its engine.</remarks>
/// <param name="logger">The logger used to dispatch messages.</param>
/// <param name="source">The source associated with logged messages.</param>
public class SourcedLogger(Logger logger, string source)
{
    /// <summary>
    /// Gets the source associated with this logger.
    /// </summary>
    public readonly string Source = source;

    /// <summary>
    /// Logs a message using this logger's source.
    /// </summary>
    /// <param name="content">The content of the message.</param>
    /// <param name="kind">The severity or classification of the message.</param>
    /// <param name="metadata">
    /// Optional contextual key-value metadata associated with the message.
    /// </param>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the underlying logger has already been destroyed.
    /// </exception>
    public void Log(
        string content,
        LogMessageKind kind = LogMessageKind.Information,
        IReadOnlyDictionary<string, object?>? metadata = null
    )
    {
        logger.Log(content, Source, kind, metadata);
    }
}
