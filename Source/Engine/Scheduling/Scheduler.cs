using System.Diagnostics;
using OpenGE.Common;
using OpenGE.Common.Collections;
using OpenGE.Common.Primitives;
using OpenGE.Logging;
using OpenGE.Scheduling.Channels;

namespace OpenGE.Scheduling;

/// <summary>
/// Manages the main update loop and its update channels.
/// </summary>
/// <remarks>
/// The scheduler defines the maximum rate at which the main update loop runs.
/// Each registered <see cref="UpdateChannel"/> may optionally impose a lower
/// update rate on its own entries.
/// The scheduler owns and destroys its registered channels; their update entries are borrowed.
/// </remarks>
public class Scheduler : Module
{
    private readonly Preparator _preparator = new();

    /// <summary>
    /// Gets the identifiable set of channels managed by the scheduler.
    /// </summary>
    /// <remarks>
    /// The scheduler owns and destroys its registered channels.
    /// Registration transfers ownership to this owner. Removing or clearing entries returns
    /// ownership to the caller without destroying them. Do not register an object owned elsewhere.
    /// </remarks>
    public readonly IdentifiableSet<string, UpdateChannel> Channels = [];

    /// <summary>
    /// Gets the target number of scheduler iterations per second.
    /// </summary>
    /// <remarks>
    /// A positive value limits the maximum rate of the scheduler and,
    /// consequently, the maximum rate at which any channel can be updated.
    ///
    /// A non-positive value disables scheduler iteration pacing and allows the
    /// main loop to run as quickly as possible.
    /// </remarks>
    public readonly int TargetUpdateRate;

    /// <summary>
    /// Initializes a new instance of the <see cref="Scheduler"/> class.
    /// </summary>
    /// <param name="targetUpdateRate">
    /// The target number of scheduler iterations per second.
    /// The default is <c>60</c>. A non-positive value disables iteration pacing.
    /// </param>
    /// <param name="channels">
    /// The initial channels, or <see langword="null"/> for none.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when two initial channels have the same identifier.
    /// </exception>
    public Scheduler(int targetUpdateRate = 60, IEnumerable<UpdateChannel>? channels = null)
        : base("Scheduler", ["Logger"])
    {
        TargetUpdateRate = targetUpdateRate;

        foreach (var channel in channels ?? [])
        {
            Channels.Add(channel);
        }

        Channels.OnAdd.Connect(channel =>
        {
            if (State.Get() == ModuleState.Running)
            {
                channel.Prepare();
                Logger.Log($"Prepared added channel '{channel.Identifier}'.", LogMessageKind.Debug);
            }
        });
    }

    private SourcedLogger Logger => Require<Logger>("Logger").From(Identifier);

    /// <summary>
    /// Gets the amount of real time elapsed since the previous scheduler
    /// iteration, in seconds.
    /// </summary>
    public double DeltaTime { get; private set; }

    private void ComposeContents()
    {
        Logger.Log("Composing module contents.", LogMessageKind.Debug);
        foreach (var channel in Compose())
        {
            try
            {
                Channels.Add(channel);
            }
            catch
            {
                if (!Channels.Contains(channel) && !channel.Destroyed)
                    channel.Destroy();

                throw;
            }
        }
        Logger.Log("Composition completed.", LogMessageKind.Debug);
    }

    private void ConfigureContents()
    {
        Logger.Log("Configuring module.", LogMessageKind.Debug);
        Configure();
        Logger.Log("Configuration completed.", LogMessageKind.Debug);
    }

    /// <summary>
    /// Composes the channels managed by this object.
    /// </summary>
    /// <returns>The channels to register, in enumeration order.</returns>
    /// <remarks>
    /// Composition occurs once when the scheduler first starts, before channels are prepared.
    /// Constructor-provided objects are registered before composed objects.
    /// All composed objects are registered before configuration occurs.
    /// The scheduler owns and destroys its registered channels.
    /// If composition fails, later lifecycle calls reject further initialization.
    /// </remarks>
    protected virtual IEnumerable<UpdateChannel> Compose()
    {
        yield break;
    }

    /// <summary>
    /// Configures relationships and behavior after composition, before startup or first use.
    /// </summary>
    /// <remarks>
    /// All constructor-provided and composed objects are available here.
    /// This hook is invoked at most once, including across later lifecycle cycles.
    /// If configuration throws, later lifecycle calls reject further initialization
    /// rather than repeating configuration side effects.
    /// </remarks>
    protected virtual void Configure() { }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        base.OnDestroy();
        foreach (var channel in Channels.ToArray())
            channel.Destroy();

