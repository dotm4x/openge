using OpenGE.Scheduling;

namespace OpenGE.Scheduling.Interfaces;

/// <summary>
/// Defines an object that can participate in scheduled updates.
/// </summary>
public interface IUpdatable
{
    /// <summary>
    /// Updates the object using the timing information for the current
    /// channel update.
    /// </summary>
    /// <param name="context">
    /// The context describing the current update, including its delta time
    /// and target update rate.
    /// </param>
    void Update(UpdateContext context);
}
