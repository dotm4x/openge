# Documenting the API

OpenGE's documentation describes contracts: what an object represents, when an operation is valid, and what callers or subclasses are responsible for. Keep it close to the public API and consistent with the implementation. See [Ownership and destruction](Ownership.md) for lifetime conventions.

## Language and style

- Write API documentation in English, using brief, concrete sentences.
- Use `<summary>` for the symbol's responsibility. Prefer “Gets the registered outputs” or “Imports a resource” over descriptions of implementation steps.
- Use `<remarks>` for lifecycle rules, ownership, ordering, or other behavior that needs explanation beyond the summary.
- Avoid repeating a type's full contract on every member. Put details where callers will look for them.
- Use `<inheritdoc />` for overrides and interface implementations when the inherited contract applies. Add remarks only for meaningful differences.

Document stable behavior rather than current implementation details. “Dependencies are available before startup” is an API contract; the name of the private dictionary storing them is not.

## Coverage and XML

Document public types and members, enum values, interfaces, records, constructors, and protected extension points. Do not add XML comments to private or internal implementation details merely to increase coverage.

Use the tags that explain the contract:

- `<param>` and `<typeparam>` describe each parameter, including primary-constructor and positional-record parameters.
- `<returns>` clarifies results, especially ownership, nullability, and lookup failure.
- `<exception>` describes exceptions the implementation actually throws and that callers need to understand. Do not copy exception lists from unrelated APIs.
- `<see cref="..."/>` links to symbols when the reference helps explain behavior. Verify that it resolves.
- `<see langword="null"/>`, `<see langword="true"/>`, and `<see langword="false"/>` identify language keywords in prose.
- `<c>...</c>` marks literals, expressions, and identifiers that do not need links.

Keep tags correctly nested and closed. Use `<inheritdoc />` consistently, and `<para>` when a longer remarks block needs distinct paragraphs.

For ordinary constructors, state what they initialize and describe the arguments without repeating every property's documentation. For primary constructors, follow the surrounding OpenGE style: document initialization in the summary, explain the type's role in remarks, and place parameter tags on the type declaration.

## Describe lifetime explicitly

Distinguish owning an object from owning a collection or store that references it. State whether registration or a returned value transfers ownership. Describe removal and replacement when callers can mutate an owning collection.

```csharp
/// <summary>
/// Gets the registered logging outputs.
/// </summary>
/// <remarks>
/// The logger owns and destroys its registered outputs.
/// Removing an output returns ownership to the caller without destroying it.
/// </remarks>
public readonly ReactiveSet<LogOutput> Outputs = [];
```

For a borrowed relationship, use direct wording such as “The renderer references the window without owning or destroying it.” Injected dependencies are always borrowed. Native handles should document their owner and validity period.

Keep these claims grounded in the code. A readonly field, a constructor argument, or a `Store<T>` does not by itself establish ownership.

## Describe lifecycle and extension points

OpenGE commonly separates composition, configuration, startup, updates, shutdown, and destruction. Document the phases that matter to a particular API:

- When `Compose()` runs, who owns its results, and whether constructor-provided objects are already registered.
- What is available in `Configure()`, whether it runs once, and what happens if initialization fails.
- Which operations require injection, loading, or a running module.
- Whether stopping permits a later restart, and what destruction releases permanently.
- Whether an override must call the base implementation and any important ordering requirement.

Keep composition exceptions explicit rather than copying the default ownership sentence into every class. A processing view borrowing existing scene objects has a different contract from a manager adopting newly composed contents.

```csharp
/// <summary>
/// Composes the outputs managed by this logger.
/// </summary>
/// <returns>The outputs to register, in enumeration order.</returns>
/// <remarks>
/// Composition occurs once before configuration.
/// The logger owns and destroys the returned outputs.
/// </remarks>
protected virtual IEnumerable<LogOutput> Compose()
{
    yield break;
}
```

Examples should illustrate a contract with a small, coherent fragment. Avoid entire application setups or lists of private implementation details.

## Describe reactive state and units

Explain what a `Store`, `Signal`, or reactive collection represents. State notification timing when it matters: before removal, after insertion, only when a value changes, or during an explicit lifecycle transition. Do not promise that destruction emits ordinary change events unless the implementation does so.

For options and numeric values, document defaults, units, valid ranges, and the meaning of `null` or sentinel values. Use consistent terms for local and world coordinates, window and screen coordinates, radians, pixels, seconds, and update rates. Distinguish a configured target rate from a measured rate.

Read-only views restrict access; they do not imply ownership or guarantee that the underlying value never changes.

## AI-assisted documentation

Much of OpenGE's API documentation is written or refined with AI assistance. This is intentional and part of the project's documentation workflow.

OpenGE has many related APIs that share concepts such as ownership, lifecycle, composition, and reactive state. AI makes it practical to document these consistently across the codebase and is particularly useful for repository-wide documentation work.

AI-generated documentation must still be grounded in the implementation. It should inspect the relevant code and surrounding APIs, follow the conventions in this document, and never invent behavior, ownership, exceptions, guarantees, or lifecycle rules.

The implementation is always the source of truth.

AI-assisted documentation should preserve established terminology, distinguish ownership from borrowing, use `<inheritdoc />` where appropriate, avoid unnecessary documentation of internal implementation details, and keep related APIs documented at a similar level of detail.

Human review is especially important for architectural and behavioral contracts. The goal is not for documentation to appear human-written or AI-written, but to be accurate, consistent, useful, and maintainable.

## Comments and verification

Keep ordinary comments for non-obvious decisions or constraints. Remove comments that merely narrate the next line of code. Move a comment into XML only when it describes a caller-visible or subclass-visible contract.

For a documentation-only change, preserve signatures, visibility, architecture, and executable behavior. Check examples, relative Markdown links, XML references, and the final diff.

When changing C# XML comments, build with documentation enabled and fix warnings introduced by the change. Do not suppress warnings with `NoWarn` or disable `GenerateDocumentationFile`. Keep C# examples and edited source compatible with CSharpier. Markdown-only edits generally need a content and link review rather than a build or test run.
