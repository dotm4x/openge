namespace Mirage.Common.Primitives;

/// <summary>
/// Provides information about the currently executing step in a sequence.
/// </summary>
/// <param name="Sequence">The sequence executing the step.</param>
/// <param name="Index">The zero-based index of the executing step.</param>
public readonly record struct SequenceContext(Sequence Sequence, int Index);

/// <summary>
/// Represents an ordered sequence of actions with controllable execution flow.
/// </summary>
/// <remarks>
/// Steps execute synchronously in registration order unless navigation changes
/// the next step. Navigation requested during a step takes effect after the
/// action returns.
///
/// The sequence does not own resources captured by its actions.
/// Callers must serialize access to the sequence.
/// </remarks>
/// <example>
/// <code>
/// var sequence = new Sequence()
///     .Step(context => Console.WriteLine("First"))
///     .Step(context => context.Sequence.Skip(1))
///     .Step(context => Console.WriteLine("Skipped"))
///     .Step(context => Console.WriteLine("Last"))
///     .Start();
/// </code>
/// </example>
public sealed class Sequence
{
    private readonly List<Action<SequenceContext>> _steps = [];

    private bool _executing;
    private int _index;
    private bool _runningLoop;

    /// <summary>
    /// Gets whether all steps have been consumed.
    /// </summary>
    public bool Completed => _index == Count && !_executing;

    /// <summary>
    /// Gets the number of registered steps.
    /// </summary>
    public int Count => _steps.Count;

    /// <summary>
    /// Gets the index of the next step to execute.
    /// </summary>
    /// <remarks>
    /// An index equal to <see cref="Count"/> indicates that no
    /// steps remain. Indices are zero-based.
    /// </remarks>
    public int Index => _index;

    /// <summary>
    /// Gets whether automatic execution is paused.
    /// </summary>
    public bool Paused { get; private set; }

    /// <summary>
    /// Gets whether automatic execution is running.
    /// </summary>
    public bool Running { get; private set; }

    private void Execute()
    {
        _executing = true;

        var index = _index;
        var action = _steps[index];

        // Prepare the default continuation before invoking the action.
        // Navigation methods may override this position.
        _index = index + 1;

        try
        {
            action(new SequenceContext(this, index));
        }
        catch
        {
            Running = false;
            Paused = false;
            throw;
        }
        finally
        {
            _executing = false;
        }
    }

    private void Run()
    {
        if (_runningLoop)
            return;

        _runningLoop = true;

        try
        {
            while (Running && _index < Count)
                Execute();

            if (_index >= Count)
            {
                Running = false;
                Paused = false;
            }
        }
        finally
        {
            _runningLoop = false;
        }
    }

    private void ThrowIfExecuting()
    {
        if (_executing)
            throw new InvalidOperationException("The sequence is already executing a step.");
    }

    private void ValidateIndex(int index)
    {
        if (index < 0 || index > Count)
            throw new ArgumentOutOfRangeException(nameof(index));
    }

    /// <summary>
    /// Removes every registered step and resets execution.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a step is executing.
    /// </exception>
    public void Clear()
    {
        Reset();
        _steps.Clear();
    }

    /// <summary>
    /// Moves execution to a specified step.
    /// </summary>
    /// <param name="index">The destination index.</param>
    /// <remarks>
    /// Moving backward does not undo previously executed actions.
    /// Navigation during a step changes which step executes next.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the destination is outside the valid range.
    /// </exception>
    public void GoTo(int index)
    {
        ValidateIndex(index);
        _index = index;
    }

    /// <summary>
    /// Executes one step without starting automatic execution.
    /// </summary>
    /// <returns>Whether a step was executed.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a step is already executing or the sequence is running.
    /// </exception>
    public bool Next()
    {
        ThrowIfExecuting();

        if (Running)
            throw new InvalidOperationException(
                "Cannot advance manually while the sequence is running."
            );

        if (_index >= Count)
            return false;

        Execute();
        return true;
    }

    /// <summary>
    /// Pauses automatic execution after the current action returns.
    /// </summary>
    public void Pause()
    {
        if (!Running)
            return;

        Running = false;
        Paused = true;
    }

    /// <summary>
    /// Restores the initial execution position without running steps.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a step is executing.
    /// </exception>
    public void Reset()
    {
        ThrowIfExecuting();

        Running = false;
        Paused = false;
        _index = 0;
    }

    /// <summary>
    /// Restarts execution from the first step.
    /// </summary>
    /// <remarks>
    /// When called during a step, execution restarts after the current
    /// action returns, without recursively invoking the execution loop.
    /// </remarks>
    public void Restart()
    {
        _index = 0;
        Running = true;
        Paused = false;

        if (!_runningLoop && !_executing)
            Run();
    }

    /// <summary>
    /// Omits the next specified number of steps.
    /// </summary>
    /// <param name="count">The number of steps to omit.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when count is negative.
    /// </exception>
    public void Skip(int count = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        _index = (int)Math.Min((long)_index + count, Count);
    }

    /// <summary>
    /// Advances to a specified step, omitting intermediate steps.
    /// </summary>
    /// <param name="index">The destination index.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the destination precedes the current position
    /// or exceeds the number of steps.
    /// </exception>
    public void SkipTo(int index)
    {
        ValidateIndex(index);

        if (index < _index)
            throw new ArgumentOutOfRangeException(nameof(index));

        _index = index;
    }

    /// <summary>
    /// Begins or resumes automatic execution.
    /// </summary>
    /// <remarks>
    /// Execution continues synchronously until the sequence completes,
    /// pauses, stops, or an action throws an exception.
    ///
    /// Calling this method on a completed sequence has no effect.
    /// Use <see cref="Restart"/> to execute it from the beginning.
    /// </remarks>
    /// <returns>The current sequence.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a step is already executing.
    /// </exception>
    public Sequence Start()
    {
        ThrowIfExecuting();

        if (_index >= Count)
            return this;

        Running = true;
        Paused = false;

        Run();

        return this;
    }

    /// <summary>
    /// Appends an action to the sequence.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <returns>The current sequence.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the action is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a step is executing.
    /// </exception>
    public Sequence Step(Action<SequenceContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ThrowIfExecuting();

        _steps.Add(action);
        return this;
    }

    /// <summary>
    /// Stops automatic execution while preserving the next index.
    /// </summary>
    public void Stop()
    {
        Running = false;
        Paused = false;
    }
}
