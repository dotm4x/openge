using System.Numerics;
using OpenGE.Common.Events;
using OpenGE.Noding;

namespace OpenGE.Spatial.Nodes;

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
    /// Gets the local origin around which this node and its descendants are
    /// scaled and rotated.
    /// </summary>
    public Store<Vector2> Origin { get; init; } = new(Vector2.Zero);

    /// <summary>
    /// Gets the position of the origin relative to the nearest <see cref="Node2D"/>
    /// ancestor.
    /// </summary>
    public Store<Vector2> Position { get; init; } = new(Vector2.Zero);

    /// <summary>
    /// Gets the local rotation, expressed in radians.
    /// </summary>
    public Store<float> Rotation { get; init; } = new(0f);

    /// <summary>
    /// Gets the local scale.
    /// </summary>
    public Store<Vector2> Scale { get; init; } = new(Vector2.One);

    /// <summary>
    /// Initializes a new instance of the <see cref="Node2D"/> class.
    /// </summary>
    /// <remarks>
    /// Initial values are assigned through an object initializer, for example
    /// <c>new Node2D { Position = new Vector2(10, 20) }</c>.
    /// </remarks>
    /// <param name="name">
    /// The initial name of the node. The default is <c>"Node2D"</c>.
    /// </param>
    public Node2D(string name = "Node2D")
        : base(name) { }

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
        get => Vector2.Transform(Origin.Value, GlobalTransform);
        set
        {
            var parentTransform = GetParentTransform();

            if (!Matrix3x2.Invert(parentTransform, out var inverse))
            {
                throw new InvalidOperationException(
                    "The Node2D parent transformation cannot be inverted."
                );
            }

            Position.Value = Vector2.Transform(value, inverse);
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

            Rotation.Value = value - parentRotation;
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
            var ancestor = Parent.Value;

            while (ancestor is not null)
            {
                if (ancestor is Node2D node)
                    transform *= node.LocalTransform;

                ancestor = ancestor.Parent.Value;
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
            var origin = Origin.Value;
            var position = Position.Value;
            var rotation = Rotation.Value;
            var scale = Scale.Value;

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
        var ancestor = Parent.Value;

        while (ancestor is not null)
        {
            if (ancestor is Node2D node)
                transform *= node.LocalTransform;

            ancestor = ancestor.Parent.Value;
        }

        return transform;
    }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        Origin.Destroy();
        Position.Destroy();
        Rotation.Destroy();
        Scale.Destroy();

        base.OnDestroy();
    }
}
