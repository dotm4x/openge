using System.Numerics;
using OpenGE.Common.Collections;
using OpenGE.Common.Events;
using OpenGE.Common.Interfaces;
using OpenGE.Common.Lifecycle;
using OpenGE.Common.Primitives;
using OpenGE.Scheduling;
using OpenGE.Scheduling.Interfaces;

namespace OpenGE.Windowing;

/// <summary>
/// Defines the initial configuration of a <see cref="Window"/>.
/// </summary>
public class WindowOptions : IIdentifiable<string>
{
    private static readonly string[] DefaultTitleMessages =
    [
        "Reality is loading…",
        "Somewhere between pixels and possibility.",
        "If lost, check the scene tree.",
        "The window is not the whole world.",
        "Objects may appear more stable than they are.",
        "Rendering your imagination.",
        "This frame took the scenic route.",
        "No mirage is final.",
        "Is this window real, or just well rendered?",
        "The desert is rendering. Please wait.",
        "A little illusion, a lot of C#.",
        "Look twice. It may be a node.",
        "The horizon is outside the client area.",
        "Reality has entered fullscreen mode.",
        "The update loop says hello.",
        "Somewhere, a frame is being skipped.",
        "It works on this machine.",
        "The bug is probably in another module.",
        "Compiling the laws of reality.",
        "One more feature, then we ship.",
        "Have you tried restarting the scene?",
        "The window is open. The TODO list is not.",
        "There are no bugs, only unexpected features.",
        "This title was selected at runtime.",
        "Your pixels are in another window.",
        "Please wait while the developer finds the semicolon.",
        "A wild NullReferenceException appeared!",
        "This frame is brought to you by delta time.",
        "The scheduler is doing its best.",
        "Somewhere, C# is compiling.",
        "Warning: reality may contain experimental features.",
        "Drawing outside the lines since this frame.",
        "A new node has entered the scene.",
        "The scene tree has a nice view from here.",
        "The pixels are all present and accounted for.",
        "A window into another coordinate system.",
        "Presenting one frame at a time.",
        "The render target knows what you did.",
        "The viewport is looking back.",
        "Everything is relative to the parent.",
        "Your frame has been queued for rendering.",
        "This isn't the frame you're looking for.",
        "It's dangerous to render alone.",
        "A wild window appeared!",
        "The cake is probably in the assets folder.",
        "A companion cube was added to the scene.",
        "One does not simply skip the update loop.",
        "May your frame time be ever in your favor.",
        "The cake is a compile-time constant.",
        "A block of reality has been placed.",
        "Hallownest would make a very large scene graph.",
        "The window has no map. Check the scene tree.",
        "The simulation is starting to feel suspicious.",
        "There is probably a spoon somewhere.",
        "The pixels must flow.",
    ];

    /// <summary>
    /// Gets the cursors to register initially.
    /// </summary>
    /// <remarks>
    /// The default is an empty sequence. The window stores these references
    /// but does not own the cursors.
    /// </remarks>
    public IEnumerable<Cursor> Cursors { get; init; } = [];

    /// <summary>
    /// Gets the initial display mode.
    /// </summary>
    /// <remarks>
    /// The default is <see cref="WindowMode.Normal"/>.
    /// </remarks>
    public WindowMode Mode { get; init; } = WindowMode.Normal;

    /// <summary>
    /// Gets the initial client-area position in screen coordinates.
    /// </summary>
    /// <remarks>
    /// When <see langword="null"/>, the platform chooses the initial position.
    /// Some platforms may ignore an explicitly supplied position.
    /// </remarks>
    public Vector2? Position { get; init; }

    /// <summary>
    /// Gets whether the user can resize the window.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="true"/>.
    /// </remarks>
    public bool Resizable { get; init; } = true;

    /// <summary>
    /// Gets the initial client-area size in pixels.
    /// </summary>
    /// <remarks>
    /// The default is <c>(800, 600)</c>.
    /// </remarks>
    public Vector2 Size { get; init; } = new(800, 600);

