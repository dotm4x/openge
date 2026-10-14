using System.Numerics;
using OpenGE.Graphics.Primitives;

namespace OpenGE.Graphics.Geometry;

/// <summary>
/// Represents a two-dimensional vertex prepared for graphical rendering.
/// </summary>
/// <param name="Position">
/// The local position of the vertex.
/// </param>
/// <param name="TextureCoordinate">
/// The texture coordinate associated with the vertex.
/// </param>
/// <param name="Color">
/// The color associated with the vertex.
/// </param>
public readonly record struct GraphicVertex2D(
    Vector2 Position,
    Vector2 TextureCoordinate,
    Color Color
);
