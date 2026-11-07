# Rendering

Rendering separates backend-independent commands, spaces that collect them, a renderer that drives frames, and backends that execute commands. Shared projects define the contracts; platform projects implement them. See [Platform integration](Platforms.md).

## Commands and renderables

- Commands are backend-independent values that reference borrowed resources only.
- Renderable objects produce ordered commands for the current frame from a rendering context that carries camera, viewport, and timing.
- A space without a camera is invalid; reject it before collecting its commands rather than failing inside a backend.
- Two-dimensional geometry composes its own transform first, then the camera view, then the projection for the current viewport.

## Spaces

- A space groups renderables and carries a camera, an optional offscreen target, and a priority.
- Spaces with the same priority render in registration order.
- Scene-driven spaces walk the loaded hierarchy, skip invisible subtrees, and tolerate a renderable changing the hierarchy while producing commands.
- Entries, targets, and cameras are borrowed; a space never destroys them.

## Renderer

- The renderer owns its backends and spaces and exposes them as read-only views, so an active selection cannot be removed from under it.
- Frames are rendered only while the associated window is open.
- Every frame clears the window, and each offscreen target once, before the first space that renders to it.
- Frame completion is attempted even when collecting or executing commands fails. When both fail, the original rendering error comes first in the aggregate.
- A viewport is derived from the space's target, or from the window when the space renders to the window.

## Backends

Backends implement explicit hooks for the frame, the destination, and each command:

- beginning and ending a frame, including acquiring and releasing frame resources;
- selecting a destination once per render call, so a space cannot switch targets mid-collection;
- executing one command with the context of its space;
- releasing the destination after the commands run, including when execution fails.

## Graphics resources

- Textures, shaders, materials, and offscreen targets describe resources; backends create, cache, and release their native representations.
- Resources referenced by a material are borrowed and are not destroyed with it.
- Offscreen target sizes are fixed; replace the target instead of resizing it in place.
- Color components are normalized floating-point values. Validate ranges where values are consumed, and remember that a default-initialized color and an explicitly constructed one can differ in opacity.
