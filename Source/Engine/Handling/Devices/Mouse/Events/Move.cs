using System.Numerics;
using OpenGE.Common.Events;

namespace OpenGE.Handling.Devices.Mouse.Events;

/// <summary>
/// Initializes a new instance of the <see cref="MouseMoveEventPayload"/> class.
/// </summary>
/// <remarks>
/// Describes a change in the cursor position.
/// </remarks>
/// <param name="Position">
/// The cursor position within the window after the movement.
/// </param>
/// <param name="Delta">
/// The movement relative to the previous mouse motion event.
/// </param>
public record MouseMoveEventPayload(Vector2 Position, Vector2 Delta);

/// <summary>
/// Initializes a new instance of the <see cref="MouseMoveEvent"/> class.
/// </summary>
/// <remarks>
/// Stores the latest mouse movement and notifies listeners when it changes.
/// </remarks>
public class MouseMoveEvent()
    : Store<MouseMoveEventPayload>(new MouseMoveEventPayload(Vector2.Zero, Vector2.Zero));
