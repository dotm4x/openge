# Architecture patterns

The same composition, lifecycle, and state patterns repeat across every area. Follow the shape of the surrounding code instead of inventing a parallel one. See [Ownership and destruction](Ownership.md) for lifetime rules and [Failure handling](Failures.md) for recovery rules.

## Reactive state

- Mutable state that observers react to is a `Store<T>`, exposed as a `public readonly` field. The store notifies only when the value changes.
- Read-only exposure uses `IReadOnlyStore<T>`, `IReadOnlyEvent<T>`, `IReadOnlyIdentifiableSet<...>`, or `IReadOnlyReactiveSet<T>`. Read-only views restrict access; they do not imply ownership.
- Fire-only notifications use `Signal<T>`; values that new listeners must be able to read use `Event<T>` and `Store<T>`. Persistent connections survive `Clear()` and are used for internal wiring.
- Base classes that own a store publish reported values through their own helper methods, so a reported value is not treated as a new request from the consumer.

## Options and records

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

## Composition and configuration

- One-time setup is expressed through two hooks: `Compose()`, which returns the objects the owner adopts, and `Configure()`, which connects already registered objects.
- Both hooks run at most once. `Preparator` enforces the order and makes failure permanent:

```csharp
_preparator.Ensure(ComposeContents, ConfigureContents);
```

- Use `EnsureComposed(...)` only when dependencies must be injected into composed contents before configuration.
- Constructor-provided objects are registered before composed objects, and every object is registered before `Configure()` runs.
- If composition or configuration throws, later lifecycle calls must reject further initialization instead of repeating side effects.

## Modules and engines

- A module derives from `Module`, declares a unique identifier, and lists its dependencies by identifier, for example `base("Renderer-0", ["Logger"])`.
- Retrieve dependencies through `Require<TModule>(identifier)`; they are borrowed and never destroyed by the consumer.
- `Engine` resolves dependencies, injects them, starts modules in dependency order, and stops them in reverse order. Startup and shutdown roll back completed work and aggregate the original failure first.
- Lifecycle state is observable through `State` and is enforced: operations that require a running module throw `InvalidOperationException`, and destroyed objects throw `DestroyedObjectException`.

## Collections

- `IdentifiableSet<TIdentifier, TItem>` keys items by identifier, supports an optional `Limit`, and exposes `OnAdd`, `OnRemove`, and `OnClear`. Duplicate identifiers throw `InvalidOperationException`.
- `ReactiveSet<TItem>` and `ReactiveDictionary<TKey, TValue>` provide reactive events. Destruction disconnects signals and drops references without firing ordinary mutation events.
- Removing or clearing entries returns ownership of the objects to the caller without destroying them. Document that contract on every owning collection.
- Prefer one ownership contract per managed collection. Do not add ownership flags or separate owned and borrowed registration methods without a real use case.
- Expose mutable registries as read-only views when mutation could invalidate internal invariants, such as an active selection held internally.
