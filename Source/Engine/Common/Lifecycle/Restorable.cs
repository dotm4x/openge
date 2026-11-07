namespace OpenGE.Common.Lifecycle;

/// <summary>
/// Defines an object that can restore its state.
/// </summary>
public interface IRestorable
{
    /// <summary>
    /// Restores the object to its initial state.
    /// </summary>
    void Restore();
}
