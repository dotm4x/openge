<p align="center">
  <a href="Assets/Wide.png">
    <img
      src="Assets/Wide.png"
      width="600"
      alt="Mirage Logo"
    >
  </a>
</p>

---

Mirage is an experimental 2D game engine for C# and .NET.

It is built around small systems that can be composed together while keeping their responsibilities separate. Mirage is currently developed alongside games at Stone Bogus and evolves as those projects need it.

> [!WARNING]
> Mirage is under active development. APIs and project structure may change.

## Features

- Hierarchical node system
- 2D spatial nodes and cameras
- Space-based rendering
- Clear and 2D mesh rendering with textures and materials
- Keyboard and mouse input
- Resource importing and decoding
- Update scheduling
- Window management
- Reactive events and state
- Modular engine composition

## Example

A Mirage engine is assembled from modules. This complete example loads a small
node tree, stops, and releases its owners.
It does not open a window or run a game loop.

```csharp
using Mirage.Common;
using Mirage.Logging;
using Mirage.Logging.Outputs;
using Mirage.Noding;
using Mirage.Spatial.Nodes;

var engine = new ExampleEngine();
engine.Start();
engine.Stop();
engine.Destroy();

internal sealed class ExampleEngine : Engine
{
    protected override IEnumerable<Module> Compose()
    {
        yield return new Logger([new ConsoleOutput()]);
        yield return new NodeManager(new ExampleRoot());
    }
}

internal sealed class ExampleRoot() : Node("Main")
{
    protected override IEnumerable<Node> Compose()
    {
        yield return new Node2D("Player");
    }
}
```

The engine owns its modules. `NodeManager` depends on `Logger`, so the engine starts
logging first and keeps it available until the node manager has stopped and been destroyed.

## Nodes

Nodes represent the structure of a game.

```text
Main
├── Camera
├── Player
├── World
│   ├── Enemy
│   └── Enemy
└── UI
```

Nodes can compose other nodes and can be added or moved while the game is running.

```csharp
using Mirage.Graphics.Nodes;
using Mirage.Noding;
using Mirage.Spatial.Nodes;

internal sealed class MyRoot : Node
{
    public readonly Camera2D Camera = new();
    public readonly Node2D Player = new("Player");

    public MyRoot()
        : base("Main") { }

    protected override IEnumerable<Node> Compose()
    {
        yield return Camera;
        yield return Player;
    }
}
```

A `NodeManager` exposes explicitly selected module dependencies to managed nodes.
Nodes can use `Require<TModule>()` during `Configure()` after their dependency context
has been injected. Retain connections to borrowed publishers and disconnect them before
the subscribing node is destroyed.

## Composition & Configuration

Mirage uses composition to declare owned objects and configuration to connect them after they have been composed.

The following conceptual fragments assume a subclass with initialized `Keyboard` and
`Mouse` devices, a `Jump` event, and an `OnJump` callback. `Compose()` declares contents:

```csharp
protected override IEnumerable<InputDevice> Compose()
{
    yield return Keyboard;
    yield return Mouse;
}
```

`Configure()` can then connect behavior or establish relationships once those objects are available:

```csharp
protected override void Configure()
{
    Keyboard.Jump.Connect(OnJump);
}
```

`Mirage.Common.Primitives.Preparator` controls these phases throughout the engine:

```csharp
private readonly Preparator _preparator = new();

// ComposeContents registers the results of the object's Compose() hooks.
_preparator.Ensure(ComposeContents, Configure);
```

Composition completes before configuration, and each phase runs at most once. `State`
exposes the current `PreparationState`; `Prepared` becomes true after both phases succeed.
Reentrant preparation is rejected. If either phase throws, preparation permanently fails
and the original exception propagates. Startup failures after successful preparation do
not reset it, so later lifecycle cycles reuse the prepared contents.

Nodes use `EnsureComposed()` during dependency injection and finish preparation when
loaded. This preserves access to injected dependencies in `Configure()`. The preparator
controls ordering only: each owner still registers its contents and handles their cleanup.
It does not introduce preparation hooks into `Module` or replace startup and shutdown.

## Lifecycle failures

If startup fails, `Engine` stops completed modules in reverse dependency order.
If a module cannot stop, rollback leaves its dependencies available, enters `Failed`,
and throws an `AggregateException` containing the startup failure first and the cleanup
failure afterward. A failed `Stop()` also enters `Failed`. Call `Stop()` again to retry
remaining running modules and the shutdown hook; startup and destruction require `Idle`.
If a module's `OnStart()` fails, its `OnStop()` runs to clean up incomplete startup.
Shutdown hooks must tolerate partial initialization. If cleanup fails, the engine
keeps dependencies available for a shutdown retry.


