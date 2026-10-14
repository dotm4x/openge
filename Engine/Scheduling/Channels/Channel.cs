using OpenGE.Common.Collections;
using OpenGE.Common.Interfaces;
using OpenGE.Common.Lifecycle;
using OpenGE.Common.Primitives;
using OpenGE.Scheduling.Interfaces;

namespace OpenGE.Scheduling.Channels;

/// <summary>
/// Represents the priority of an update channel.
/// Higher priorities are processed first.
/// </summary>
public enum UpdateChannelPriority
{
    /// <summary>
    /// Low priority.
    /// </summary>
    Low,

    /// <summary>
    /// Normal priority.
    /// </summary>
    Normal,

    /// <summary>
    /// High priority.
    /// </summary>
    High,

    /// <summary>
    /// Critical priority.
    /// </summary>
    Critical,
}

/// <summary>
/// Represents a prioritized collection of objects updated at a configurable
/// rate.
/// </summary>
/// <remarks>
/// A channel may impose its own target update rate, but it can never execute
/// more than once during a single scheduler iteration.
///
/// The effective target is the lower positive limit configured by the channel
/// and scheduler. A non-positive limit is unlimited; both must be non-positive
/// for the channel to run without a rate limit.
///
/// Destroying a channel clears its entry references but does not destroy the
/// entries themselves.
/// </remarks>
public class UpdateChannel : Destroyable, IIdentifiable<string>
{
    private double _accumulator;
    private readonly Preparator _preparator = new();

    /// <summary>
    /// Gets the updatable entries contained in this channel.
    /// </summary>
    /// <remarks>Entries are borrowed. Registration, removal, and clearing do not transfer ownership.</remarks>
    public readonly ReactiveSet<IUpdatable> Entries = [];

    /// <summary>
    /// Gets the priority used to order this channel relative to other channels.
    /// </summary>
    public readonly UpdateChannelPriority Priority;

    /// <summary>
    /// Gets the target number of updates per second.
    /// </summary>
    /// <remarks>
    /// A positive value limits how frequently this channel updates.
    ///
    /// A non-positive value disables only the channel's own rate limit.
    /// The scheduler's positive target rate also caps this channel, even when
    /// this value is higher or unlimited. The configured value is preserved.
    /// </remarks>
    public readonly double TargetUpdateRate;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateChannel"/> class.
    /// </summary>
    /// <param name="identifier">
    /// The unique identifier of the channel.
    /// </param>
    /// <param name="targetUpdateRate">
    /// The target number of updates per second.
    /// A non-positive value disables the channel's own rate limit.
    /// </param>
    /// <param name="priority">
    /// The priority of the channel.
    /// </param>
    /// <param name="entries">
    /// The initial entries, or <see langword="null"/> for none.
    /// </param>
    public UpdateChannel(
        string identifier,
        double targetUpdateRate = 0,
        UpdateChannelPriority priority = UpdateChannelPriority.Normal,
        IEnumerable<IUpdatable>? entries = null
    )
    {
        Identifier = identifier;
        TargetUpdateRate = targetUpdateRate;
        Priority = priority;

        foreach (var entry in entries ?? [])
        {
            Entries.Add(entry);
        }
    }

    /// <summary>
    /// Gets the real elapsed time represented by the latest channel update,
    /// in seconds, or zero before the first update.
    /// </summary>
    public double DeltaTime { get; private set; }

    /// <summary>
    /// Gets the measured number of channel updates per second, or zero before
    /// the first update.
    /// </summary>
    public double UpdateRate { get; private set; }

    /// <inheritdoc />
    public string Identifier { get; }

    private void ComposeContents()
    {
        var composedObjects = Compose().ToArray();
        HashSet<IUpdatable> objects = [];

        foreach (var entry in composedObjects)
        {
            if (Entries.Contains(entry) || !objects.Add(entry))
                throw new InvalidOperationException("Duplicate composed object found.");
        }

        foreach (var entry in composedObjects)
            Entries.Add(entry);
    }

    private void ConfigureContents()
    {
        Configure();
    }

    private void PerformUpdate(UpdateContext context)
    {
        DeltaTime = context.DeltaTime;
        UpdateRate = DeltaTime > 0 ? 1.0 / DeltaTime : 0;

        foreach (var entry in Entries)
            entry.Update(context);

        OnUpdate(context);
    }

    /// <summary>
    /// Composes the entries managed by this object.
    /// </summary>
    /// <returns>The entries to register, in enumeration order.</returns>
    /// <remarks>
    /// Composition occurs once when prepared by the scheduler or before the first update.
    /// Constructor-provided objects are registered before composed objects.
    /// All composed objects are registered before configuration occurs.
    /// The channel borrows entries, including those returned here, because modules and nodes
    /// have independent owners. Return objects owned elsewhere; composition does not transfer
    /// their lifetime to this processing collection.
    /// If composition fails, later lifecycle calls reject further initialization.
    /// </remarks>
    protected virtual IEnumerable<IUpdatable> Compose()
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
        Entries.Destroy();

        _accumulator = 0;

        base.OnDestroy();
    }

    /// <summary>
    /// Called after all entries have been updated for a channel tick.
    /// </summary>
    /// <param name="context">
    /// The timing information associated with the update.
    /// </param>
    protected virtual void OnUpdate(UpdateContext context) { }

    /// <summary>
    /// Receives elapsed scheduler time and updates this channel when required.
    /// </summary>
    /// <param name="deltaTime">
    /// The real elapsed time since the previous scheduler iteration, in
    /// seconds.
    /// </param>
    /// <remarks>
    /// Direct calls use the channel's own target rate. When driven by a scheduler,
    /// its positive target rate also caps the channel's effective target rate.
    /// An unlimited effective target executes on every call using the supplied delta time.
    ///
    /// A rate-limited channel accumulates elapsed time until its target
    /// interval has been reached. It then performs one update using the real
    /// accumulated elapsed time.
    ///
    /// A channel never performs more than one update during a single scheduler
    /// iteration.
    /// </remarks>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the channel has already been destroyed.
    /// </exception>
    public void Update(double deltaTime)
    {
        Update(deltaTime, 0);
    }

    internal void Update(double deltaTime, double schedulerTargetUpdateRate)
    {
        ThrowIfDestroyed();

        _preparator.Ensure(ComposeContents, ConfigureContents);

        var effectiveUpdateRate = TargetUpdateRate;
        if (schedulerTargetUpdateRate > 0)
        {
            effectiveUpdateRate =
                effectiveUpdateRate > 0
                    ? System.Math.Min(effectiveUpdateRate, schedulerTargetUpdateRate)
                    : schedulerTargetUpdateRate;
        }

        if (effectiveUpdateRate <= 0)
        {
            PerformUpdate(new UpdateContext(deltaTime, 0));

            return;
        }

        _accumulator += deltaTime;

        var updateInterval = 1.0 / effectiveUpdateRate;

        if (_accumulator < updateInterval)
            return;

        var elapsedTime = _accumulator;

        _accumulator = 0;

        PerformUpdate(new UpdateContext(elapsedTime, effectiveUpdateRate));
    }

    internal void Prepare()
    {
        ThrowIfDestroyed();
        _preparator.Ensure(ComposeContents, ConfigureContents);
    }
}