    /// <summary>
    /// Gets the initial title of the window.
    /// </summary>
    /// <remarks>
    /// The default is <c>"OpenGE"</c> followed by a randomly selected message.
    /// </remarks>
    public string Title { get; init; } =
        $"OpenGE - {DefaultTitleMessages[Random.Shared.Next(DefaultTitleMessages.Length)]}";

    /// <summary>
    /// Gets whether the window is initially visible.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="true"/>.
    /// </remarks>
    public bool Visible { get; init; } = true;

    /// <inheritdoc />
    /// <remarks>
    /// The default is <c>"window"</c>.
    /// </remarks>
    public string Identifier { get; init; } = "window";
}

/// <summary>
/// Specifies the display mode of a window.
/// </summary>
public enum WindowMode
{
    /// <summary>
    /// The window is displayed normally.
    /// </summary>
    Normal,

    /// <summary>
    /// The window is minimized.
    /// </summary>
    Minimized,

    /// <summary>
    /// The window is maximized within the available desktop area.
    /// </summary>
    Maximized,

    /// <summary>
    /// The window occupies an entire display.
    /// </summary>
    Fullscreen,
}

/// <summary>
/// Represents a platform-independent application window.
/// </summary>
/// <remarks>
/// A window manages its state, lifecycle, events, cursors, and requested
/// properties independently of the platform used to create it.
///
/// Derived implementations provide the platform-specific behavior through
/// the protected window hooks.
///
/// Writable stores represent both requested and current window state.
/// Derived implementations may publish state changes reported by the
/// underlying platform.
///
/// Registered cursors are borrowed and may be shared between windows.
/// The window owns its stores and cursor collection, but not the selected
/// or registered cursor objects.
/// </remarks>
public abstract class Window : Destroyable, IUpdatable, IIdentifiable<string>
{
    private readonly Signal<Unit> _closing = new();

    private readonly Store<bool> _focused = new(false);
    private readonly Store<bool> _opened = new(false);

    private object? _publishingStore;
    private object? _publishingValue;

    /// <summary>
    /// Gets whether an initial position was explicitly configured.
    /// </summary>
    protected readonly bool HasInitialPosition;

    /// <summary>
    /// Gets the cursor selected for this window,
    /// or <see langword="null"/> when no cursor is selected.
    /// </summary>
    public readonly Store<Cursor?> Cursor;

    /// <summary>
    /// Gets the identifiable set of registered cursors.
    /// </summary>
    /// <remarks>
    /// Registered cursors are borrowed and are not destroyed by the window.
    /// </remarks>
    public readonly IdentifiableSet<string, Cursor> Cursors = [];

    /// <summary>
    /// Gets a read-only store indicating whether the window currently has input focus.
    /// </summary>
    public readonly IReadOnlyStore<bool> Focused;

    /// <summary>
    /// Gets the store that controls and reports the window's display mode.
    /// </summary>
    public readonly Store<WindowMode> Mode;

    /// <summary>
    /// Gets a read-only store indicating whether the window is open.
    /// </summary>
    public readonly IReadOnlyStore<bool> Opened;

    /// <summary>
    /// Gets the store that controls and reports the position of the window's client area.
    /// </summary>
    /// <remarks>
    /// The position is expressed in screen coordinates.
    /// </remarks>
    public readonly Store<Vector2> Position;

    /// <summary>
    /// Gets the store that controls and reports whether the window is resizable.
    /// </summary>
    public readonly Store<bool> Resizable;

    /// <summary>
    /// Gets the store that controls and reports the size of the window's client area.
    /// </summary>
    /// <remarks>
    /// The size is expressed in pixels.
    /// </remarks>
    public readonly Store<Vector2> Size;

    /// <summary>
    /// Gets the store that controls and reports the title of the window.
    /// </summary>
    public readonly Store<string> Title;

    /// <summary>
    /// Gets the store that controls and reports whether the window is visible.
    /// </summary>
    public readonly Store<bool> Visible;

