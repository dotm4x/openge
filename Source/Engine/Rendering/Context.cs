using OpenGE.Graphics;
using OpenGE.Graphics.Interfaces;
using OpenGE.Graphics.Primitives;

namespace OpenGE.Rendering;

/// <summary>
/// Provides the rendering state available while producing rendering data for a frame.
/// </summary>
/// <remarks>
/// A rendering context contains frame-specific state used by renderable objects
/// to produce <see cref="RenderData"/>.
///
/// The context does not execute rendering commands and does not depend on a
/// specific rendering backend.
/// </remarks>
/// <param name="deltaTime">
/// The elapsed time since the previous rendering frame, in seconds.
/// </param>
/// <param name="camera">
/// The camera used to render the current view.
/// </param>
/// <param name="viewport">
/// The viewport associated with the current view.
/// </param>
public class RenderContext(double deltaTime, ICamera camera, Viewport viewport) : IRenderContext
{
    /// <inheritdoc />
    public ICamera Camera { get; } = camera;

    /// <inheritdoc />
    public double DeltaTime { get; } = deltaTime;

    /// <inheritdoc />
    public Viewport Viewport { get; } = viewport;
}
