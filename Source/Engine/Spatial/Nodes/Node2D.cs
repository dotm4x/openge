using System.Numerics;
using OpenGE.Common.Events;
using OpenGE.Noding;

namespace OpenGE.Spatial.Nodes;

/// <summary>
/// Provides optional values used to initialize a <see cref="Node2D"/>.
/// </summary>
public class Node2DOptions : NodeOptions
{
    /// <summary>
    /// Gets the initial local origin used as the center of scale and rotation.
    /// The default is <see cref="Vector2.Zero"/>.
    /// </summary>
    /// <remarks>
    /// Coordinates use the same application-defined units as <see cref="Position"/>.
    /// </remarks>
    public Vector2 Origin { get; init; } = Vector2.Zero;

    /// <summary>
    /// Gets the initial local position of the origin relative to the nearest
    /// <see cref="Node2D"/> ancestor.
    /// The default is <see cref="Vector2.Zero"/>.
    /// </summary>
    public Vector2 Position { get; init; } = Vector2.Zero;

    /// <summary>
    /// Gets the initial local rotation in radians.
    /// The default is <c>0</c>.
    /// </summary>
    public float Rotation { get; init; } = 0f;

    /// <summary>
    /// Gets the initial local scale factor.
    /// The default is <see cref="Vector2.One"/>.
    /// </summary>
    public Vector2 Scale { get; init; } = Vector2.One;
}

/// <summary>
/// Represents a node positioned in two-dimensional space.
/// </summary>
/// <remarks>
/// Origin, position, rotation, and scale determine the transformation of this
/// node and its <see cref="Node2D"/> descendants. Ordinary nodes between
/// <see cref="Node2D"/> instances organize the tree without contributing a
/// transformation.
/// </remarks>
public class Node2D : Node
{
    private Vector2 _cachedOrigin;
    private Vector2 _cachedPosition;
    private float _cachedRotation;
    private Vector2 _cachedScale;
    private bool _hasCachedLocalTransform;

    /// <summary>
    /// Gets the local origin around which this node and its descendants
    /// are scaled and rotated.
    /// </summary>
    public readonly Store<Vector2> Origin;

    /// <summary>
    /// Gets the position of the origin relative to the nearest
    /// <see cref="Node2D"/> ancestor.
    /// </summary>
    public readonly Store<Vector2> Position;

    /// <summary>
    /// Gets the local rotation, expressed in radians.
    /// </summary>
    public readonly Store<float> Rotation;

    /// <summary>
    /// Gets the local scale.
    /// </summary>
    public readonly Store<Vector2> Scale;

    /// <summary>
    /// Initializes a new instance of the <see cref="Node2D"/> class.
    /// </summary>
    /// <param name="name">The initial name of the node.</param>
    /// <param name="options">
    /// The initial two-dimensional transformation and node values,
    /// or <see langword="null"/> for defaults.
    /// </param>
    public Node2D(string name = "Node2D", Node2DOptions? options = null)
        : base(name, options)
    {
        options ??= new Node2DOptions();

        Origin = new Store<Vector2>(options.Origin);
        Position = new Store<Vector2>(options.Position);
        Rotation = new Store<float>(options.Rotation);
        Scale = new Store<Vector2>(options.Scale);
    }

    /// <summary>
    /// Gets or sets the world position of this node's origin.
    /// </summary>
    /// <remarks>
    /// Getting the position derives it from <see cref="GlobalTransform"/>.
    ///
    /// Setting the position converts the supplied world-space position into
    /// the local coordinate space defined by this node's <see cref="Node2D"/>
    /// ancestors and updates <see cref="Position"/>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the transformation of the <see cref="Node2D"/> ancestors
    /// cannot be inverted.
    /// </exception>
    public Vector2 GlobalPosition
    {
        get => Vector2.Transform(Origin.Get(), GlobalTransform);
        set
        {
            var parentTransform = GetParentTransform();

            if (!Matrix3x2.Invert(parentTransform, out var inverse))
            {
                throw new InvalidOperationException(
                    "The Node2D parent transformation cannot be inverted."
                );
            }

            Position.Set(Vector2.Transform(value, inverse));
        }
    }

    /// <summary>
    /// Gets or sets the world rotation of this node, expressed in radians.
    /// </summary>
    /// <remarks>
    /// Getting the rotation extracts it from <see cref="GlobalTransform"/>.
    ///
    /// Setting the rotation converts the supplied world-space rotation into
    /// the local rotation required relative to this node's
    /// <see cref="Node2D"/> ancestors and updates <see cref="Rotation"/>.
    /// </remarks>
    public float GlobalRotation
    {
        get
        {
            var transform = GlobalTransform;

            return MathF.Atan2(transform.M12, transform.M11);
        }
        set
        {
            var parentTransform = GetParentTransform();

            var parentRotation = MathF.Atan2(parentTransform.M12, parentTransform.M11);

            Rotation.Set(value - parentRotation);
        }
    }

    /// <summary>
    /// Gets this node's transformation after applying its
    /// <see cref="Node2D"/> ancestors.
    /// </summary>
    /// <remarks>
    /// Ancestors that are not <see cref="Node2D"/> instances are skipped.
    /// If there is no <see cref="Node2D"/> ancestor, this node's local
    /// transformation is also its global transformation.
    /// </remarks>
    public Matrix3x2 GlobalTransform
    {
        get
        {
            var transform = LocalTransform;
            var ancestor = Parent.Get();

            while (ancestor is not null)
            {
                if (ancestor is Node2D node)
                    transform *= node.LocalTransform;

                ancestor = ancestor.Parent.Get();
            }

            return transform;
        }
    }

    /// <summary>
    /// Gets the transformation defined by this node's local values.
    /// </summary>
    /// <remarks>
    /// The matrix is recalculated only when origin, position, rotation,
    /// or scale has changed since the previous read.
    /// </remarks>
    public Matrix3x2 LocalTransform
    {
        get
        {
            var origin = Origin.Get();
            var position = Position.Get();
            var rotation = Rotation.Get();
            var scale = Scale.Get();

            if (
                !_hasCachedLocalTransform
                || origin != _cachedOrigin
                || position != _cachedPosition
                || rotation != _cachedRotation
                || scale != _cachedScale
            )
            {
                field =
                    Matrix3x2.CreateTranslation(-origin)
                    * Matrix3x2.CreateScale(scale)
                    * Matrix3x2.CreateRotation(rotation)
                    * Matrix3x2.CreateTranslation(position);

                _cachedOrigin = origin;
                _cachedPosition = position;
                _cachedRotation = rotation;
                _cachedScale = scale;
                _hasCachedLocalTransform = true;
            }

            return field;
        }
    }

    private Matrix3x2 GetParentTransform()
    {
        var transform = Matrix3x2.Identity;
        var ancestor = Parent.Get();

        while (ancestor is not null)
        {
            if (ancestor is Node2D node)
                transform *= node.LocalTransform;

            ancestor = ancestor.Parent.Get();
        }

        return transform;
    }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        base.OnDestroy();

        Origin.Destroy();
        Position.Destroy();
        Rotation.Destroy();
        Scale.Destroy();
    }
}
