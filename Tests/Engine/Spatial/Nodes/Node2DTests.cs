using System.Numerics;
using OpenGE.Noding;
using OpenGE.Spatial.Nodes;
using Xunit;

namespace Tests.Spatial.Nodes;

public class Node2DTests
{
    [Fact]
    public void Constructor_UsesDefaultsAndConfiguredTransform()
    {
        var defaultNode = new Node2D();
        var configuredNode = new Node2D("configured")
        {
            Origin = new Vector2(2, 3),
            Position = new Vector2(10, 20),
            Rotation = MathF.PI / 2,
            Scale = new Vector2(2, 4),
        };

        Assert.Equal(Vector2.Zero, defaultNode.Origin.Value);
        Assert.Equal(Vector2.Zero, defaultNode.Position.Value);
        Assert.Equal(0, defaultNode.Rotation.Value);
        Assert.Equal(Vector2.One, defaultNode.Scale.Value);

        Assert.Equal("configured", configuredNode.Name.Value);
        Assert.Equal(new Vector2(2, 3), configuredNode.Origin.Value);
        Assert.Equal(new Vector2(10, 20), configuredNode.Position.Value);
        Assert.Equal(MathF.PI / 2, configuredNode.Rotation.Value);
        Assert.Equal(new Vector2(2, 4), configuredNode.Scale.Value);
    }

    [Fact]
    public void LocalTransform_TransformsOriginToLocalPosition()
    {
        var position = new Vector2(12, 18);
        var node = new Node2D
        {
            Origin = new Vector2(3, 5),
            Position = position,
            Rotation = 0.7f,
            Scale = new Vector2(2, 3),
        };

        var transformedOrigin = Vector2.Transform(node.Origin.Value, node.LocalTransform);

        Assert.Equal(position, transformedOrigin);
    }

    [Fact]
    public void GlobalTransform_ComposesNode2DAncestorsAcrossRegularNodes()
    {
        var root = new Node2D("root") { Position = new Vector2(10, 20), Rotation = MathF.PI / 2 };
        var ordinaryNode = new Node("ordinary") { Parent = root };
        var child = new Node2D("child") { Position = new Vector2(5, 0), Parent = ordinaryNode };

        Assert.Equal(root, ordinaryNode.Parent.Value);
        Assert.Equal(ordinaryNode, child.Parent.Value);
        Assert.Equal(new Vector2(10, 25), child.GlobalPosition);
    }

    [Fact]
    public void GlobalPosition_Set_ConvertsWorldPositionToLocalPosition()
    {
        var parent = new Node2D("parent")
        {
            Position = new Vector2(10, 20),
            Rotation = MathF.PI / 2,
            Scale = new Vector2(2, 2),
        };
        var child = new Node2D("child") { Parent = parent };
        var worldPosition = new Vector2(4, 8);

        child.GlobalPosition = worldPosition;

        Assert.Equal(worldPosition, child.GlobalPosition);
        Assert.NotEqual(worldPosition, child.Position.Value);
    }

    [Fact]
    public void GlobalPosition_Set_WhenParentScaleIsSingular_ThrowsInvalidOperationException()
    {
        var parent = new Node2D("parent") { Scale = Vector2.Zero };
        var child = new Node2D("child") { Parent = parent };

        Assert.Throws<InvalidOperationException>(() => child.GlobalPosition = Vector2.One);
    }

    [Fact]
    public void GlobalRotation_GetAndSet_AccountsForAncestorRotation()
    {
        var parent = new Node2D("parent") { Rotation = 0.4f };
        var child = new Node2D("child") { Parent = parent, Rotation = 0.2f };

        Assert.Equal(0.6f, child.GlobalRotation, precision: 5);

        child.GlobalRotation = 1.1f;

        Assert.Equal(0.7f, child.Rotation.Value, precision: 5);
        Assert.Equal(1.1f, child.GlobalRotation, precision: 5);
    }

    [Fact]
    public void LocalTransform_ReflectsChangesToTransformStores()
    {
        var node = new Node2D();
        var originalTransform = node.LocalTransform;
        node.Position.Value = new Vector2(7, 9);

        var updatedTransform = node.LocalTransform;

        Assert.NotEqual(originalTransform, updatedTransform);
        Assert.Equal(new Vector2(7, 9), Vector2.Transform(Vector2.Zero, updatedTransform));
    }

    [Fact]
    public void Destroy_DestroysTransformStores()
    {
        var node = new Node2D();

        node.Destroy();

        Assert.True(node.Destroyed);
        Assert.True(node.Origin.Destroyed);
        Assert.True(node.Position.Destroyed);
        Assert.True(node.Rotation.Destroyed);
        Assert.True(node.Scale.Destroyed);
    }
}
