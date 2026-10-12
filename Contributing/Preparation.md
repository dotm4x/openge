# Preparation

Use `Mirage.Common.Primitives.Preparator` for objects with one-time composition and
configuration. Keep one preparator per owner and call `Ensure(compose, configure)`
before startup or first use. Registration, ownership validation, logging, and cleanup
remain in the callbacks. Do not add preparation to objects without these responsibilities.

The states are `Unprepared`, `Composing`, `Composed`, `Configuring`, `Prepared`, and
`Failed`. Completed phases are skipped. Callbacks execute synchronously; callers must
serialize access. Recursive calls while a callback is active throw. An exception escaping
a callback changes the state to `Failed` and propagates unchanged. Later calls reject
preparation rather than repeating partial side effects. Callback arguments must be non-null.

Nodes need separate composition and configuration: `EnsureComposed(compose)` registers
children during injection, then `Ensure(compose, configure)` configures the node on load.
Do not move configuration ahead of dependency injection. Other owners can execute both
phases together at their existing preparation point.

Preparation is independent of start, stop, load, unload, and destruction. Successful
preparation survives later lifecycle cycles. Each owner remains responsible for objects
adopted before a failure and must release objects that could not be registered according
to [Ownership and destruction](Ownership.md). `Module` needs no preparator of its own;
concrete modules use it only where they compose and configure contents.
