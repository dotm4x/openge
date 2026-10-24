using System.Numerics;
using OpenGE.Graphics.Primitives;
using OpenGE.Rendering;
using Xunit;

namespace Tests.Rendering;

public class RenderContextTests
{
    [Fact]
    public void Constructor_PreservesFrameState()
    {
        var camera = new TestCamera();
        var viewport = new Viewport(new Vector2(4, 7), new Vector2(640, 480));
        var context = new RenderContext(0.25, camera, viewport);

        Assert.Same(camera, context.Camera);
        Assert.Equal(0.25, context.DeltaTime);
        Assert.Equal(viewport, context.Viewport);
    }
}