    /// <summary>
    /// Initializes a new instance of the <see cref="Window"/> class.
    /// </summary>
    /// <param name="options">
    /// The initial window configuration, or <see langword="null"/> to use the defaults.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when the configured identifier is empty or consists only of whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the initial cursor collection contains duplicate identifiers.
    /// </exception>
    protected Window(WindowOptions? options = null)
    {
        options ??= new WindowOptions();

        if (string.IsNullOrWhiteSpace(options.Identifier))
            throw new ArgumentException("Window identifier cannot be empty.", nameof(options));

        Identifier = options.Identifier;
        HasInitialPosition = options.Position.HasValue;

        Focused = _focused;
        Opened = _opened;

        foreach (var cursor in options.Cursors)
            Cursors.Add(cursor);

        Cursor = new Store<Cursor?>(Cursors.FirstOrDefault());

        Mode = new Store<WindowMode>(options.Mode);
        Position = new Store<Vector2>(options.Position ?? Vector2.Zero);
        Resizable = new Store<bool>(options.Resizable);
        Size = new Store<Vector2>(options.Size);
        Title = new Store<string>(options.Title);
        Visible = new Store<bool>(options.Visible);

        Mode.Connect(OnModeChanged, true);
        Position.Connect(OnMove, true);
        Resizable.Connect(OnResizableChanged, true);
        Size.Connect(OnResize, true);
        Title.Connect(OnTitleChanged, true);
        Visible.Connect(OnVisibilityChanged, true);
    }

    /// <summary>
    /// Gets the event fired immediately before the underlying window is closed.
    /// </summary>
    /// <remarks>
    /// Subscribers can release resources associated with the underlying window
    /// while the platform-specific window is still available.
    /// </remarks>
    public IReadOnlyEvent<Unit> OnClosing => _closing;

    /// <inheritdoc />
    public string Identifier { get; }

    /// <inheritdoc />
    public virtual void Update(UpdateContext context)
    {
        ThrowIfDestroyed();

        if (_opened.Get())
            OnProcess();
    }

    /// <summary>
    /// Releases the underlying platform window.
    /// </summary>
    protected abstract void OnClose();

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        if (_opened.Get())
        {
            _closing.Fire(Unit.Value);
            OnClose();
        }

        _focused.Destroy();
        _opened.Destroy();
        _closing.Destroy();

        Mode.Destroy();
        Position.Destroy();
        Resizable.Destroy();
        Size.Destroy();
        Title.Destroy();
        Visible.Destroy();
        Cursor.Destroy();
        Cursors.Destroy();

