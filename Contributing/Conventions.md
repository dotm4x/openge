# Code conventions

These rules apply to every project. They describe how OpenGE code reads and how new code should be written. See [Architecture patterns](Architecture.md) for the structural conventions and [Documenting the API](Documenting.md) for XML documentation.

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

## Determinism and iteration

- Never iterate a `Dictionary`, `IdentifiableSet`, or another hash-ordered structure when order affects behavior. Maintain a registration-order list or cache a sorted snapshot instead.
- Snapshot a collection (`ToArray()`) before iterating when the loop body may register, remove, or destroy entries.
- Sort with a stable sort (`OrderBy`) when ties must preserve registration order.
- Cache derived orderings, such as sorted channels or sorted outputs, and invalidate them through collection events instead of re-sorting on every tick.

## Validation and failures

- Validate constructor arguments eagerly and throw `ArgumentException`, `ArgumentNullException`, or `ArgumentOutOfRangeException` with the parameter name.
- Validate mutable state where it is consumed. A `Store<T>` can change after construction, so a value that must stay valid, such as a zoom factor or a size, is rechecked before use.
- Prefer `DestroyedObjectException`, `InvalidOperationException`, and `NotSupportedException` for lifecycle, state, and capability failures respectively.
- Recover where an operation changes lifecycle state, transfers ownership, or acquires resources. See [Failure handling](Failures.md) for recovery, rollback, and diagnostic expectations.

## Comments

- Keep comments for non-obvious decisions or constraints, and delete comments that merely narrate the next line.
- Move a comment into XML documentation when it describes a caller-visible or subclass-visible contract.
