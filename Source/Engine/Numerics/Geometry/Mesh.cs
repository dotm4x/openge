namespace OpenGE.Numerics.Geometry;

/// <summary>
/// Represents indexed triangle geometry composed of vertices of a specific type.
/// </summary>
/// <typeparam name="TVertex">
/// The type used to represent each vertex.
/// </typeparam>
/// <remarks>
/// Derived mesh types define the representation
/// and validation requirements of their vertices.
/// </remarks>
public abstract class Mesh<TVertex>
    where TVertex : unmanaged
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Mesh{TVertex}"/> class.
    /// </summary>
    /// <param name="vertices">The vertices to copy.</param>
    /// <param name="indices">
    /// The triangle indices to copy, or <see langword="null"/> to use the
    /// vertices sequentially in groups of three.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when there are no complete triangles or an index refers to a
    /// vertex outside the mesh.
    /// </exception>
    protected Mesh(IEnumerable<TVertex> vertices, IEnumerable<int>? indices = null)
    {
        var vertexArray = vertices.ToArray();
        var indexArray = indices?.ToArray() ?? Enumerable.Range(0, vertexArray.Length).ToArray();

        if (indexArray.Length == 0 || indexArray.Length % 3 != 0)
        {
            throw new ArgumentException(
                "A mesh must contain at least one complete triangle.",
                indices is null ? nameof(vertices) : nameof(indices)
            );
        }

        for (var index = 0; index < indexArray.Length; index++)
        {
            if ((uint)indexArray[index] >= (uint)vertexArray.Length)
            {
                throw new ArgumentException(
                    $"Index {index} refers to a vertex outside the mesh.",
                    nameof(indices)
                );
            }
        }

        Vertices = vertexArray;
        Indices = indexArray;
    }

    /// <summary>
    /// Gets the vertex indices ordered in groups of three.
    /// </summary>
    public ReadOnlyMemory<int> Indices { get; }

    /// <summary>
    /// Gets the number of triangles in the mesh.
    /// </summary>
    public int TriangleCount => Indices.Length / 3;

    /// <summary>
    /// Gets the vertices that compose the mesh.
    /// </summary>
    public ReadOnlyMemory<TVertex> Vertices { get; }
}
