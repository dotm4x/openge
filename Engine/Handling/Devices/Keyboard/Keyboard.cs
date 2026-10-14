using OpenGE.Common.Collections;
using OpenGE.Common.Primitives;

namespace OpenGE.Handling.Devices.Keyboard;

/// <summary>
/// Represents a keyboard input device that manages and publishes keyboard events.
/// </summary>
/// <remarks>
/// Platform-specific implementations are responsible for receiving native keyboard input
/// and publishing it through this device.
/// </remarks>
public abstract class Keyboard : InputDevice
{
    private readonly Preparator _preparator = new();

    /// <summary>
    /// Gets the identifiable set of registered keyboard events.
    /// </summary>
    /// <remarks>
    /// The keyboard owns and destroys its registered events.
    /// Registration transfers ownership to this owner. Removing or clearing entries returns
    /// ownership to the caller without destroying them. Do not register an object owned elsewhere.
    /// </remarks>
    public readonly IdentifiableSet<string, InputEvent<KeyboardEventPayload, KeyboardKey>> Events =
    [];

    /// <summary>
    /// Initializes a new instance of the <see cref="Keyboard"/> class.
    /// </summary>
    /// <param name="events">The events to register initially.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the initial events contain duplicate identifiers.
    /// </exception>
    protected Keyboard(params InputEvent<KeyboardEventPayload, KeyboardKey>[] events)
        : base("Keyboard")
    {
        foreach (var @event in events)
            Events.Add(@event);
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
    /// Composes the events managed by this keyboard.
    /// </summary>
    /// <returns>The events to register, in enumeration order.</returns>
    /// <remarks>
    /// Composition occurs once before the first input processing call.
    /// Constructor-provided objects are registered before composed objects.
    /// All composed objects are registered before configuration occurs.
    /// The keyboard owns and destroys its registered events.
    /// If composition fails, later lifecycle calls reject further initialization.
    /// </remarks>
    protected virtual IEnumerable<InputEvent<KeyboardEventPayload, KeyboardKey>> Compose()
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
        _preparator.Ensure(ComposeContents, ConfigureContents);
    }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        foreach (var @event in Events.ToArray())
            @event.Destroy();

        Events.Destroy();

        base.OnDestroy();
    }

    /// <summary>
    /// Publishes a keyboard event to registered events matching its key.
    /// </summary>
    /// <param name="payload">The keyboard event payload to publish.</param>
    protected void Publish(KeyboardEventPayload payload)
    {
        foreach (var @event in Events)
        {
            if (@event.Source.Get() == payload.Key)
                @event.Fire(payload);
        }
    }
}
