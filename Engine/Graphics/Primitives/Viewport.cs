using System.Numerics;

namespace OpenGE.Graphics.Primitives;

/// <summary>
/// Describes a rectangular region within a rendering target.
/// </summary>
/// <param name="Position">
/// The position of the viewport within its rendering target.
/// </param>
/// <param name="Size">
/// The size of the viewport in rendering-target units.
/// </param>
public readonly record struct Viewport(Vector2 Position, Vector2 Size)
{
    /// <summary>
    /// Gets the viewport aspect ratio, calculated as width divided by height.
    /// </summary>
    /// <remarks>
    /// Returns <c>0</c> when the viewport height is <c>0</c>.
    /// </remarks>
    public float AspectRatio => Size.Y == 0f ? 0f : Size.X / Size.Y;
}
