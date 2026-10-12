namespace Mirage.Common.Primitives;

/// <summary>
/// Represents a phase of one-time preparation.
/// </summary>
public enum PreparationState
{
    /// <summary>Composition has not started.</summary>
    Unprepared,

    /// <summary>Composition is executing.</summary>
    Composing,

    /// <summary>Composition completed; configuration has not started.</summary>
    Composed,

    /// <summary>Configuration is executing.</summary>
    Configuring,

    /// <summary>Composition and configuration completed successfully.</summary>
    Prepared,

    /// <summary>A phase failed and preparation cannot be retried.</summary>
    Failed,
}

/// <summary>
/// Controls one-time composition and configuration.
/// </summary>
/// <remarks>
/// Composition completes before configuration begins. Completed phases are never repeated.
/// A failed phase permanently rejects further preparation, preserving partial side effects.
/// Recursive preparation is rejected. Callers must serialize access to this object.
/// The preparator does not own composed objects or manage startup and shutdown.
/// </remarks>
public sealed class Preparator
{
    /// <summary>
    /// Gets the current preparation phase.
    /// </summary>
    public PreparationState State { get; private set; }

    /// <summary>
    /// Gets whether composition and configuration have completed successfully.
    /// </summary>
    public bool Prepared => State == PreparationState.Prepared;

    /// <summary>
    /// Ensures that composition completes once, without configuring the object.
    /// </summary>
    /// <param name="compose">The operation that composes and registers contents.</param>
    /// <remarks>
    /// Use this phase separately when dependencies must be injected into composed contents
    /// before configuration. Later calls to <see cref="Ensure"/> reuse that composition.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Preparation is active or has failed.</exception>
    public void EnsureComposed(Action compose)
    {
        if (State is PreparationState.Composed or PreparationState.Prepared)
            return;

        if (State != PreparationState.Unprepared)
            throw new InvalidOperationException("Preparation has already started or failed.");

        Execute(compose, PreparationState.Composing, PreparationState.Composed);
    }

    /// <summary>
    /// Ensures that composition and configuration complete once, in that order.
    /// </summary>
    /// <param name="compose">The operation that composes and registers contents.</param>
    /// <param name="configure">The operation that configures composed contents.</param>
    /// <exception cref="InvalidOperationException">Preparation is active or has failed.</exception>
    public void Ensure(Action compose, Action configure)
    {
        EnsureComposed(compose);

        if (Prepared)
            return;

        Execute(configure, PreparationState.Configuring, PreparationState.Prepared);
    }

    private void Execute(Action operation, PreparationState active, PreparationState completed)
    {
        State = active;

        try
        {
            operation();
            State = completed;
        }
        catch
        {
            State = PreparationState.Failed;
            throw;
        }
    }
}
