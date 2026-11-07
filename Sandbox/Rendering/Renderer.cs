using OpenGE.Graphics.Interfaces;
using OpenGE.Graphics.Primitives;
using OpenGE.Rendering;
using OpenGE.Rendering.Spaces;
using OpenGE.SDL3.Rendering;
using OpenGE.SDL3.Windowing;

namespace OpenGE.Sandbox.Rendering;

internal sealed class MyRenderer(ICamera camera, SDL3Window window)
    : Renderer(0, new SDL3RenderBackend(window), window, clearColor: Color.Black, spaces: [])
{
    protected override IEnumerable<RenderSpace> ComposeSpaces()
    {
        yield return new RenderSpace("World", camera, priority: RenderSpacePriority.Low);
    }
}
