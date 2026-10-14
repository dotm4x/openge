using OpenGE.Graphics.Primitives;
using OpenGE.Numerics.Geometry;

namespace OpenGE.Graphics.Geometry;

/// <summary>
/// Represents indexed two-dimensional geometry prepared for graphical rendering.
/// </summary>
/// <remarks>
/// Each vertex contains the graphical attributes required by the standard
/// two-dimensional rendering pipeline, including its position, texture
/// coordinate, and color.
/// </remarks>
public sealed class GraphicMesh2D : Mesh<GraphicVertex2D>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphicMesh2D"/> class.
    /// </summary>
    /// <param name="vertices">The graphical vertices to copy.</param>
    /// <param name="indices">
    /// The triangle indices to copy, or <see langword="null"/> to use the
    /// vertices sequentially in groups of three.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when there are no complete triangles, an index refers to a
    /// vertex outside the mesh, or a vertex contains nonfinite values.
    /// </exception>
    public GraphicMesh2D(IEnumerable<GraphicVertex2D> vertices, IEnumerable<int>? indices = null)
        : base(Validate(vertices), indices) { }

    private static IEnumerable<GraphicVertex2D> Validate(IEnumerable<GraphicVertex2D> vertices)
    {
        var index = 0;

        foreach (var vertex in vertices)
        {
            if (
                !float.IsFinite(vertex.Position.X)
                || !float.IsFinite(vertex.Position.Y)
                || !float.IsFinite(vertex.TextureCoordinate.X)
                || !float.IsFinite(vertex.TextureCoordinate.Y)
            )
            {
                throw new ArgumentException(
                    $"Vertex {index} contains nonfinite values.",
                    nameof(vertices)
                );
            }

            yield return vertex;
            index++;
        }
    }
}
