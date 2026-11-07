# Rendering

Rendering is split into backend-independent commands, spaces that collect them, a renderer that drives frames, and backends that execute commands. Shared projects define the contracts; `SDL3` and `Raylib` implement them. See [Platform integration](Platforms.md) for the platform boundary.

## Commands and renderables

- Commands are backend-independent `readonly record struct` values implementing `IRenderCommand`; they reference borrowed resources only.
- Renderable objects implement `IRenderable.Render(IRenderContext)` and return `RenderData` with commands in execution order.
- Two-dimensional geometry uses `Render2DCommand`. Backends compose its transform first, then the camera view, then the projection for the current viewport.
- A space with no camera is invalid. `RenderContext.Camera` is required, and the renderer rejects a space without one before collecting its commands.

## Spaces

- A `RenderSpace` groups renderables, carries a camera, an optional target, and a priority, and collects commands from its entries followed by its collection hook.
- Spaces with the same priority render in registration order.
- `NodeRenderSpace` collects the loaded, visible nodes under the active root and skips invisible subtrees.
- Entries and targets are borrowed; the space never destroys them.

## Renderer

- `Renderer` owns its backends and spaces and exposes them as read-only views so the active backend cannot be removed from under it.
- Frames are rendered only while the associated window is open.
- The window is cleared at the start of every frame with `ClearColor`, and each offscreen target is cleared once, before the first space that renders to it.
- Frame completion is attempted even when collecting or executing commands fails. When both fail, the rendering error is the first inner exception of an `AggregateException`.
- Spaces are rendered in priority order using a viewport derived from their target, or the window size when they render to the window.

## Backends

Backends implement explicit hooks:

- `OnBeginFrame` acquires the resources needed for a frame.
- `OnBeginRender(target)` selects the destination. The destination is selected once per `Render` call, so a space cannot switch targets mid-collection.
- `OnRender(command, context)` executes one command.
- `OnEndRender` releases the destination and runs even when command execution fails.
- `OnEndFrame` finishes and presents the frame.

## Graphics resources

- `Texture`, `Shader`, `GraphicMaterial`, and `RenderTarget` describe resources; backends create, cache, and release their native representations.
- Referenced shaders, textures, and samplers are borrowed and are not destroyed with the material.
- `RenderTarget` sizes are immutable; swap the target through its store instead of resizing in place.
- `Color` components are normalized floats. Validate ranges where the value is consumed, and remember that the default struct value is fully transparent while explicitly constructed colors default alpha to opaque.
