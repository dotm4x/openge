using System.Diagnostics;
using System.Numerics;
using OpenGE.Windowing;
using SDL = global::SDL3.SDL;

namespace OpenGE.SDL3.Windowing;

/// <summary>
/// Implements a OpenGE window using SDL3.
/// </summary>
/// <remarks>
/// All native operations must run on the SDL video thread. SDL's event queue is
/// process-wide; events are routed to their window and retained until its next update.
/// </remarks>
public sealed class SDL3Window : Window
{
    private static readonly TimeSpan TitleInterval = TimeSpan.FromMilliseconds(250);
    private static readonly Dictionary<uint, SDL3Window> Windows = [];
    private readonly List<SDL.Event> _events = [];
    private readonly List<SDL.Event> _pendingEvents = [];
    private uint _id;
    private long _lastTitleChange;
    private bool _titlePending;

    static SDL3Window()
    {
        if (OperatingSystem.IsLinux())
            SDL.SetHint("SDL_VIDEO_DRIVER", "x11");
    }

    /// <summary>Creates a window wrapper without opening its native handle.</summary>
    public SDL3Window(WindowOptions? options = null)
        : base(options)
    {
        Cursor.Connect(cursor =>
        {
            if (Native == IntPtr.Zero || cursor is null)
                return;
            if (cursor is not SDLCursor)
                throw new ArgumentException("An SDL window requires an SDLCursor.");
            cursor.Apply();
        });
    }

    /// <summary>Gets SDL events delivered to this window during its last update.</summary>
    /// <remarks>Window events are routed by SDL's associated window. Events without
    /// an associated window are delivered to every open OpenGE window.</remarks>
    public IReadOnlyList<SDL.Event> FrameEvents => _events;

    /// <summary>Gets the borrowed native handle; zero while closed.</summary>
    public IntPtr Native { get; private set; }

    private static void Ensure(bool success, string operation)
    {
        if (!success)
            throw new InvalidOperationException($"{operation} failed: {SDL.GetError()}");
    }

