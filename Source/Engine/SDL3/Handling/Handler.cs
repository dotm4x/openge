using OpenGE.Handling;
using OpenGE.Handling.Devices;
using OpenGE.SDL3.Windowing;

namespace OpenGE.SDL3.Handling;

/// <summary>
/// Provides SDL3 input contexts to registered input devices.
/// </summary>
public class SDL3InputHandler(
    int index,
    SDL3Window window,
    IEnumerable<InputDevice>? devices = null
) : InputHandler(index, window, devices)
{
    private SDL3Window SDLWindow => (SDL3Window)Window;

    /// <inheritdoc />
    protected override InputContext CreateContext()
    {
        return new SDL3InputContext(SDLWindow, SDLWindow.FrameEvents);
    }
}
