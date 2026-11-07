using System.Numerics;
using OpenGE.Common.Collections;
using OpenGE.Common.Events;
using OpenGE.Common.Primitives;
using OpenGE.Handling.Devices.Mouse.Events;

namespace OpenGE.Handling.Devices.Mouse;

/// <summary>
/// Represents a mouse input device that manages and publishes mouse events.
/// </summary>
/// <remarks>
/// Platform-specific implementations are responsible for receiving native mouse input
/// and publishing it through this device.
/// </remarks>
public abstract class Mouse : InputDevice
{
    private readonly Store<Vector2> _position = new(Vector2.Zero);
    private readonly Preparator _preparator = new();

    /// <summary>
    /// Gets the identifiable set of registered mouse button events.
    /// </summary>
    /// <remarks>
    /// The mouse owns and destroys its registered events.
    /// Registration transfers ownership to this owner. Removing or clearing entries returns
    /// ownership to the caller without destroying them. Do not register an object owned elsewhere.
    /// </remarks>
    public readonly IdentifiableSet<string, MouseButtonEvent> Events = [];

    /// <summary>
    /// Stores the latest mouse movement and notifies listeners of changes.
    /// </summary>
    public readonly MouseMoveEvent OnMove = new();

    /// <summary>
    /// Notifies listeners when the mouse wheel moves.
    /// </summary>
    public readonly MouseWheelEvent OnWheel = new();

    /// <summary>
    /// Gets the latest cursor position in window coordinates.
    /// </summary>
    public readonly IReadOnlyStore<Vector2> Position;

    /// <summary>
    /// Initializes a new instance of the <see cref="Mouse"/> class.
    /// </summary>
    /// <param name="events">The button events to register initially.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the initial events contain duplicate identifiers.
    /// </exception>
    protected Mouse(params MouseButtonEvent[] events)
        : base("Mouse")
    {
        foreach (var @event in events)
            Events.Add(@event);

        Position = _position;
    }

    private void ComposeContents()
    {
        foreach (var @event in Compose())
        {
            try
            {
                Events.Add(@event);
            }
            catch
            {
                if (!Events.Contains(@event) && !@event.Destroyed)
                    @event.Destroy();

                throw;
            }
        }
    }

    private void ConfigureContents()
    {
        Configure();
    }

    /// <summary>
    /// Composes the button events managed by this mouse.
    /// </summary>
    /// <returns>The events to register, in enumeration order.</returns>
    /// <remarks>
    /// Composition occurs once before the first input processing call.
    /// Constructor-provided objects are registered before composed objects.
    /// All composed objects are registered before configuration occurs.
    /// The mouse owns and destroys its registered events.
    /// If composition fails, later lifecycle calls reject further initialization.
    /// </remarks>
    protected virtual IEnumerable<MouseButtonEvent> Compose()
    {
        yield break;
    }

    /// <summary>
    /// Configures relationships and behavior after composition, before first use.
    /// </summary>
    /// <remarks>
    /// All constructor-provided and composed objects are available here.
    /// This hook is invoked at most once.
    /// If configuration throws, later processing rejects further initialization
    /// rather than repeating configuration side effects.
    /// </remarks>
    protected virtual void Configure() { }

    /// <summary>
    /// Ensures that composition and configuration have completed before input is processed.
    /// </summary>
    protected void EnsureInitialized()
    {
        ThrowIfDestroyed();
        _preparator.Ensure(ComposeContents, ConfigureContents);
    }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        foreach (var @event in Events.ToArray())
            @event.Destroy();

        Events.Destroy();
        OnMove.Destroy();
        OnWheel.Destroy();
        _position.Destroy();

        base.OnDestroy();
    }

    /// <summary>
    /// Publishes a mouse button event to matching registered events.
    /// </summary>
    /// <param name="payload">The mouse button event payload to publish.</param>
    protected void PublishButton(MouseButtonEventPayload payload)
    {
        ThrowIfDestroyed();
        _position.Set(payload.Position);

        foreach (var @event in Events)
        {
            if (@event.Source.Get() == payload.Button)
                @event.Fire(payload);
        }
    }

    /// <summary>
    /// Publishes a mouse movement event and updates the current cursor position.
    /// </summary>
    /// <param name="payload">The mouse movement payload to publish.</param>
    protected void PublishMove(MouseMoveEventPayload payload)
    {
        ThrowIfDestroyed();
        _position.Set(payload.Position);
        OnMove.Set(payload);
    }

    /// <summary>
    /// Publishes a mouse wheel event and updates the current cursor position.
    /// </summary>
    /// <param name="payload">The mouse wheel payload to publish.</param>
    protected void PublishWheel(MouseWheelEventPayload payload)
    {
        ThrowIfDestroyed();
        _position.Set(payload.Position);
        OnWheel.Fire(payload);
    }
}
