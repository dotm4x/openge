namespace OpenGE.Scheduling;

/// <summary>
/// Provides timing information for a scheduled update.
/// </summary>
/// <remarks>
/// An update context describes the timing of the channel update currently
/// being executed. Its values belong to the channel rather than necessarily
/// to the scheduler's main loop.
/// </remarks>
public readonly record struct UpdateContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateContext"/> struct.
    /// </summary>
    /// <param name="deltaTime">
    /// The amount of simulated time represented by this update, in seconds.
    /// </param>
    /// <param name="updateRate">
    /// The effective target number of updates per second, capped by any positive
    /// scheduler target, or <c>0</c> when neither imposes a limit.
    /// </param>
    public UpdateContext(double deltaTime, double updateRate)
    {
        DeltaTime = deltaTime;
        UpdateRate = updateRate;
    }

    /// <summary>
    /// Gets the amount of simulated time represented by this update,
    /// in seconds.
    /// </summary>
    public double DeltaTime { get; }

    /// <summary>
    /// Gets the effective target number of updates per second for the channel.
    /// </summary>
    /// <remarks>
    /// The value is the lower positive target configured by the channel and scheduler.
    /// A value of <c>0</c> means neither imposes a limit. This is a target, not the
    /// measured update rate; actual updates may be slower.
    /// </remarks>
    public double UpdateRate { get; }
}
