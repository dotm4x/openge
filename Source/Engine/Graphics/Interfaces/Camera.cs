using System.Numerics;
using OpenGE.Graphics.Primitives;

namespace OpenGE.Graphics.Interfaces;

/// <summary>
/// Provides the transformations required to render a scene from a camera view.
/// </summary>
public interface ICamera
{
    /// <summary>
    /// Gets the transformation from world space into camera view space.
    /// </summary>
    Matrix4x4 View { get; }

    /// <summary>
    /// Creates the projection transformation for the specified viewport.
    /// </summary>
    /// <param name="viewport">
    /// The viewport for which to create the projection transformation.
    /// </param>
    /// <returns>The projection transformation.</returns>
    Matrix4x4 GetProjection(Viewport viewport);
}
