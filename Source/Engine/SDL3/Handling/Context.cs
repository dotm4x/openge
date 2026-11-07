using OpenGE.Handling;
using OpenGE.Windowing;
using SDL = global::SDL3.SDL;

namespace OpenGE.SDL3.Handling;

/// <summary>
/// Provides SDL3 input events for one input update.
/// </summary>
public sealed class SDL3InputContext(Window window, IEnumerable<SDL.Event> events)
    : InputContext(window)
{
    /// <summary>
    /// Gets the SDL events captured for this update.
    /// </summary>
    public readonly IReadOnlyList<SDL.Event> Events = [.. events];
}
