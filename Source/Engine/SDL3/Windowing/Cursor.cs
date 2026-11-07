using System.Numerics;
using System.Runtime.InteropServices;
using OpenGE.Graphics.Resources;
using OpenGE.Windowing;
using Image = OpenGE.Graphics.Resources.Image;
using SDL = global::SDL3.SDL;

namespace OpenGE.SDL3.Windowing;

/// <summary>Implements a OpenGE cursor with SDL3 system or RGBA image cursors.</summary>
/// <remarks>
/// SDL cursor selection and visibility are global. Use SDL video-thread access.
/// This object owns its native handle and video lease but borrows its image.
/// </remarks>
public sealed class SDLCursor : Cursor
{
    private IntPtr _native;

    /// <summary>
    /// Initializes a new instance of the <see cref="SDLCursor"/> class.
    /// </summary>
    public SDLCursor(string identifier, CursorOptions? options = null)
        : base(identifier, options)
    {
        VideoRuntime.Acquire();
        try
        {
            _native = CreateNative(Icon.Get(), SystemIcon.Get(), Hotspot.Get());
        }
        catch
        {
            VideoRuntime.Release();
            throw;
        }
    }

    /// <summary>Gets the borrowed native cursor handle, valid until destruction or recreation.</summary>
    public IntPtr Native
    {
        get
        {
            ThrowIfDestroyed();
            return _native;
        }
    }

    private void ApplyVisibility()
    {
        var success = Visible.Get() ? SDL.ShowCursor() : SDL.HideCursor();
        if (!success)
            throw new InvalidOperationException(
                $"Changing cursor visibility failed: {SDL.GetError()}"
            );
    }

    private static IntPtr CreateNative(Image? icon, SystemCursor systemIcon, Vector2 hotspot)
    {
        if (icon is null)
        {
            var native = SDL.CreateSystemCursor(Map(systemIcon));
            if (native == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"Creating SDL system cursor failed: {SDL.GetError()}"
                );
            return native;
        }
        if (icon.Destroyed)
            throw new ObjectDisposedException(nameof(icon));
        if (icon.Format != RasterImageFormat.Rgba8)
            throw new NotSupportedException($"Unsupported cursor image format: {icon.Format}.");
        if (icon.Width > int.MaxValue || icon.Height > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(icon), "Cursor image exceeds SDL limits.");
        if (
            !float.IsFinite(hotspot.X)
            || !float.IsFinite(hotspot.Y)
            || hotspot.X < 0
            || hotspot.Y < 0
            || hotspot.X >= icon.Width
            || hotspot.Y >= icon.Height
            || hotspot.X != MathF.Truncate(hotspot.X)
            || hotspot.Y != MathF.Truncate(hotspot.Y)
        )
            throw new ArgumentOutOfRangeException(
                nameof(hotspot),
                "Hotspot must be a whole pixel within the image."
            );

        var width = checked((int)icon.Width);
        var height = checked((int)icon.Height);
        var pixels = icon.Data.ToArray();
        var pin = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        var surface = IntPtr.Zero;
        try
        {
            surface = SDL.CreateSurfaceFrom(
                width,
                height,
                BitConverter.IsLittleEndian ? SDL.PixelFormat.ABGR8888 : SDL.PixelFormat.RGBA8888,
                pin.AddrOfPinnedObject(),
                checked(width * 4)
            );
            if (surface == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"Creating SDL cursor surface failed: {SDL.GetError()}"
                );
            var native = SDL.CreateColorCursor(surface, (int)hotspot.X, (int)hotspot.Y);
            if (native == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"Creating SDL color cursor failed: {SDL.GetError()}"
                );
            return native;
        }
        finally
        {
            if (surface != IntPtr.Zero)
                SDL.DestroySurface(surface);
            pin.Free();
        }
    }

    private static SDL.SystemCursor Map(SystemCursor icon) =>
        icon switch
        {
            SystemCursor.Default => SDL.SystemCursor.Default,
            SystemCursor.Text => SDL.SystemCursor.Text,
            SystemCursor.Wait => SDL.SystemCursor.Wait,
            SystemCursor.Crosshair => SDL.SystemCursor.Crosshair,
            SystemCursor.Progress => SDL.SystemCursor.Progress,
            SystemCursor.ResizeNorthWestSouthEast => SDL.SystemCursor.NWSEResize,
            SystemCursor.ResizeNorthEastSouthWest => SDL.SystemCursor.NESWResize,
            SystemCursor.ResizeEastWest => SDL.SystemCursor.EWResize,
            SystemCursor.ResizeNorthSouth => SDL.SystemCursor.NSResize,
            SystemCursor.Move => SDL.SystemCursor.Move,
            SystemCursor.NotAllowed => SDL.SystemCursor.NotAllowed,
            SystemCursor.Pointer => SDL.SystemCursor.Pointer,
            _ => throw new ArgumentOutOfRangeException(nameof(icon)),
        };

    private void Recreate()
    {
        var replacement = CreateNative(Icon.Get(), SystemIcon.Get(), Hotspot.Get());
        var previous = _native;
        var active = SDL.GetCursor() == previous;
        if (active && !SDL.SetCursor(replacement))
        {
            SDL.DestroyCursor(replacement);
            throw new InvalidOperationException($"Selecting cursor failed: {SDL.GetError()}");
        }
        _native = replacement;
        SDL.DestroyCursor(previous);
        if (active)
            ApplyVisibility();
    }

    /// <inheritdoc />
    protected override void OnApply()
    {
        if (!SDL.SetCursor(_native))
            throw new InvalidOperationException($"Selecting cursor failed: {SDL.GetError()}");
        ApplyVisibility();
    }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        if (_native != IntPtr.Zero)
        {
            if (SDL.GetCursor() == _native)
                SDL.SetCursor(SDL.GetDefaultCursor());
            SDL.DestroyCursor(_native);
            _native = IntPtr.Zero;
            VideoRuntime.Release();
        }
        base.OnDestroy();
    }

    /// <inheritdoc />
    protected override void OnHotspotChanged(Vector2 hotspot)
    {
        if (Icon.Get() is not null)
            Recreate();
    }

    /// <inheritdoc />
    protected override void OnIconChanged(Image? icon) => Recreate();

    /// <inheritdoc />
    protected override void OnSystemIconChanged(SystemCursor systemIcon)
    {
        if (Icon.Get() is null)
            Recreate();
    }

    /// <inheritdoc />
    protected override void OnVisibilityChanged(bool visible)
    {
        if (SDL.GetCursor() == _native)
            ApplyVisibility();
    }
}
