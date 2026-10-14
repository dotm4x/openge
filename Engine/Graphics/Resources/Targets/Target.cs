using System.Numerics;
using OpenGE.Graphics.Resources.Textures;

namespace OpenGE.Graphics.Resources.Targets;

/// <summary>
/// Represents a texture that can be used as an offscreen rendering destination.
/// </summary>
/// <remarks>
/// Rendering backends manage the native GPU resources associated with
/// the target. Its contents can be sampled by subsequent rendering operations.
/// </remarks>
public sealed class RenderTarget : Texture
{
    /// <summary>
    /// Gets the dimensions of the render target, in pixels.
    /// </summary>
    public override Vector2 Size { get; }

    /// <summary>
    /// Initializes a new render target with the specified dimensions.
    /// </summary>
    /// <param name="size">The dimensions of the target, in pixels.</param>
    public RenderTarget(Vector2 size)
    {
        if (
            !float.IsFinite(size.X)
            || !float.IsFinite(size.Y)
            || size.X < 1
            || size.Y < 1
            || size.X != MathF.Truncate(size.X)
            || size.Y != MathF.Truncate(size.Y)
            || size.X >= uint.MaxValue
            || size.Y >= uint.MaxValue
        )
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        Size = size;
    }
}
