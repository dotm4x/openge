using System.Numerics;
using Mirage.Handling;
using Mirage.Handling.Devices.Mouse;
using Mirage.Handling.Devices.Mouse.Events;
using Mirage.SDL3.Windowing;
using SDL = global::SDL3.SDL;

namespace Mirage.SDL3.Handling.Devices.Mouse;

/// <summary>
/// Processes mouse input using SDL3.
/// </summary>
public class SDL3Mouse : Mirage.Handling.Devices.Mouse.Mouse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SDL3Mouse"/> class.
    /// </summary>
    /// <param name="events">The mouse button events to register initially.</param>
    public SDL3Mouse(params MouseButtonEvent[] events)
        : base(events) { }

    private static MouseButton? FromSDLButton(byte button)
    {
        return button switch
        {
            1 => MouseButton.Left,
            2 => MouseButton.Middle,
            3 => MouseButton.Right,
            4 => MouseButton.X1,
            5 => MouseButton.X2,
            _ => null,
        };
    }

    private void ProcessButton(SDL.Event sdlEvent, SDL.EventType type)
    {
        var button = FromSDLButton(sdlEvent.Button.Button);

        if (button is null)
            return;

        PublishButton(
            new MouseButtonEventPayload(
                button.Value,
                type == SDL.EventType.MouseButtonDown,
                sdlEvent.Button.Clicks,
                new Vector2(sdlEvent.Button.X, sdlEvent.Button.Y)
            )
        );
    }

    private void ProcessMotion(SDL.Event sdlEvent)
    {
        PublishMove(
            new MouseMoveEventPayload(
                new Vector2(sdlEvent.Motion.X, sdlEvent.Motion.Y),
                new Vector2(sdlEvent.Motion.XRel, sdlEvent.Motion.YRel)
            )
        );
    }

    private void ProcessWheel(SDL.Event sdlEvent)
    {
        PublishWheel(
            new MouseWheelEventPayload(
                new Vector2(sdlEvent.Wheel.X, sdlEvent.Wheel.Y),
                new Vector2(sdlEvent.Wheel.MouseX, sdlEvent.Wheel.MouseY)
            )
        );
    }

    /// <inheritdoc />
    protected override void OnProcess(InputContext context)
    {
        EnsureInitialized();

        if (context is not SDL3InputContext sdlContext)
        {
            throw new InvalidOperationException(
                $"{nameof(SDL3Mouse)} requires an {nameof(SDL3InputContext)}."
            );
        }

        if (context.Window is not SDL3Window window)
        {
            throw new InvalidOperationException(
                $"{nameof(SDL3Mouse)} requires an {nameof(SDL3Window)}."
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

            switch (type)
            {
                case SDL.EventType.MouseButtonDown:
                case SDL.EventType.MouseButtonUp:
                    if (sdlEvent.Button.WindowID == windowId)
                        ProcessButton(sdlEvent, type);

                    break;

                case SDL.EventType.MouseMotion:
                    if (sdlEvent.Motion.WindowID == windowId)
                        ProcessMotion(sdlEvent);

                    break;

                case SDL.EventType.MouseWheel:
                    if (sdlEvent.Wheel.WindowID == windowId)
                        ProcessWheel(sdlEvent);

                    break;
            }
        }
    }
}