        base.OnDestroy();
    }

    /// <summary>
    /// Applies a requested display mode to the underlying platform window.
    /// </summary>
    /// <param name="mode">The requested display mode.</param>
    protected abstract void OnModeChanged(WindowMode mode);

    /// <summary>
    /// Applies a requested client-area position to the underlying platform window.
    /// </summary>
    /// <param name="position">The requested position.</param>
    protected abstract void OnMove(Vector2 position);

    /// <summary>
    /// Creates and initializes the underlying platform window.
    /// </summary>
    protected abstract void OnOpen();

    /// <summary>
    /// Processes platform-specific events and synchronizes window state.
    /// </summary>
    protected abstract void OnProcess();

    /// <summary>
    /// Applies the requested resizability state to the underlying platform window.
    /// </summary>
    /// <param name="resizable">Whether resizing should be allowed.</param>
    protected abstract void OnResizableChanged(bool resizable);

    /// <summary>
    /// Applies a requested client-area size to the underlying platform window.
    /// </summary>
    /// <param name="size">The requested client-area size.</param>
    protected abstract void OnResize(Vector2 size);

    /// <summary>
    /// Applies a requested title to the underlying platform window.
    /// </summary>
    /// <param name="title">The requested title.</param>
    protected abstract void OnTitleChanged(string title);

    /// <summary>
    /// Applies the requested visibility state to the underlying platform window.
    /// </summary>
    /// <param name="visible">Whether the window should be visible.</param>
    protected abstract void OnVisibilityChanged(bool visible);

    /// <summary>
    /// Publishes a value reported by the underlying platform without treating
    /// the resulting store notification as a new platform request.
    /// </summary>
    /// <typeparam name="TValue">The type of value being published.</typeparam>
    /// <param name="store">The store receiving the platform value.</param>
    /// <param name="value">The value reported by the platform.</param>
    protected void Publish<TValue>(Store<TValue> store, TValue value)
    {
        ThrowIfDestroyed();

        if (EqualityComparer<TValue>.Default.Equals(store.Get(), value))
            return;

        var previousStore = _publishingStore;
        var previousValue = _publishingValue;

        _publishingStore = store;
        _publishingValue = value;

        try
        {
            store.Set(value);
        }
        finally
        {
            _publishingStore = previousStore;
            _publishingValue = previousValue;
        }
    }

    /// <summary>
    /// Publishes the current focus state reported by the underlying platform.
    /// </summary>
    /// <param name="focused">Whether the window currently has input focus.</param>
    protected void PublishFocused(bool focused)
    {
        Publish(_focused, focused);
    }

    /// <summary>
    /// Publishes the current display mode reported by the underlying platform.
    /// </summary>
    /// <param name="mode">The current display mode.</param>
    protected void PublishMode(WindowMode mode)
    {
        Publish(Mode, mode);
    }

    /// <summary>
    /// Publishes the current client-area position reported by the underlying platform.
    /// </summary>
    /// <param name="position">The current client-area position.</param>
    protected void PublishPosition(Vector2 position)
    {
        Publish(Position, position);
    }

    /// <summary>
    /// Publishes the current resizability state reported by the underlying platform.
    /// </summary>
    /// <param name="resizable">Whether the window is currently resizable.</param>
    protected void PublishResizable(bool resizable)
    {
        Publish(Resizable, resizable);
    }

    /// <summary>
    /// Publishes the current client-area size reported by the underlying platform.
    /// </summary>
    /// <param name="size">The current client-area size.</param>
    protected void PublishSize(Vector2 size)
    {
        Publish(Size, size);
    }

    /// <summary>
    /// Publishes the current title reported by the underlying platform.
    /// </summary>
    /// <param name="title">The current window title.</param>
    protected void PublishTitle(string title)
    {
        Publish(Title, title);
    }

    /// <summary>
    /// Publishes the current visibility state reported by the underlying platform.
    /// </summary>
    /// <param name="visible">Whether the window is currently visible.</param>
    protected void PublishVisible(bool visible)
    {
        Publish(Visible, visible);
    }

    /// <summary>
    /// Determines whether a store notification represents a requested change
    /// that should be applied to the underlying platform.
    /// </summary>
    /// <typeparam name="TValue">The type of value being inspected.</typeparam>
    /// <param name="store">The store that produced the notification.</param>
    /// <param name="value">The notified value.</param>
    /// <returns>
    /// <see langword="true"/> when the value should be applied to the underlying
    /// platform; otherwise, <see langword="false"/>.
    /// </returns>
    protected bool ShouldApply<TValue>(Store<TValue> store, TValue value)
    {
        if (!EqualityComparer<TValue>.Default.Equals(store.Get(), value))
            return false;

        return !(
            ReferenceEquals(_publishingStore, store)
            && EqualityComparer<TValue>.Default.Equals((TValue)_publishingValue!, value)
        );
    }

    /// <summary>
    /// Closes the window if it is currently open.
    /// </summary>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the window has already been destroyed.
    /// </exception>
    public void Close()
    {
        ThrowIfDestroyed();

        if (!_opened.Get())
            return;

        _closing.Fire(Unit.Value);
        OnClose();

        if (Destroyed)
            return;

        _focused.Set(false);
        _opened.Set(false);
    }

    /// <summary>
    /// Opens the window if it is not already open.
    /// </summary>
    /// <exception cref="DestroyedObjectException">
    /// Thrown when the window has already been destroyed.
    /// </exception>
    public void Open()
    {
        ThrowIfDestroyed();

        if (_opened.Get())
            return;

        OnOpen();

        if (!Destroyed)
            _opened.Set(true);
    }
}
