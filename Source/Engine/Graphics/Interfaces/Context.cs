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
    /// <remarks>
    /// The camera is required; rendering spaces without a camera are rejected
    /// before their commands are produced.
    /// </remarks>
    ICamera Camera { get; }

    /// <summary>
    /// Gets the delta time of the update that produced the current rendering frame,
    /// in seconds.
    /// </summary>
    /// <remarks>
    /// The value comes from the scheduled update that drives rendering. It is not
    /// necessarily the elapsed time between two rendering frames.
    /// </remarks>
    double DeltaTime { get; }

    /// <summary>
    /// Gets the viewport in which the current view is rendered.
    /// </summary>
    Viewport Viewport { get; }
}
