using System.Numerics;
using OpenGE.Graphics.Nodes;
using OpenGE.Graphics.Primitives;
using Xunit;

namespace Tests.Graphics.Nodes;

public class Camera2DTests
{
    [Fact]
    public void GetProjection_UsesViewportSizeAndZoom()
    {
        var camera = new Camera2D("camera", new Camera2DOptions { Zoom = 2f });
        var viewport = new Viewport(Vector2.Zero, new Vector2(800, 600));

        var projection = camera.GetProjection(viewport);

        // The projection keeps the Y axis pointing down, matching screen space.
        Assert.Equal(2f / 400f, projection.M11, precision: 5);
        Assert.Equal(-2f / 300f, projection.M22, precision: 5);
        Assert.Equal(0f, projection.M41, precision: 5);
        Assert.Equal(0f, projection.M42, precision: 5);
    }

    [Fact]
    public void GetProjection_WhenZoomIsInvalid_ThrowsInvalidOperationException()
    {
        var camera = new Camera2D();

        camera.Zoom.Set(0f);

        Assert.Throws<InvalidOperationException>(() => camera.GetProjection(default));

        camera.Zoom.Set(float.NaN);

        Assert.Throws<InvalidOperationException>(() => camera.GetProjection(default));
    }

    [Fact]
    public void View_MapsGlobalPositionToCameraOrigin()
    {
        var camera = new Camera2D("camera", new Camera2DOptions { Position = new Vector2(10, -5) });

        var view = camera.View;
        var origin = Vector4.Transform(new Vector4(10, -5, 0, 1), view);

        Assert.Equal(0f, origin.X, precision: 5);
        Assert.Equal(0f, origin.Y, precision: 5);
    }

    [Fact]
    public void Constructor_InvalidZoom_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Camera2D("camera", new Camera2DOptions { Zoom = 0f })
        );
    }
}
