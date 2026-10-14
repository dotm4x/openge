using OpenGE.Graphics.Primitives;

namespace OpenGE.Graphics.Interfaces;

/// <summary>
/// Provides the state available while producing rendering data for a frame.
/// </summary>
/// <remarks>
/// A rendering context contains frame-specific state used by renderable objects
/// to produce <see cref="RenderData"/>.
///
/// The context does not execute rendering operations or depend on a specific
/// rendering backend.
/// </remarks>
public interface IRenderContext
{
    /// <summary>
    /// Gets the camera used to render the current view.
    /// </summary>
    ICamera Camera { get; }

    /// <summary>
    /// Gets the elapsed time since the previous rendering frame, in seconds.
    /// </summary>
    double DeltaTime { get; }

    /// <summary>
    /// Gets the viewport in which the current view is rendered.
    /// </summary>
    Viewport Viewport { get; }
}
