using System.Numerics;
using OpenGE.Common;

namespace OpenGE.Graphics.Resources.Textures;

/// <summary>
/// Represents a texture that can be sampled during rendering.
/// </summary>
/// <remarks>
/// A texture describes a graphical resource independently of its
/// source and the rendering backend.
///
/// Derived types define how the texture obtains its contents.
/// Rendering backends manage the corresponding native GPU resources.
/// </remarks>
public abstract class Texture : Resource
{
    /// <summary>
    /// Gets the dimensions of the texture, in pixels.
    /// </summary>
    public abstract Vector2 Size { get; }
}
