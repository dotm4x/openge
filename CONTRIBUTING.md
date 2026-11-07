# Contributing

This document describes how OpenGE is built, structured, and maintained. It is the single entry point for contributors and automated agents. Read it before changing code; the focused documents linked below expand on the parts that need more depth.

- [Failure handling](Contributing/Failures.md): recovery boundaries, partial startup, cleanup errors, and diagnostic context.
- [Ownership and destruction](Contributing/Ownership.md): lifetime responsibility, borrowed references, composition, and teardown.
- [Documenting the API](Contributing/Documenting.md): XML documentation, lifecycle contracts, reactive state, and examples.

Shared development practice also follows the [Rayscale Development and Implementation Guidelines](https://gitlab.com/rayscale/development/guidelines).

## Repository layout

```text
Source/Engine/<Area>/          Production projects (one project per area)
Tests/Engine/<Area>/           Test project mirroring the production layout
Tests/Tests.csproj             Single xUnit test project
Build/bin, Build/obj           Build output (see Directory.Build.props)
Contributing/                  Focused contributor documentation
Assets/, docfx.json            Documentation assets and docfx metadata
```

- Each production project lives at `Source/Engine/<Area>/OpenGE.<Area>.csproj` and targets `net10.0`.
- `Directory.Build.props` owns every project's compiler settings, output paths, and documentation generation. Do not repeat those properties per project.
- `OpenGE.slnx` is the solution; add new projects there under the `/Source/Engine/` folder.
- Shared projects must stay platform-agnostic. Rendering, input, and windowing concepts live in the shared projects; `SDL3` and `Raylib` are the only projects allowed to reference native platform APIs.

## Prerequisites and commands

The repository requires the .NET 10 SDK and the pinned CSharpier tool.

```bash
dotnet tool restore --tool-manifest dotnet-tools.json   # restore pinned tools
dotnet build OpenGE.slnx                                # build every project
dotnet test OpenGE.slnx                                 # run the xUnit suite
dotnet csharpier check .                                # verify formatting
dotnet csharpier format .                               # apply formatting
```

CI runs restore, `csharpier check`, a Release build, and the test suite. A change is not complete until the same commands pass locally. Run the smallest relevant command while iterating, and the full suite before finishing.

## Naming

- Use clear, complete English names. Avoid unnecessary abbreviations and ambiguous identifiers.
- Use `PascalCase` for types, methods, properties, and events; `camelCase` for parameters and local variables.
- Prefix private instance fields with `_` followed by `camelCase`.
- Prefix generic type parameters with `T` plus a meaningful `PascalCase` name (`TValue`, `TKey`, `TElement`, `TResult`). Never use a bare `T`.
- Name files after the concept they contain, without the `OpenGE.` prefix (`Rendering/Backend.cs`, `Graphics/Resources/Targets/Target.cs`). A file may declare several related types when they form one concept, such as an options class with its type or a payload record with the event that publishes it.
- Use consistent terminology across related APIs. The same concept keeps the same noun everywhere: composition, configuration, injection, space, channel, target, resource.

## C# style

- Use file-scoped namespaces and modern, idiomatic C# for the configured language version.
- Follow `.editorconfig` and `Directory.Build.props`; never disable nullable warnings or code analysis to make a build pass.
- Use `var` only when the assigned type is apparent from the initializer.
- Prefer properties for exposed state and methods for meaningful operations.
- Keep classes non-sealed unless sealing is required by a concrete reason.
- Introduce an interface when it establishes a meaningful contract, not to wrap a single implementation.
- Respect nullable annotations. Do not add redundant null checks for non-nullable parameters or use `!` without justification.
- Avoid wrappers, forwarding methods, inheritance layers, and abstractions that only restate an existing one.
- Preserve public behavior unless the change explicitly requires a behavioral change.

## Core patterns

These patterns repeat across every area. Follow the shape of the surrounding code instead of inventing a parallel one.

### Reactive state

- Mutable state that observers react to is a `Store<T>`, exposed as a `public readonly` field. The store notifies only when the value changes.
- Read-only exposure uses `IReadOnlyStore<T>`, `IReadOnlyEvent<T>`, `IReadOnlyIdentifiableSet<...>`, or `IReadOnlyReactiveSet<T>`. Read-only views restrict access; they do not imply ownership.
- Fire-only notifications use `Signal<T>`; values that new listeners must be able to read use `Event<T>` and `Store<T>`. Persistent connections survive `Clear()` and are used for internal wiring.
- Base classes that own a store keep it private and publish platform- or state-reported values through their own helper methods, so reported values are not treated as new requests.

### Options and primary constructors

- Configuration bags derive from the base options type and use `init` properties with inline defaults:

```csharp
public class Camera2DOptions : Node2DOptions
{
    /// <summary>
    /// Gets the initial zoom factor.
    /// The default is <c>1</c>, which means no zoom.
    /// </summary>
    public float Zoom { get; init; } = 1f;
}
```

- Small immutable payloads and commands are `readonly record struct`; data that needs identity or behavior is a class.
- Types with a primary constructor describe the type in `<summary>` and document the constructor parameters with `<param>` tags on the type.

### Composition and configuration

- One-time setup is expressed through two hooks: `Compose()`, which returns the objects the owner adopts, and `Configure()`, which connects already registered objects.
- Both hooks run at most once. `Preparator` enforces the order and makes failure permanent:

```csharp
_preparator.Ensure(ComposeContents, ConfigureContents);
```

- Use `EnsureComposed(...)` only when dependencies must be injected into composed contents before configuration.
- Constructor-provided objects are registered before composed objects, and every object is registered before `Configure()` runs.
- If composition or configuration throws, later lifecycle calls must reject further initialization instead of repeating side effects.

### Modules and engines

- A module derives from `Module`, declares a unique identifier, and lists its dependencies by identifier (`base("Renderer-0", ["Logger"])`).
- Retrieve dependencies through `Require<TModule>(identifier)`; they are borrowed and never destroyed by the consumer.
- `Engine` resolves dependencies, injects them, starts modules in dependency order, and stops them in reverse order. Startup and shutdown roll back completed work and aggregate the original failure first.
- Lifecycle state is observable through `State` and is enforced: operations that require a running module throw `InvalidOperationException`, and destroyed objects throw `DestroyedObjectException`.

### Ownership and teardown

- Each `Destroyable` has one logical owner. Ownership transfers through constructors, composition, and explicit registration; borrowed references are never destroyed.
- `OnDestroy()` releases owned contents and then calls `base.OnDestroy()` last. See [Ownership and destruction](Contributing/Ownership.md) for ordering rules and the exceptions.
- Collections do not own their elements; the surrounding owner destroys them explicitly.

### Determinism and iteration

- Never iterate a `Dictionary`, `IdentifiableSet`, or another hash-ordered structure when order affects behavior. Maintain a registration-order list or cache a sorted snapshot instead.
- Snapshot a collection (`ToArray()`) before iterating when the loop body may register, remove, or destroy entries.
- Sort with a stable sort (`OrderBy`) when ties must preserve registration order.
- Cache derived orderings (sorted channels, sorted outputs) and invalidate them through collection events instead of re-sorting on every tick.

### Validation

- Validate constructor arguments eagerly and throw `ArgumentException`, `ArgumentNullException`, or `ArgumentOutOfRangeException` with the parameter name.
- Validate mutable state where it is consumed. A `Store<T>` can change after construction, so a value that must stay valid, such as a zoom factor or a size, is rechecked before use.
- Prefer `DestroyedObjectException`, `InvalidOperationException`, and `NotSupportedException` for lifecycle, state, and capability failures respectively.

## Collections

- `IdentifiableSet<TIdentifier, TItem>` keys items by identifier, supports an optional `Limit`, and exposes `OnAdd`/`OnRemove`/`OnClear`. Duplicate identifiers throw `InvalidOperationException`.
- `ReactiveSet<TItem>` and `ReactiveDictionary<TKey, TValue>` provide reactive events. Destruction disconnects signals and drops references without firing ordinary mutation events.
- Removing or clearing entries returns ownership of the objects to the caller without destroying them. Document that contract on every owning collection.
- Expose mutable registries as read-only views when mutation could invalidate internal invariants.

## Rendering

- Commands are backend-independent `readonly record struct` values implementing `IRenderCommand`; they reference borrowed resources only.
- Renderable objects implement `IRenderable.Render(IRenderContext)` and return `RenderData` with commands in execution order.
- A `RenderSpace` groups renderables, carries a camera, target, and priority, and collects commands. Spaces with the same priority render in registration order.
- `Renderer` owns its backends and spaces, renders only while the associated window is open, clears the window every frame, and clears each offscreen target once before the first space that uses it.
- `RenderBackend` implementations receive explicit hook calls: `OnBeginFrame`, `OnBeginRender(target)`, `OnRender` per command, `OnEndRender`, and `OnEndFrame`. `OnEndRender` runs even when command execution fails.
- The rendering destination is selected once per `Render` call; a space cannot switch targets mid-collection.
- 2D geometry composes its transform first, then the camera view, then the projection for the current viewport.
- Graphics resources (`Texture`, `Shader`, `GraphicMaterial`, `RenderTarget`) are borrowed by the objects that reference them and are created, cached, and released by the backend that uses them.

## Scheduling

- `Scheduler` drives the main loop. `UpdateChannel` groups `IUpdatable` entries with a priority and an optional target rate; the effective rate is the lower positive rate of the channel and scheduler.
- A rate-limited channel keeps the remainder of its accumulated time so the average rate matches the target.
- `UpdateContext.DeltaTime` is the delta of that update, clamped by `Scheduler.MaximumDeltaTime`; `UpdateRate` is the instantaneous rate of the latest update, not a measured average.
- A channel never performs more than one update per scheduler iteration.

## Windowing and input

- Windows expose requested and reported state through stores. Platform implementations publish reported changes through the `Publish*` helpers so a platform notification is not processed as a new request; `ShouldApply` distinguishes the two directions.
- Windows, cursors, devices, and events are `Destroyable`; managers own windows, while cursors and input contexts are borrowed.
- Input devices are abstract: platform projects receive native input and publish agnostic events. Event payloads are `readonly record struct` values.

## Importing and logging

- Decoders derive from `Decoder`, declare the `ResourceType` they produce, and accept the file extensions they support. Decoded resources are owned by the caller.
- The importer owns decoded resources and its decoders. Failures keep the original exception type and attach `OpenGE.ResourcePath`, `OpenGE.ResourceType`, and `OpenGE.Decoder` to `Exception.Data`; update failures attach `OpenGE.UpdateChannel`.
- Loggers own their outputs. Outputs are ordered by `LogOutputPriority` (highest first) and receive `LogMessage` values with content, source, kind, and optional metadata.
- Use `Logger.From(source)` to scope messages to a component instead of repeating the source string.

## XML documentation

Every public and protected member is documented; the build reports missing documentation and the project enables `GenerateDocumentationFile`. The full guidance lives in [Documenting the API](Contributing/Documenting.md); these are the non-negotiable conventions:

- Write in English, in brief, concrete sentences, and end summary sentences with a period.
- Tag order: `<summary>`, `<typeparam>`, `<remarks>`, `<example>`, `<param>`, `<returns>`, `<exception>`. `<inheritdoc />` always comes first.
- Explicit constructors start with `Initializes a new instance of the <see cref="X"/> class.` (or `struct.`). Types with a primary constructor describe the type instead and keep `<param>` tags for the constructor parameters.
- Use `<inheritdoc />` for overrides and interface implementations, adding `<remarks>` only for meaningful differences.
- Boolean members read `Gets a value indicating whether ...`.
- Use `<see langword="null"/>` for the null literal and `<see langword="true"/>`/`<see langword="false"/>` for booleans. Use `<c>` for literals and identifiers that do not need links.
- Document defaults, units, valid ranges, and ownership on parameters and options.
- Keep documentation lines within roughly 100 columns, matching the surrounding code.
- Never document guarantees the implementation does not provide.

## Tests

Use xUnit and the existing build and test commands.

- Mirror the production layout: `Source/Engine/Numerics/Geometry/Mesh2D.cs` corresponds to `Tests/Engine/Numerics/Geometry/Mesh2DTests.cs`.
- Name test classes after the production type with a `Tests` suffix, and name test methods `Operation_Condition_ExpectedBehavior`.
- Structure every test with Arrange, Act, and Assert, separated by blank lines and marked with `// Arrange`, `// Act`, and `// Assert` when it aids clarity.
- Test observable behavior, boundary conditions, and failure contracts. Assert meaningful outcomes, state changes, events, and exception types.
- Keep tests deterministic and independent; dispose everything the test owns.
- Prefer small in-test helpers (`TestWindow`, `RecordingBackend`, `TestEngine`) over shared infrastructure or mocking frameworks.
- Never weaken, delete, or skip a test to hide a failure, and never change production behavior only to satisfy a test.

## Commits

Commit messages follow Conventional Commits, as used in this repository's history:

```text
feat(raylib): implement raylib render backend
fix(scheduling): keep channel rate accuracy and bound iteration deltas
refactor(core): unify destruction order and deterministic iteration
docs: unify XML documentation style across the engine
chore(structure): move engine sources to Source and tests to Tests/Engine
testing: add unit tests for various components in the Tests project
ci: add testing step to CI pipeline
```

- Use a lowercase type, an optional lowercase scope in parentheses, a colon, and an imperative subject without a trailing period.
- Keep the subject short; explain motivation, behavior changes, and verification in the body.
- Prefer one focused commit per change. Keep unrelated refactoring out of feature and fix commits.

## Before you finish

- [ ] `dotnet build OpenGE.slnx` succeeds with no new warnings.
- [ ] `dotnet test OpenGE.slnx` passes.
- [ ] `dotnet csharpier check .` passes.
- [ ] Public and protected members are documented in the style above, and docfx-visible references resolve.
- [ ] Ownership, lifecycle, and failure behavior match the documented contract.
- [ ] Tests cover the new or changed behavior, including failure paths.
- [ ] The diff contains no unrelated changes.
