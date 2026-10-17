using System.Numerics;
using OpenGE.Windowing;
using Raylib_cs;

namespace OpenGE.Raylib.Windowing;

/// <summary>
/// Implements an OpenGE window using Raylib.
/// </summary>
public sealed class RaylibWindow : Window
{
    private static RaylibWindow? _active;
    private int _threadId;

    public RaylibWindow(WindowOptions? options = null)
        : base(options) { }

    private bool IsReady => ReferenceEquals(_active, this) && Raylib_cs.Raylib.IsWindowReady();

    private void CheckThread()
    {
        if (_threadId != 0 && Environment.CurrentManagedThreadId != _threadId)
            throw new InvalidOperationException(
                "Raylib window operations must run on the window thread."
            );
    }

    private static (int X, int Y) ToPosition(Vector2 value)
    {
        if (
            !float.IsFinite(value.X)
            || !float.IsFinite(value.Y)
            || value.X < int.MinValue
            || value.X > int.MaxValue
            || value.Y < int.MinValue
            || value.Y > int.MaxValue
        )
            throw new ArgumentOutOfRangeException(nameof(value));

        return ((int)value.X, (int)value.Y);
    }

    private static (int Width, int Height) ToSize(Vector2 value)
    {
        if (
            !float.IsFinite(value.X)
            || !float.IsFinite(value.Y)
            || value.X < 1
            || value.Y < 1
            || value.X > int.MaxValue
            || value.Y > int.MaxValue
        )
            throw new ArgumentOutOfRangeException(nameof(value));

        return ((int)value.X, (int)value.Y);
    }

    private void Synchronize()
    {
        if (!IsReady)
            return;

        PublishMode(
            Raylib_cs.Raylib.IsWindowFullscreen() ? WindowMode.Fullscreen
            : Raylib_cs.Raylib.IsWindowMinimized() ? WindowMode.Minimized
            : Raylib_cs.Raylib.IsWindowMaximized() ? WindowMode.Maximized
            : WindowMode.Normal
        );

        PublishFocused(Raylib_cs.Raylib.IsWindowFocused());
        PublishVisible(!Raylib_cs.Raylib.IsWindowHidden());
        PublishResizable(Raylib_cs.Raylib.IsWindowState(ConfigFlags.ResizableWindow));
        PublishPosition(Raylib_cs.Raylib.GetWindowPosition());
        PublishSize(
            new Vector2(Raylib_cs.Raylib.GetScreenWidth(), Raylib_cs.Raylib.GetScreenHeight())
        );
    }

    /// <inheritdoc />
    protected override void OnOpen()
    {
        if (_active is not null || Raylib_cs.Raylib.IsWindowReady())
            throw new InvalidOperationException("Only one Raylib window can be active.");

        if (Cursor.Get() is not null)
            throw new NotSupportedException("RaylibCursor has not been implemented.");

        var (width, height) = ToSize(Size.Get());

        var flags = (ConfigFlags)0;

        if (Resizable.Get())
            flags |= ConfigFlags.ResizableWindow;

        if (!Visible.Get())
            flags |= ConfigFlags.HiddenWindow;

        flags |= Mode.Get() switch
        {
            WindowMode.Normal => (ConfigFlags)0,
            WindowMode.Minimized => ConfigFlags.MinimizedWindow,
            WindowMode.Maximized => ConfigFlags.MaximizedWindow,
            WindowMode.Fullscreen => ConfigFlags.FullscreenMode,
            _ => throw new ArgumentOutOfRangeException(nameof(Mode)),
        };

        _active = this;

        try
        {
            Raylib_cs.Raylib.SetConfigFlags(flags);
            Raylib_cs.Raylib.InitWindow(width, height, Title.Get());

            if (!Raylib_cs.Raylib.IsWindowReady())
                throw new InvalidOperationException("Failed to initialize the Raylib window.");

            _threadId = Environment.CurrentManagedThreadId;

            Raylib_cs.Raylib.SetExitKey((KeyboardKey)0);

            if (HasInitialPosition && Mode.Get() == WindowMode.Normal)
            {
                var (x, y) = ToPosition(Position.Get());
                Raylib_cs.Raylib.SetWindowPosition(x, y);
            }

            Synchronize();
        }
        catch
        {
            if (Raylib_cs.Raylib.IsWindowReady())
                Raylib_cs.Raylib.CloseWindow();

            _active = null;
            _threadId = 0;
            throw;
        }
    }

