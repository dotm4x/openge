using System.Numerics;
using OpenGE.Common.Events;
using OpenGE.Graphics.Interfaces;
using OpenGE.Graphics.Primitives;
using OpenGE.Spatial.Nodes;

namespace OpenGE.Graphics.Nodes;

/// <summary>
/// Represents a camera positioned in two-dimensional space.
/// </summary>
public class Camera2D : Node2D, ICamera
{
    /// <summary>
    /// Gets the zoom factor applied to the camera view.
    /// A value of <c>1</c> means no zoom.
    /// </summary>
    /// <remarks>
    /// Values that are not finite or are less than or equal to zero are rejected,
    /// and the previous zoom is restored. An assigned store contributes its value
    /// because the camera observes this store.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when a value that is not finite or is less than or equal to zero is assigned.
    /// </exception>
    public Store<float> Zoom
    {
        get;
        init
        {
            if (!ReferenceEquals(field, value))
                field.Value = value.Value;
        }
    } = new(1f);

    /// <summary>
    /// Initializes a new instance of the <see cref="Camera2D"/> class.
    /// </summary>
    /// <remarks>
    /// Initial values are assigned through an object initializer, for example
    /// <c>new Camera2D { Zoom = 2f }</c>.
    /// </remarks>
    /// <param name="name">
    /// The initial name of the node. The default is <c>"Camera2D"</c>.
    /// </param>
    public Camera2D(string name = "Camera2D")
        : base(name)
    {
        Zoom.Connect(OnZoomChanged, true);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The projection keeps the Y axis pointing down, so world positions map to
    /// screen-space coordinates with the origin at the viewport center.
    /// </remarks>
    public Matrix4x4 GetProjection(Viewport viewport)
    {
        var zoom = Zoom.Value;

        var width = viewport.Size.X / zoom;
        var height = viewport.Size.Y / zoom;

        return Matrix4x4.CreateOrthographicOffCenter(
            -width / 2f,
            width / 2f,
            height / 2f,
            -height / 2f,
            -1f,
            1f
        );
    }

    /// <inheritdoc />
    public Matrix4x4 View
    {
        get
        {
            var position = GlobalPosition;
            var rotation = GlobalRotation;

            return Matrix4x4.CreateTranslation(-position.X, -position.Y, 0f)
                * Matrix4x4.CreateRotationZ(-rotation);
        }
    }

    private void OnZoomChanged(float zoom)
    {
        if (float.IsFinite(zoom) && zoom > 0f)
            return;

        Zoom.Value = Zoom.Previous;

        throw new ArgumentOutOfRangeException(
            nameof(Zoom),
            zoom,
            "Camera zoom must be finite and greater than zero."
        );
    }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        Zoom.Destroy();

        base.OnDestroy();
    }
}
