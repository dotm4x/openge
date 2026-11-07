# Architecture patterns

The same composition, lifecycle, and state patterns repeat across every area. Follow the shape of the surrounding code instead of inventing a parallel one. See [Ownership and destruction](Ownership.md) for lifetime rules and [Failure handling](Failures.md) for recovery rules.

## Reactive state

- State that observers react to is held in a reactive store and read or written through `Value`. Notifications occur only when the value changes.
- Expose a store directly as an `init` property rather than wrapping it in getter and setter properties. Document the member as the value it represents, not as a store.
- A type that observes a store keeps its own store and adopts the assigned value, so subscriptions survive object-initializer assignment. A store nobody observes can be replaced outright.
- Read-only views restrict access; they do not imply ownership or guarantee that the underlying value never changes.
- Use fire-only notifications for events nobody reads back, and readable reactive values for state new listeners must observe.
- Persistent subscriptions survive ordinary clearing and are used for internal wiring; transient subscriptions are cleared between uses.
- Base classes that own observable state publish externally reported values through their own helpers, so a reported value is not mistaken for a new request.

## Configuration and value types

- Configuration objects are immutable after construction, expose optional values with documented defaults, and derive from the base configuration of their area.
- Small immutable payloads and commands are value types; data that needs identity or behavior is a reference type.
- Types with a primary constructor describe the type and document its parameters on the type declaration.

## Composition and configuration

- One-time setup is split into two hooks: one returns the objects the owner adopts, the other connects already registered objects.
- Both hooks run at most once, and failure is permanent: later lifecycle calls reject further initialization instead of repeating side effects.
- Constructor-provided objects are registered before adopted objects, and everything is registered before connection hooks run.
- Composition hooks that return borrowed objects must document that they do not transfer ownership.
- When dependencies must be available while connecting, composition may be forced separately from configuration.

## Modules and the engine

- A module has a unique identifier and declares the identifiers of the modules it depends on.
- Dependencies are looked up by identifier, are always borrowed, and are never destroyed by the consumer.
- The engine resolves dependencies, injects them, starts modules in dependency order, and stops them in reverse order.
- Startup and shutdown roll back completed work, aggregate the original failure first, and leave the system in a state that can still be stopped or retried.
- Lifecycle state is observable and enforced: operations that require a running module are rejected, and destroyed objects reject further use.

## Collections

- Registries key items by a stable identifier and report additions, removals, and clearing through notifications.
- Reactive collections report their mutations; destruction disconnects notifications and drops references without firing ordinary mutation events.
- Collections never own their elements. The surrounding owner destroys them explicitly.
- Removing or clearing entries returns ownership of the objects to the caller without destroying them, and the owning collection documents that contract.
- Prefer one ownership contract per managed collection; do not add ownership flags or parallel registration methods without a real use case.
- Expose mutable registries as read-only views when external mutation could invalidate internal invariants.
