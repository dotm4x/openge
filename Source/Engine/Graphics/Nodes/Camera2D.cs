using System.Numerics;
using OpenGE.Common.Events;
using OpenGE.Graphics.Interfaces;
using OpenGE.Graphics.Primitives;
using OpenGE.Spatial.Nodes;

namespace OpenGE.Graphics.Nodes;

/// <summary>
/// Provides optional values used to initialize a <see cref="Camera2D"/>.
/// </summary>
public class Camera2DOptions : Node2DOptions
{
    /// <summary>
    /// Gets the initial zoom factor.
    /// The default is <c>1</c>, which means no zoom.
    /// </summary>
    public float Zoom { get; init; } = 1f;
}

/// <summary>
/// Represents a camera positioned in two-dimensional space.
/// </summary>
public class Camera2D : Node2D, ICamera
{
    /// <summary>
    /// Gets the zoom factor applied to the camera view.
    /// A value of <c>1</c> means no zoom.
    /// </summary>
    public readonly Store<float> Zoom;

    /// <summary>
    /// Initializes a new instance of the <see cref="Camera2D"/> class.
    /// </summary>
    /// <param name="name">The node name.</param>
    /// <param name="options">
    /// The initial camera and two-dimensional transformation values,
    /// or <see langword="null"/> for defaults.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="options"/> specifies a zoom that is not finite
    /// or is less than or equal to zero.
    /// </exception>
    public Camera2D(string name = "Camera2D", Camera2DOptions? options = null)
        : base(name, options)
    {
        options ??= new Camera2DOptions();

        if (!float.IsFinite(options.Zoom) || options.Zoom <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.Zoom,
                "Camera zoom must be finite and greater than zero."
            );
        }

        Zoom = new Store<float>(options.Zoom);
    }

    /// <inheritdoc />
    public Matrix4x4 GetProjection(Viewport viewport)
    {
        var zoom = Zoom.Get();

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

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        base.OnDestroy();

        Zoom.Destroy();
    }
}
