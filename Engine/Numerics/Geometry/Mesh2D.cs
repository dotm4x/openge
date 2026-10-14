using System.Numerics;

namespace OpenGE.Numerics.Geometry;

/// <summary>
/// Represents indexed two-dimensional triangle geometry.
/// </summary>
/// <remarks>
/// Vertex positions are expressed in local two-dimensional coordinates.
/// The mesh does not define how the geometry is positioned, rendered,
/// or used by other systems.
/// </remarks>
public sealed class Mesh2D : Mesh<Vector2>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Mesh2D"/> class.
    /// </summary>
    /// <param name="vertices">The local vertex positions to copy.</param>
    /// <param name="indices">
    /// The triangle indices to copy, or <see langword="null"/> to use the
    /// vertices sequentially in groups of three.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when a vertex contains nonfinite coordinates, there are no
    /// complete triangles, or an index refers to a vertex outside the mesh.
    /// </exception>
    public Mesh2D(IEnumerable<Vector2> vertices, IEnumerable<int>? indices = null)
        : base(Validate(vertices), indices) { }

    private static IEnumerable<Vector2> Validate(IEnumerable<Vector2> vertices)
    {
        var index = 0;

        foreach (var vertex in vertices)
        {
            if (!float.IsFinite(vertex.X) || !float.IsFinite(vertex.Y))
            {
                throw new ArgumentException(
                    $"Vertex {index} contains nonfinite coordinates.",
                    nameof(vertices)
                );
            }

            yield return vertex;
            index++;
        }
    }
}