    private static (int X, int Y) PositionToNative(Vector2 value)
    {
        if (
            !float.IsFinite(value.X)
            || !float.IsFinite(value.Y)
            || value.X < int.MinValue
            || value.X > int.MaxValue
            || value.Y < int.MinValue
            || value.Y > int.MaxValue
        )
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Position must contain finite 32-bit coordinates."
            );
        return ((int)value.X, (int)value.Y);
    }

    private static WindowMode ReadMode(SDL.WindowFlags flags)
    {
        if (flags.HasFlag(SDL.WindowFlags.Fullscreen))
            return WindowMode.Fullscreen;
        if (flags.HasFlag(SDL.WindowFlags.Minimized))
            return WindowMode.Minimized;
        if (flags.HasFlag(SDL.WindowFlags.Maximized))
            return WindowMode.Maximized;
        return WindowMode.Normal;
    }

    private static (int Width, int Height) SizeToNative(Vector2 value)
    {
        if (
            !float.IsFinite(value.X)
            || !float.IsFinite(value.Y)
            || value.X < 1
            || value.Y < 1
            || value.X > int.MaxValue
            || value.Y > int.MaxValue
        )
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Size must contain positive 32-bit dimensions."
            );
        return ((int)value.X, (int)value.Y);
    }

    private void Synchronize(bool includeGeometry = true)
    {
        var native = Native;
        if (native == IntPtr.Zero || Destroyed)
            return;
        var flags = SDL.GetWindowFlags(native);
        PublishMode(ReadMode(flags));
        if (Native != native || Destroyed)
            return;
        PublishFocused(flags.HasFlag(SDL.WindowFlags.InputFocus));
        if (Native != native || Destroyed)
            return;
        PublishVisible(!flags.HasFlag(SDL.WindowFlags.Hidden));
        if (Native != native || Destroyed)
            return;
        PublishResizable(flags.HasFlag(SDL.WindowFlags.Resizable));
        if (!includeGeometry || Native != native || Destroyed)
            return;
        Ensure(SDL.GetWindowPosition(native, out var x, out var y), "Reading position");
        PublishPosition(new Vector2(x, y));
        if (Native != native || Destroyed)
            return;
        Ensure(SDL.GetWindowSize(native, out var w, out var h), "Reading size");
        PublishSize(new Vector2(w, h));
    }

    /// <inheritdoc />
    protected override void OnClose()
    {
        if (Native == IntPtr.Zero)
            return;
        Windows.Remove(_id);
        var native = Native;
        Native = IntPtr.Zero;
        _id = 0;
        _events.Clear();
        _pendingEvents.Clear();
        _titlePending = false;
        SDL.DestroyWindow(native);
        VideoRuntime.Release();
    }

    /// <inheritdoc />
    protected override void OnModeChanged(WindowMode mode)
    {
        if (!ShouldApply(Mode, mode) || Native == IntPtr.Zero)
            return;
        var flags = SDL.GetWindowFlags(Native);
        if (mode != WindowMode.Fullscreen && flags.HasFlag(SDL.WindowFlags.Fullscreen))
            Ensure(SDL.SetWindowFullscreen(Native, false), "Leaving fullscreen");
        switch (mode)
        {
            case WindowMode.Normal:
                Ensure(SDL.RestoreWindow(Native), "Restoring window");
                break;
            case WindowMode.Minimized:
                Ensure(SDL.MinimizeWindow(Native), "Minimizing window");
                break;
            case WindowMode.Maximized:
                Ensure(SDL.MaximizeWindow(Native), "Maximizing window");
                break;
            case WindowMode.Fullscreen:
                Ensure(SDL.SetWindowFullscreen(Native, true), "Entering fullscreen");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    /// <inheritdoc />
    protected override void OnMove(Vector2 position)
    {
        if (!ShouldApply(Position, position))
            return;
        var (x, y) = PositionToNative(position);
        if (Native == IntPtr.Zero || SDL.GetWindowFlags(Native).HasFlag(SDL.WindowFlags.Fullscreen))
            return;
        Ensure(SDL.SetWindowPosition(Native, x, y), "Moving window");
    }

    /// <inheritdoc />
    protected override void OnOpen()
    {
        if (Native != IntPtr.Zero)
            return;
        var (width, height) = SizeToNative(Size.Get());
        var flags = (SDL.WindowFlags)0;
        if (Resizable.Get())
            flags |= SDL.WindowFlags.Resizable;
        if (!Visible.Get())
            flags |= SDL.WindowFlags.Hidden;
        flags |= Mode.Get() switch
        {
            WindowMode.Normal => 0,
            WindowMode.Minimized => SDL.WindowFlags.Minimized,
            WindowMode.Maximized => SDL.WindowFlags.Maximized,
            WindowMode.Fullscreen => SDL.WindowFlags.Fullscreen,
            _ => throw new ArgumentOutOfRangeException(nameof(Mode)),
        };
        VideoRuntime.Acquire();
        try
        {
            Native = SDL.CreateWindow(Title.Get(), width, height, flags);
            if (Native == IntPtr.Zero)
                throw new InvalidOperationException($"Creating window failed: {SDL.GetError()}");
            _id = SDL.GetWindowID(Native);
            Windows.Add(_id, this);
            if (HasInitialPosition && Mode.Get() == WindowMode.Normal)
            {
                var (x, y) = PositionToNative(Position.Get());
                Ensure(SDL.SetWindowPosition(Native, x, y), "Setting initial position");
            }
            _lastTitleChange = Stopwatch.GetTimestamp();
            _titlePending = false;
            Synchronize();
            if (Cursor.Get() is { } cursor)
            {
                if (cursor is not SDLCursor)
                    throw new ArgumentException("An SDL window requires an SDLCursor.");
                cursor.Apply();
            }
        }
        catch
        {
            if (Native == IntPtr.Zero)
                VideoRuntime.Release();
            else
                OnClose();
            throw;
        }
    }

    /// <inheritdoc />
    protected override void OnProcess()
    {
        _events.Clear();
        if (Native == IntPtr.Zero)
            return;
        while (SDL.PollEvent(out var e))
        {
            var type = (SDL.EventType)e.Type;
            if (type == SDL.EventType.Quit)
            {
                foreach (var window in Windows.Values.ToArray())
                    window.Close();
                break;
            }
            var native = SDL.GetWindowFromEvent(in e);
            if (native == IntPtr.Zero)
            {
                foreach (var window in Windows.Values)
                    window._pendingEvents.Add(e);
                continue;
            }
            if (!Windows.TryGetValue(SDL.GetWindowID(native), out var target))
                continue;
            target._pendingEvents.Add(e);
            if (type == SDL.EventType.WindowCloseRequested)
            {
                target.Close();
                if (Native == IntPtr.Zero)
                    break;
                continue;
            }
            if (
                type
                is SDL.EventType.WindowMoved
                    or SDL.EventType.WindowResized
                    or SDL.EventType.WindowPixelSizeChanged
                    or SDL.EventType.WindowShown
                    or SDL.EventType.WindowHidden
                    or SDL.EventType.WindowFocusGained
                    or SDL.EventType.WindowFocusLost
                    or SDL.EventType.WindowMinimized
                    or SDL.EventType.WindowMaximized
                    or SDL.EventType.WindowRestored
                    or SDL.EventType.WindowEnterFullscreen
                    or SDL.EventType.WindowLeaveFullscreen
            )
                target.Synchronize();
            if (Native == IntPtr.Zero)
                break;
        }
        if (Native == IntPtr.Zero)
            return;
        _events.AddRange(_pendingEvents);
        _pendingEvents.Clear();
        var now = Stopwatch.GetTimestamp();
        if (_titlePending && Stopwatch.GetElapsedTime(_lastTitleChange, now) >= TitleInterval)
        {
            var requested = Title.Get();
            if (SDL.GetWindowTitle(Native) != requested)
                Ensure(SDL.SetWindowTitle(Native, requested), "Changing title");
            _titlePending = false;
            _lastTitleChange = Stopwatch.GetTimestamp();
        }
    }

    /// <inheritdoc />
    protected override void OnResizableChanged(bool resizable)
    {
        if (!ShouldApply(Resizable, resizable) || Native == IntPtr.Zero)
            return;
        Ensure(SDL.SetWindowResizable(Native, resizable), "Changing resizability");
    }

    /// <inheritdoc />
    protected override void OnResize(Vector2 size)
    {
        if (!ShouldApply(Size, size))
            return;
        var (width, height) = SizeToNative(size);
        if (Native == IntPtr.Zero || SDL.GetWindowFlags(Native).HasFlag(SDL.WindowFlags.Fullscreen))
            return;
        Ensure(SDL.SetWindowSize(Native, width, height), "Resizing window");
    }

    /// <inheritdoc />
    protected override void OnTitleChanged(string title)
    {
        if (ShouldApply(Title, title) && Native != IntPtr.Zero)
            _titlePending = true;
    }

    /// <inheritdoc />
    protected override void OnVisibilityChanged(bool visible)
    {
        if (!ShouldApply(Visible, visible) || Native == IntPtr.Zero)
            return;
        if (visible)
            Ensure(SDL.ShowWindow(Native), "Showing window");
        else
            Ensure(SDL.HideWindow(Native), "Hiding window");
    }
}
