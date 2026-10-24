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
        var configuredNode = new Node2D(
            "configured",
            new Node2DOptions
            {
                Origin = new Vector2(2, 3),
                Position = new Vector2(10, 20),
                Rotation = MathF.PI / 2,
                Scale = new Vector2(2, 4),
            }
        );

        Assert.Equal(Vector2.Zero, defaultNode.Origin.Get());
        Assert.Equal(Vector2.Zero, defaultNode.Position.Get());
        Assert.Equal(0, defaultNode.Rotation.Get());
        Assert.Equal(Vector2.One, defaultNode.Scale.Get());

        Assert.Equal("configured", configuredNode.Name.Get());
        Assert.Equal(new Vector2(2, 3), configuredNode.Origin.Get());
        Assert.Equal(new Vector2(10, 20), configuredNode.Position.Get());
        Assert.Equal(MathF.PI / 2, configuredNode.Rotation.Get());
        Assert.Equal(new Vector2(2, 4), configuredNode.Scale.Get());
    }

    [Fact]
    public void LocalTransform_TransformsOriginToLocalPosition()
    {
        var position = new Vector2(12, 18);
        var node = new Node2D(
            options: new Node2DOptions
            {
                Origin = new Vector2(3, 5),
                Position = position,
                Rotation = 0.7f,
                Scale = new Vector2(2, 3),
            }
        );

        var transformedOrigin = Vector2.Transform(node.Origin.Get(), node.LocalTransform);

        Assert.Equal(position, transformedOrigin);
    }

    [Fact]
    public void GlobalTransform_ComposesNode2DAncestorsAcrossRegularNodes()
    {
        var root = new Node2D(
            "root",
            new Node2DOptions { Position = new Vector2(10, 20), Rotation = MathF.PI / 2 }
        );
        var ordinaryNode = new Node("ordinary", new NodeOptions { Parent = root });
        var child = new Node2D(
            "child",
            new Node2DOptions { Position = new Vector2(5, 0), Parent = ordinaryNode }
        );

        Assert.Equal(root, ordinaryNode.Parent.Get());
        Assert.Equal(ordinaryNode, child.Parent.Get());
        Assert.Equal(new Vector2(10, 25), child.GlobalPosition);
    }

    [Fact]
    public void GlobalPosition_Set_ConvertsWorldPositionToLocalPosition()
    {
        var parent = new Node2D(
            "parent",
            new Node2DOptions
            {
                Position = new Vector2(10, 20),
                Rotation = MathF.PI / 2,
                Scale = new Vector2(2, 2),
            }
        );
        var child = new Node2D("child", new Node2DOptions { Parent = parent });
        var worldPosition = new Vector2(4, 8);

        child.GlobalPosition = worldPosition;

        Assert.Equal(worldPosition, child.GlobalPosition);
        Assert.NotEqual(worldPosition, child.Position.Get());
    }

    [Fact]
    public void GlobalPosition_Set_WhenParentScaleIsSingular_ThrowsInvalidOperationException()
    {
        var parent = new Node2D("parent", new Node2DOptions { Scale = Vector2.Zero });
        var child = new Node2D("child", new Node2DOptions { Parent = parent });

        Assert.Throws<InvalidOperationException>(() => child.GlobalPosition = Vector2.One);
    }

    [Fact]
    public void GlobalRotation_GetAndSet_AccountsForAncestorRotation()
    {
        var parent = new Node2D("parent", new Node2DOptions { Rotation = 0.4f });
        var child = new Node2D("child", new Node2DOptions { Parent = parent, Rotation = 0.2f });

        Assert.Equal(0.6f, child.GlobalRotation, precision: 5);

        child.GlobalRotation = 1.1f;

        Assert.Equal(0.7f, child.Rotation.Get(), precision: 5);
        Assert.Equal(1.1f, child.GlobalRotation, precision: 5);
    }

    [Fact]
    public void LocalTransform_ReflectsChangesToTransformStores()
    {
        var node = new Node2D();
        var originalTransform = node.LocalTransform;
        node.Position.Set(new Vector2(7, 9));

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
