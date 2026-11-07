using OpenGE.SDL3.Windowing;
using OpenGE.Windowing;

namespace OpenGE.Sandbox.Windowing;

internal sealed class MyWindowManager : WindowManager
{
    public readonly SDL3Window Main = new(new WindowOptions { Title = "OpenGE Sandbox" });

    protected override IEnumerable<Window> Compose()
    {
        yield return Main;
    }
}
