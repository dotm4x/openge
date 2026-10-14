using OpenGE.Windowing;

namespace OpenGE.Handling;

/// <summary>
/// Provides the context required to process input for one window.
/// </summary>
/// <remarks>
/// The window is borrowed and is not owned by the context.
/// Platform-specific implementations may expose additional input data.
/// </remarks>
/// <param name="window">The window associated with this input context.</param>
public abstract class InputContext(Window window)
{
    /// <summary>
    /// Gets the window associated with this input context.
    /// </summary>
    public readonly Window Window = window;
}
