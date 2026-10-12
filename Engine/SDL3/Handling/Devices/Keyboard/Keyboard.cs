using Mirage.Handling;
using Mirage.Handling.Devices;
using Mirage.Handling.Devices.Keyboard;
using Mirage.SDL3.Windowing;
using SDL = global::SDL3.SDL;

namespace Mirage.SDL3.Handling.Devices.Keyboard;

/// <summary>
/// Processes keyboard input using SDL3.
/// </summary>
/// <param name="events">The keyboard events to register initially.</param>
public class SDL3Keyboard(params InputEvent<KeyboardEventPayload, KeyboardKey>[] events)
    : Mirage.Handling.Devices.Keyboard.Keyboard(events)
{
    /// <inheritdoc />
    protected override void OnProcess(InputContext context)
    {
        EnsureInitialized();

        if (context is not SDL3InputContext sdlContext)
        {
            throw new InvalidOperationException(
                $"{nameof(SDL3Keyboard)} requires an {nameof(SDL3InputContext)}."
            );
        }

        if (context.Window is not SDL3Window window)
        {
            throw new InvalidOperationException(
                $"{nameof(SDL3Keyboard)} requires an {nameof(SDL3Window)}."
            );
        }

        if (!window.Opened.Get())
            return;

        var windowId = SDL.GetWindowID(window.Native);

        if (windowId == 0)
        {
            throw new InvalidOperationException(
                $"Getting SDL window identifier failed: {SDL.GetError()}"
            );
        }

        foreach (var sdlEvent in sdlContext.Events)
        {
            var type = (SDL.EventType)sdlEvent.Type;

            if (type is not (SDL.EventType.KeyDown or SDL.EventType.KeyUp))
                continue;

            if (sdlEvent.Key.WindowID != windowId)
                continue;

            var key = SDL3KeyboardKeyMapper.FromScancode(sdlEvent.Key.Scancode);

            if (key == KeyboardKey.Unknown)
                continue;

            Publish(
                new KeyboardEventPayload(key, type == SDL.EventType.KeyDown, sdlEvent.Key.Repeat)
            );
        }
    }
}
