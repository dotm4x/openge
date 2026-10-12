using Mirage.Common;
using Mirage.Common.Collections;
using Mirage.Common.Lifecycle;
using Mirage.Common.Primitives;
using Mirage.Logging.Outputs;

namespace Mirage.Logging;

/// <summary>
/// Manages logging and dispatches log messages across registered outputs.
/// </summary>
/// <remarks>The logger owns and destroys its registered outputs.</remarks>
public class Logger : Module
{
    private readonly Preparator _preparator = new();

    /// <summary>
    /// Gets the outputs registered with the logger.
    /// </summary>
    /// <remarks>
    /// The logger owns and destroys its registered outputs.
    /// Registration transfers ownership to this owner. Removing or clearing entries returns
    /// ownership to the caller without destroying them. Do not register an object owned elsewhere.
    /// </remarks>
    public readonly ReactiveSet<LogOutput> Outputs = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="Logger"/> class.
    /// </summary>
    /// <param name="outputs">
    /// The initial outputs, or <see langword="null"/> for none.
    /// </param>
    public Logger(IEnumerable<LogOutput>? outputs = null)
        : base("Logger")
    {
        foreach (var output in outputs ?? [])
        {
            Outputs.Add(output);
        }
    }

    private void ComposeContents()
    {
        foreach (var output in Compose())
        {
            try
            {
                Outputs.Add(output);
            }
            catch
            {
                if (!Outputs.Contains(output) && !output.Destroyed)
                    output.Destroy();

                throw;
            }
        }
    }

    private void ConfigureContents()
    {
        Configure();
    }

    /// <summary>
    /// Composes the outputs managed by this logger.
    /// </summary>
    /// <returns>The outputs to register, in enumeration order.</returns>
    /// <remarks>
    /// Composition occurs once when the logger first starts.
    /// Constructor-provided outputs are registered before composed outputs.
    /// All composed outputs are registered before configuration occurs.
    /// The logger owns and destroys its registered outputs.
    /// If composition fails, later lifecycle calls reject further initialization.
    /// </remarks>
    protected virtual IEnumerable<LogOutput> Compose()
    {
        yield break;
    }

    /// <summary>
    /// Configures relationships and behavior after composition, before startup or first use.
    /// </summary>
    /// <remarks>
    /// All constructor-provided and composed outputs are available here.
    /// This hook is invoked at most once, including across later lifecycle cycles.
    /// If configuration throws, later lifecycle calls reject further initialization
    /// rather than repeating configuration side effects.
    /// </remarks>
    protected virtual void Configure() { }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        base.OnDestroy();

        foreach (var output in Outputs.ToArray())
        {
            output.Destroy();
        }
        Outputs.Destroy();
    }

    /// <summary>
    /// Called after a message has been dispatched to all registered outputs.
    /// </summary>
    /// <param name="message">The message that was logged.</param>
    protected virtual void OnLog(LogMessage message) { }

    /// <inheritdoc />
    protected override void OnStart()
    {
        _preparator.Ensure(ComposeContents, ConfigureContents);

        base.OnStart();
    }

    /// <summary>
    /// Creates a logger associated with the specified source.
    /// </summary>
    /// <param name="source">The source to associate with logged messages.</param>
    /// <returns>A logger that automatically uses the specified source.</returns>
    public SourcedLogger From(string source)
    {
        ThrowIfDestroyed();
        return new SourcedLogger(this, source);
    }

    /// <summary>
    /// Logs a message using the specified content and optional message information.
    /// </summary>
    /// <param name="content">The content of the message.</param>
    /// <param name="source">
    /// The optional origin component, service, or module of the message.
    /// </param>
    /// <param name="kind">The severity or classification of the message.</param>
    /// <param name="metadata">
    /// Optional contextual key-value metadata associated with the message.
    /// </param>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the logger has already been destroyed.
    /// </exception>
    public void Log(
        string content,
        string source = "",
        LogMessageKind kind = LogMessageKind.Information,
        IReadOnlyDictionary<string, object?>? metadata = null
    )
    {
        Log(new LogMessage(content, source, kind, metadata));
    }

    /// <summary>
    /// Logs one or more messages to all registered outputs in descending
    /// order of their priority.
    /// </summary>
    /// <param name="messages">The messages to log.</param>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the logger has already been destroyed.
    /// </exception>
    public void Log(params LogMessage[] messages)
    {
        ThrowIfDestroyed();

        LogOutput[] sortedOutputs = [.. Outputs.OrderByDescending(output => output.Priority)];

        foreach (var message in messages)
        {
            foreach (var output in sortedOutputs)
            {
                output.Log(message);
            }

            OnLog(message);
        }
    }
}
