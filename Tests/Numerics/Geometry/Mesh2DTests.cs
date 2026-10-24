using System.Numerics;
using OpenGE.Numerics.Geometry;
using Xunit;

namespace Tests.Numerics.Geometry;

public class Mesh2DTests
{
    [Fact]
    public void Constructor_WithoutIndices_UsesVerticesSequentially()
    {
        var vertices = new[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(2, 0),
            new Vector2(3, 0),
            new Vector2(2, 1),
        };

        var mesh = new Mesh2D(vertices);

        Assert.Equal(vertices, mesh.Vertices.ToArray());
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, mesh.Indices.ToArray());
        Assert.Equal(2, mesh.TriangleCount);
    }

    [Fact]
    public void Constructor_WithIndices_CopiesIndexedGeometry()
    {
        var vertices = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1) };
        var indices = new[] { 2, 1, 0 };

        var mesh = new Mesh2D(vertices, indices);
        vertices[0] = new Vector2(10, 10);
        indices[0] = 0;

        Assert.Equal(
            new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1) },
            mesh.Vertices.ToArray()
        );
        Assert.Equal(new[] { 2, 1, 0 }, mesh.Indices.ToArray());
        Assert.Equal(1, mesh.TriangleCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Constructor_WithFewerThanThreeVertices_ThrowsArgumentException(int vertexCount)
    {
        var vertices = Enumerable.Repeat(Vector2.Zero, vertexCount);

        var exception = Assert.Throws<ArgumentException>(() => new Mesh2D(vertices));

        Assert.Equal("vertices", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithIndicesNotFormingTriangles_ThrowsArgumentException()
    {
        var vertices = new[] { Vector2.Zero, Vector2.UnitX, Vector2.UnitY };
        var indices = new[] { 0, 1 };

        var exception = Assert.Throws<ArgumentException>(() => new Mesh2D(vertices, indices));

        Assert.Equal("indices", exception.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Constructor_WithOutOfRangeIndex_ThrowsArgumentException(int invalidIndex)
    {
        var vertices = new[] { Vector2.Zero, Vector2.UnitX, Vector2.UnitY };
        var indices = new[] { 0, 1, invalidIndex };

        var exception = Assert.Throws<ArgumentException>(() => new Mesh2D(vertices, indices));

        Assert.Equal("indices", exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(NonfiniteVertices))]
    public void Constructor_WithNonfiniteCoordinate_ThrowsArgumentException(Vector2 invalidVertex)
    {
        var vertices = new[] { Vector2.Zero, Vector2.UnitX, invalidVertex };

        var exception = Assert.Throws<ArgumentException>(() => new Mesh2D(vertices));

        Assert.Equal("vertices", exception.ParamName);
    }

    public static IEnumerable<object[]> NonfiniteVertices =>
        new[]
        {
            new object[] { new Vector2(float.NaN, 0) },
            new object[] { new Vector2(0, float.NaN) },
            new object[] { new Vector2(float.PositiveInfinity, 0) },
            new object[] { new Vector2(0, float.NegativeInfinity) },
        };
}
