using OpenGE.Numerics.Geometry;
using Xunit;

namespace Tests.Numerics.Geometry;

public class MeshTests
{
    [Fact]
    public void Constructor_WithoutIndices_UsesVerticesSequentially()
    {
        var vertices = new[] { 10, 20, 30, 40, 50, 60 };

        var mesh = new TestMesh(vertices);

        Assert.Equal(vertices, mesh.Vertices.ToArray());
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, mesh.Indices.ToArray());
        Assert.Equal(2, mesh.TriangleCount);
    }

    [Fact]
    public void Constructor_WithIndices_CopiesVerticesAndIndices()
    {
        var vertices = new[] { 10, 20, 30 };
        var indices = new[] { 2, 1, 0 };

        var mesh = new TestMesh(vertices, indices);
        vertices[0] = 99;
        indices[0] = 0;

        Assert.Equal(new[] { 10, 20, 30 }, mesh.Vertices.ToArray());
        Assert.Equal(new[] { 2, 1, 0 }, mesh.Indices.ToArray());
        Assert.Equal(1, mesh.TriangleCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Constructor_WithoutCompleteTriangle_ThrowsArgumentException(int vertexCount)
    {
        var vertices = Enumerable.Range(0, vertexCount);

        var exception = Assert.Throws<ArgumentException>(() => new TestMesh(vertices));

        Assert.Equal("vertices", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void Constructor_WithIndicesNotFormingTriangles_ThrowsArgumentException(int indexCount)
    {
        var vertices = new[] { 10, 20, 30 };
        var indices = Enumerable.Repeat(0, indexCount);

        var exception = Assert.Throws<ArgumentException>(() => new TestMesh(vertices, indices));

        Assert.Equal("indices", exception.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Constructor_WithOutOfRangeIndex_ThrowsArgumentException(int invalidIndex)
    {
        var vertices = new[] { 10, 20, 30 };
        var indices = new[] { 0, 1, invalidIndex };

        var exception = Assert.Throws<ArgumentException>(() => new TestMesh(vertices, indices));

        Assert.Equal("indices", exception.ParamName);
    }

    private sealed class TestMesh(IEnumerable<int> vertices, IEnumerable<int>? indices = null)
        : Mesh<int>(vertices, indices);
}
