using System.Numerics;
using OpenGE.Common.Events;
using OpenGE.Common.Interfaces;
using OpenGE.Common.Lifecycle;
using OpenGE.Graphics.Resources;
using Image = OpenGE.Graphics.Resources.Image;

namespace OpenGE.Windowing;

/// <summary>
/// Provides the initial configuration of a <see cref="Cursor"/>.
/// </summary>
public class CursorOptions
{
    /// <summary>
    /// Gets the custom-image hotspot in pixels.
    /// </summary>
    /// <remarks>
    /// The default is <c>(0, 0)</c>.
    /// The hotspot is used only when <see cref="Icon"/> is not
    /// <see langword="null"/>.
    /// </remarks>
    public Vector2 Hotspot { get; init; } = Vector2.Zero;

    /// <summary>
    /// Gets the custom cursor image, or <see langword="null"/> to use
    /// a system cursor.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="null"/>.
    /// The cursor borrows this image and does not destroy it.
    /// </remarks>
    public Image? Icon { get; init; }

    /// <summary>
    /// Gets the system cursor style used when <see cref="Icon"/> is
    /// <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// The default is <see cref="SystemCursor.Default"/>.
    /// </remarks>
    public SystemCursor SystemIcon { get; init; } = SystemCursor.Default;

    /// <summary>
    /// Gets whether the cursor is initially visible while active.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="true"/>.
    /// </remarks>
    public bool Visible { get; init; } = true;
}

/// <summary>
/// Specifies a platform-provided cursor style.
/// </summary>
public enum SystemCursor
{
    /// <summary>
    /// The platform's default cursor.
    /// </summary>
    Default,

    /// <summary>
    /// A text-selection cursor.
    /// </summary>
    Text,

    /// <summary>
    /// A cursor indicating that an operation is waiting to complete.
    /// </summary>
    Wait,

    /// <summary>
    /// A crosshair cursor.
    /// </summary>
    Crosshair,

    /// <summary>
    /// A cursor indicating that work is occurring while interaction remains possible.
    /// </summary>
    Progress,

    /// <summary>
    /// A diagonal resize cursor running from northwest to southeast.
    /// </summary>
    ResizeNorthWestSouthEast,

    /// <summary>
    /// A diagonal resize cursor running from northeast to southwest.
    /// </summary>
    ResizeNorthEastSouthWest,

    /// <summary>
    /// A horizontal resize cursor.
    /// </summary>
    ResizeEastWest,

    /// <summary>
    /// A vertical resize cursor.
    /// </summary>
    ResizeNorthSouth,

    /// <summary>
    /// A cursor indicating that an object can be moved.
    /// </summary>
    Move,

    /// <summary>
    /// A cursor indicating that the current operation is not allowed.
    /// </summary>
    NotAllowed,

    /// <summary>
    /// A pointing cursor commonly used for links and other interactive elements.
    /// </summary>
    Pointer,
}

/// <summary>
/// Represents a platform-independent cursor backed by either a system style
/// or a custom image.
/// </summary>
/// <remarks>
/// A cursor manages its configuration and lifecycle independently of the
/// platform used to create it.
///
/// Derived implementations provide platform-specific behavior through the
/// protected cursor hooks.
///
/// The cursor borrows its custom <see cref="Image"/> and does not destroy it.
/// </remarks>
public abstract class Cursor : Destroyable, IIdentifiable<string>
{
    /// <summary>
    /// Gets the click position within a custom cursor image, measured in pixels.
    /// </summary>
    /// <remarks>
    /// This value is relevant only while <see cref="Icon"/> contains a custom image.
    /// </remarks>
    public readonly Store<Vector2> Hotspot;

    /// <summary>
    /// Gets the custom cursor image.
    /// </summary>
    /// <remarks>
    /// Set this to <see langword="null"/> to use <see cref="SystemIcon"/>.
    /// The image is borrowed and is not destroyed by this cursor.
    /// </remarks>
    public readonly Store<Image?> Icon;

    /// <summary>
    /// Gets the system cursor style used when <see cref="Icon"/> is
    /// <see langword="null"/>.
    /// </summary>
    public readonly Store<SystemCursor> SystemIcon;

    /// <summary>
    /// Gets whether this cursor should be visible while active.
    /// </summary>
    public readonly Store<bool> Visible;

    /// <summary>
    /// Initializes a new instance of the <see cref="Cursor"/> class.
    /// </summary>
    /// <param name="identifier">
    /// The unique cursor identifier.
    /// </param>
    /// <param name="options">
    /// The initial cursor configuration, or <see langword="null"/> to use
    /// the defaults.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="identifier"/> is empty or consists only
    /// of whitespace.
    /// </exception>
    protected Cursor(string identifier, CursorOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        options ??= new CursorOptions();

        Identifier = identifier;

        Hotspot = new Store<Vector2>(options.Hotspot);
        Icon = new Store<Image?>(options.Icon);
        SystemIcon = new Store<SystemCursor>(options.SystemIcon);
        Visible = new Store<bool>(options.Visible);

        Hotspot.Connect(OnHotspotChanged);
        Icon.Connect(OnIconChanged);
        SystemIcon.Connect(OnSystemIconChanged);
        Visible.Connect(OnVisibilityChanged);
    }

    /// <inheritdoc />
    public string Identifier { get; }

    /// <summary>
    /// Applies this cursor using the underlying platform implementation.
    /// </summary>
    protected abstract void OnApply();

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        Hotspot.Destroy();
        Icon.Destroy();
        SystemIcon.Destroy();
        Visible.Destroy();

        base.OnDestroy();
    }

    /// <summary>
    /// Responds to a change in the custom-image hotspot.
    /// </summary>
    /// <param name="hotspot">
    /// The new hotspot, measured in pixels.
    /// </param>
    protected abstract void OnHotspotChanged(Vector2 hotspot);

    /// <summary>
    /// Responds to a change in the custom cursor image.
    /// </summary>
    /// <param name="icon">
    /// The new custom image, or <see langword="null"/> to use the system cursor.
    /// </param>
    protected abstract void OnIconChanged(Image? icon);

    /// <summary>
    /// Responds to a change in the system cursor style.
    /// </summary>
    /// <param name="systemIcon">
    /// The new system cursor style.
    /// </param>
    protected abstract void OnSystemIconChanged(SystemCursor systemIcon);

    /// <summary>
    /// Responds to a change in cursor visibility.
    /// </summary>
    /// <param name="visible">
    /// Whether the cursor should be visible while active.
    /// </param>
    protected abstract void OnVisibilityChanged(bool visible);

    /// <summary>
    /// Makes this cursor active using its platform-specific implementation.
    /// </summary>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the cursor has already been destroyed.
    /// </exception>
    public void Apply()
    {
        ThrowIfDestroyed();
        OnApply();
    }
}
