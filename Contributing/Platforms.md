# Platform integration

Windowing, input, and rendering follow the same boundary: shared projects define contracts and agnostic state, and platform projects receive native events and publish them. `SDL3` and `Raylib` are the only projects that reference native APIs. See [Rendering](Rendering.md) for the backend contract.

## Windows

- A window exposes requested and reported state through stores. Requested changes flow to the platform through the store connections; reported changes flow back through the `Publish*` helpers.
- `Publish*` avoids feeding a platform notification back to the platform as a new request, and `ShouldApply` distinguishes the two directions. Use these helpers instead of writing to the stores directly from platform code.
- Windows are `Destroyable` and owned by `WindowManager`. Managers open windows on startup in registration order and close them on shutdown in reverse registration order.
- Cursors are borrowed by windows and are not destroyed with them.

## Input

- Input devices are abstract. Platform projects receive native input and publish agnostic events with `readonly record struct` payloads.
- Devices are owned by the input handler; the input context is created per update and borrowed for that update.
- Event payloads carry meaning, not platform handles. Keep native identifiers out of shared types.