    /// <inheritdoc />
    protected override void OnClose()
    {
        CheckThread();

        if (!ReferenceEquals(_active, this))
            return;

        try
        {
            if (Raylib_cs.Raylib.IsWindowReady())
                Raylib_cs.Raylib.CloseWindow();
        }
        finally
        {
            _active = null;
            _threadId = 0;
        }
    }

    /// <inheritdoc />
    protected override void OnProcess()
    {
        CheckThread();

        if (!IsReady)
            return;

        if (Raylib_cs.Raylib.WindowShouldClose())
        {
            Close();
            return;
        }

        Synchronize();
    }

    /// <inheritdoc />
    protected override void OnModeChanged(WindowMode mode)
    {
        if (!ShouldApply(Mode, mode) || !IsReady)
            return;

        CheckThread();

        if (mode == WindowMode.Fullscreen)
        {
            if (!Raylib_cs.Raylib.IsWindowFullscreen())
                Raylib_cs.Raylib.ToggleFullscreen();

            return;
        }

        if (Raylib_cs.Raylib.IsWindowFullscreen())
            Raylib_cs.Raylib.ToggleFullscreen();

        switch (mode)
        {
            case WindowMode.Normal:
                Raylib_cs.Raylib.RestoreWindow();
                break;

            case WindowMode.Minimized:
                Raylib_cs.Raylib.MinimizeWindow();
                break;

            case WindowMode.Maximized:
                Raylib_cs.Raylib.MaximizeWindow();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    /// <inheritdoc />
    protected override void OnMove(Vector2 position)
    {
        if (!ShouldApply(Position, position) || !IsReady)
            return;

        CheckThread();

        if (!Raylib_cs.Raylib.IsWindowFullscreen())
        {
            var (x, y) = ToPosition(position);
            Raylib_cs.Raylib.SetWindowPosition(x, y);
        }
    }

    /// <inheritdoc />
    protected override void OnResizableChanged(bool resizable)
    {
        if (!ShouldApply(Resizable, resizable) || !IsReady)
            return;

        CheckThread();

        if (resizable)
            Raylib_cs.Raylib.SetWindowState(ConfigFlags.ResizableWindow);
        else
            Raylib_cs.Raylib.ClearWindowState(ConfigFlags.ResizableWindow);
    }

    /// <inheritdoc />
    protected override void OnResize(Vector2 size)
    {
        if (!ShouldApply(Size, size) || !IsReady)
            return;

        CheckThread();

        if (!Raylib_cs.Raylib.IsWindowFullscreen())
        {
            var (width, height) = ToSize(size);
            Raylib_cs.Raylib.SetWindowSize(width, height);
        }
    }

    /// <inheritdoc />
    protected override void OnTitleChanged(string title)
    {
        if (!ShouldApply(Title, title) || !IsReady)
            return;

        CheckThread();
        Raylib_cs.Raylib.SetWindowTitle(title);
    }

    /// <inheritdoc />
    protected override void OnVisibilityChanged(bool visible)
    {
        if (!ShouldApply(Visible, visible) || !IsReady)
            return;

        CheckThread();

        if (visible)
            Raylib_cs.Raylib.ClearWindowState(ConfigFlags.HiddenWindow);
        else
            Raylib_cs.Raylib.SetWindowState(ConfigFlags.HiddenWindow);
    }
}