An application owns the engine and runs its borrowed scheduler after startup.
Keep shutdown at that application boundary. This fragment assumes `engine` and
`scheduler` come from the application's composition code:

```csharp
Exception? failure = null;
try
{
    engine.Start();
    scheduler.Run();
}
catch (Exception exception)
{
    failure = exception;
}
finally
{
    try
    {
        if (engine.State.Get() is EngineState.Running or EngineState.Failed)
            engine.Stop();

        if (engine.State.Get() == EngineState.Idle)
            engine.Destroy();
    }
    catch (Exception cleanupFailure)
    {
        failure = failure is null
            ? cleanupFailure
            : new AggregateException(failure, cleanupFailure);
    }
}

if (failure is not null)
    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
```

If shutdown leaves the engine `Failed`, retain its reference and retry `Stop()`
before destruction. Cleanup errors preserve the original failure instead of
replacing it. Failures after decoder selection include the path, requested resource type and decoder
in `Exception.Data`; scheduler errors identify the failing update channel there.

## Architecture

Mirage is split into focused projects:

```text
Common
Graphics
Handling
Importing
Logging
Noding
Numerics
Rendering
Scheduling
SDL3
Spatial
Windowing
```

They cover different parts of the engine without requiring everything to belong to one large framework.

For example, the node hierarchy describes the game world, while rendering, input, scheduling, and windowing remain separate systems.

## SDL3 integration

Reference `Engine/SDL3/Mirage.SDL3.csproj` in applications that use SDL3.
`Mirage.SDL3` is the official integration assembly and references the core engine
projects directly. Core projects do not depend on SDL3.

The integration exposes `SDL3Window` and `SDLCursor` in `Mirage.SDL3.Windowing`,
`SDL3InputHandler` and `SDL3InputContext` in `Mirage.SDL3.Handling`, and
`SDL3RenderBackend` in `Mirage.SDL3.Rendering`. Keyboard and mouse implementations
are in `Mirage.SDL3.Handling.Devices.Keyboard` and
`Mirage.SDL3.Handling.Devices.Mouse`. Update imports from the former
`Mirage.Handling.SDL3`, `Mirage.Rendering.SDL3`, and `Mirage.Windowing.SDL3` namespaces.

Construct these types directly when composing an application. The input handler
and rendering backend borrow their window; stop and destroy their owners before
destroying the window. SDL3 package versions and native runtime dependencies are
maintained by the integration project.

## Philosophy

Mirage is a focused 2D engine.

It prefers small systems, explicit ownership, composition, and existing .NET types where they already solve a problem well.

The engine does not try to abstract every implementation detail or support every kind of game. Its architecture grows alongside the games being built with it.

## AI Usage

AI-assisted tools are used throughout Mirage's development for exploring ideas, reviewing and implementing code, finding inconsistencies, and maintaining documentation.

Architecture and design decisions remain with the maintainers. AI output is reviewed and may be modified or discarded.

Much of Mirage's API documentation is written or refined with AI assistance to maintain consistency across the codebase. This README and other project documentation may also use AI assistance.

Mirage is not generated as a whole by AI; AI is one of the tools used in its iterative development.

## Development

Active development takes place on the `develop` branch.

```bash
git checkout develop
dotnet tool restore --tool-manifest dotnet-tools.json
dotnet csharpier check .
dotnet build Mirage.slnx --configuration Release
```

Create experimental applications outside this repository and reference the required
Mirage projects directly. For example, an external application's project file can include:

```xml
<ItemGroup>
  <ProjectReference Include="../Mirage/Engine/Common/Mirage.Common.csproj" />
  <ProjectReference Include="../Mirage/Engine/Logging/Mirage.Logging.csproj" />
  <ProjectReference Include="../Mirage/Engine/Noding/Mirage.Noding.csproj" />
  <ProjectReference Include="../Mirage/Engine/Spatial/Mirage.Spatial.csproj" />
</ItemGroup>
```

Adjust these relative paths to your checkout. Project references build the current
Mirage source without installing NuGet packages or modifying the engine's solution.

> [!NOTE]
> Mirage is not stable yet. Documentation and examples may change as the engine develops.

## License

Mirage is licensed under the [MIT License](LICENSE).