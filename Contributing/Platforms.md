# Platform integration

Windowing, input, and rendering follow the same boundary: shared projects define contracts and agnostic state, and platform projects translate native behavior into them. See [Rendering](Rendering.md) for the backend contract.

## Windows

- A window exposes requested and reported state separately. Requested changes flow out to the platform; values reported by the platform flow back without being treated as new requests.
- Platform code publishes reported values through the base helpers rather than writing to observable state directly, and it can tell whether a change originated from the platform or from a consumer.
- Windows are owned by the window manager, which opens them on startup and closes them on shutdown in reverse order.
- Cursors are borrowed by windows and are not destroyed with them.

## Input

- Input devices are abstract. Platform projects receive native input and publish agnostic events.
- Event payloads describe meaning, not platform handles; keep native identifiers out of shared types.
- Devices are owned by the input handler. The input context is created per update and borrowed for that update.
