# Code conventions

These rules apply to every project and stay stable while names change. See [Architecture patterns](Architecture.md) for structural conventions and [Documenting the API](Documenting.md) for documentation.

## Naming

- Use clear, complete English names. Avoid unnecessary abbreviations and ambiguous identifiers.
- Use Pascal case for types, methods, properties, and events; camel case for parameters and local variables.
- Prefix private instance fields with an underscore followed by camel case.
- Prefix generic type parameters with `T` plus a meaningful Pascal-cased name, and never with a bare `T`.
- Name files after the concept they contain, without the project prefix. A file may declare several related types when they form one concept.
- Use consistent terminology across related APIs. The same concept keeps the same noun everywhere.

## C# style

- Use file-scoped namespaces and modern, idiomatic C# for the configured language version.
- Follow the repository-wide compiler and style configuration; never disable nullable warnings or code analysis to make a build pass.
- Use `var` only when the assigned type is apparent from the initializer.
- Prefer properties for exposed state and methods for meaningful operations.
- Keep classes non-sealed unless sealing is required by a concrete reason.
- Introduce an interface when it establishes a meaningful contract, not to wrap a single implementation.
- Respect nullable annotations. Do not add redundant null checks for non-nullable parameters or use null-forgiving operators without justification.
- Avoid wrappers, forwarding methods, inheritance layers, and abstractions that only restate an existing one.
- Preserve public behavior unless the change explicitly requires a behavioral change.

## Determinism and iteration

- Never iterate a hash-ordered collection when order affects behavior. Maintain a registration-order sequence or iterate a cached, ordered snapshot instead.
- Snapshot a collection before iterating when the loop body may register, remove, or destroy entries.
- Use a stable sort when ties must preserve registration order.
- Cache derived orderings and invalidate them through change notifications instead of recomputing on every tick.

## Validation and failures

- Validate constructor arguments eagerly with the dedicated argument exceptions, including the parameter name.
- Validate mutable state where it is consumed. Observable state can change after construction, so a value that must stay valid is rechecked before use.
- Distinguish lifecycle failures, invalid-operation failures, and unsupported-capability failures with the matching exception family.
- Recover where an operation changes lifecycle state, transfers ownership, or acquires resources. See [Failure handling](Failures.md).

## Comments

- Keep comments for non-obvious decisions or constraints, and delete comments that merely narrate the next line.
- Move a comment into XML documentation when it describes a caller-visible or subclass-visible contract.
