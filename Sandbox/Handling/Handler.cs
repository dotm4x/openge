using OpenGE.Handling.Devices;
using OpenGE.SDL3.Handling;
using OpenGE.SDL3.Windowing;

namespace OpenGE.Sandbox.Handling;

internal sealed class MyInputHandler(SDL3Window window) : SDL3InputHandler(0, window)
{
    protected override IEnumerable<InputDevice> Compose()
    {
        yield break;
    }
}
