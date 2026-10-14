using OpenGE.Common.Interfaces;
using OpenGE.Common.Lifecycle;

namespace OpenGE.Handling.Devices;

/// <summary>
/// Represents an input device processed by an input handler.
/// </summary>
/// <param name="identifier">The stable identifier of this device.</param>
public abstract class InputDevice(string identifier) : Destroyable, IIdentifiable<string>
{
    /// <inheritdoc />
    public string Identifier { get; } = identifier;

    /// <summary>
    /// Processes input using the supplied context.
    /// </summary>
    /// <param name="context">The input context available for this update.</param>
    protected virtual void OnProcess(InputContext context) { }

    /// <summary>
    /// Processes this device once.
    /// </summary>
    /// <param name="context">The input context available for this update.</param>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when this device has been destroyed.
    /// </exception>
    public void Process(InputContext context)
    {
        ThrowIfDestroyed();
        OnProcess(context);
    }
}