        Channels.Destroy();
        if (InjectedDependencies is not null)
            Logger.Log("Module resources destroyed.");
    }

    /// <inheritdoc />
    protected override void OnStart()
    {
        Logger.Log("Starting module.");
        _preparator.Ensure(ComposeContents, ConfigureContents);
        foreach (var channel in Channels)
        {
            channel.Prepare();
            Logger.Log($"Prepared channel '{channel.Identifier}'.", LogMessageKind.Debug);
        }
        base.OnStart();
        Logger.Log("Module started.");
    }

    /// <inheritdoc />
    protected override void OnStop()
    {
        Logger.Log("Module stopped.");
        base.OnStop();
    }

    /// <summary>
    /// Runs the main scheduling loop.
    /// </summary>
    /// <remarks>
    /// Each iteration measures the real elapsed time and forwards it to every
    /// registered channel in descending priority order.
    ///
    /// A channel may update at most once during each scheduler iteration.
    /// Therefore, the scheduler's own target update rate acts as the maximum
    /// possible update rate for all channels when iteration pacing is enabled.
    ///
    /// Each channel uses the lower positive target configured by itself and the
    /// scheduler, and reports that effective target in its update context.
    /// Non-positive targets impose no limit.
    ///
    /// When <see cref="TargetUpdateRate"/> is non-positive, the scheduler runs
    /// without an explicit rate limit.
    ///
    /// The loop ends when the scheduler is no longer running. An update failure exits
    /// the loop and includes OpenGE.UpdateChannel in Exception.Data. The application
    /// must stop its owning engine before destroying it; the scheduler does not own modules.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The scheduler is not running.</exception>
    /// <exception cref="OpenGE.Common.Lifecycle.DestroyedObjectException">The scheduler has been destroyed.</exception>
    /// <exception cref="AggregateException">An update and reporting its failure both failed.</exception>
    public void Run()
    {
        ThrowIfDestroyed();
        if (State.Get() != ModuleState.Running)
            throw new InvalidOperationException(
                "The scheduler must be running before entering its update loop."
            );

        if (InjectedDependencies is not null)
            Logger.Log(
                $"Starting update loop with target rate {TargetUpdateRate} Hz and {Channels.Count} channels."
            );
        var stopwatch = Stopwatch.StartNew();
        var previousIterationTime = stopwatch.Elapsed.TotalSeconds;

        while (State.Get() == ModuleState.Running)
        {
            var iterationStartTime = stopwatch.Elapsed.TotalSeconds;

            DeltaTime = iterationStartTime - previousIterationTime;
            previousIterationTime = iterationStartTime;

            foreach (var channel in Channels.OrderByDescending(channel => channel.Priority))
            {
                if (State.Get() != ModuleState.Running)
                    break;

                try
                {
                    channel.Update(DeltaTime, TargetUpdateRate);
                }
                catch (Exception exception)
                {
                    exception.Data["OpenGE.UpdateChannel"] = channel.Identifier;
                    try
                    {
                        if (InjectedDependencies is not null)
                            Logger.Log(
                                $"Update channel '{channel.Identifier}' failed: {exception.Message}",
                                LogMessageKind.Warn
                            );
                    }
                    catch (Exception loggingFailure)
                    {
                        throw new AggregateException(
                            "Updating a channel and reporting its failure failed.",
                            exception,
                            loggingFailure
                        );
                    }
                    throw;
                }
            }

            if (TargetUpdateRate <= 0)
                continue;

            var targetIterationDuration = 1.0 / TargetUpdateRate;

            var elapsedIterationTime = stopwatch.Elapsed.TotalSeconds - iterationStartTime;

            var remainingIterationTime = targetIterationDuration - elapsedIterationTime;

            while (remainingIterationTime > 0)
            {
                // Sleep may return before the target interval, especially for sub-millisecond waits.
                if (remainingIterationTime >= 0.001)
                    Thread.Sleep(TimeSpan.FromSeconds(remainingIterationTime));
                else
                    Thread.SpinWait(1);

                elapsedIterationTime = stopwatch.Elapsed.TotalSeconds - iterationStartTime;
                remainingIterationTime = targetIterationDuration - elapsedIterationTime;
            }
        }

        if (InjectedDependencies is not null)
            Logger.Log("Update loop ended.");
    }
}
