namespace OpenGE.Handling.Devices.Keyboard;

/// <summary>
/// Initializes a new instance of the <see cref="KeyboardEventPayload"/> class.
/// </summary>
/// <remarks>
/// Describes a keyboard action triggered by a physical key.
/// </remarks>
/// <param name="Key">The physical key that changed state.</param>
/// <param name="Down"><see langword="true"/> when the key was pressed.</param>
/// <param name="Repeat"><see langword="true"/> when this is a repeated key press.</param>
public record KeyboardEventPayload(KeyboardKey Key, bool Down, bool Repeat);

/// <summary>
/// Initializes a new instance of the <see cref="KeyboardEvent"/> class.
/// </summary>
/// <remarks>
/// Publishes key press and release events for one physical key.
/// </remarks>
/// <param name="identifier">The stable action identifier.</param>
/// <param name="source">The key that activates this action.</param>
public class KeyboardEvent(string identifier, KeyboardKey source)
    : InputEvent<KeyboardEventPayload, KeyboardKey>(identifier, source) { }
