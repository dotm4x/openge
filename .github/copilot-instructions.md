# Copilot Instructions

## Principles

- Follow the repository's established architecture, conventions, and patterns.
- Prefer correctness, clarity, consistency, and simplicity over unnecessary abstraction.
- Make focused changes. Avoid unrelated refactoring, speculative features, and unnecessary dependencies.
- Inspect existing implementations and related code before introducing new patterns.

## Naming

- Use clear, complete English names. Avoid unnecessary abbreviations and ambiguous identifiers.
- Use `PascalCase` for types, methods, properties, and events. Use `camelCase` for parameters and local variables.
- Prefix private instance fields with an underscore, followed by `camelCase`.
- Prefix generic type parameter names with `T`, immediately followed by a meaningful `PascalCase` name, without a hyphen or underscore. Use names such as `TValue`, `TKey`, `TElement`, and `TResult`. Never use a bare `T`.
- Use consistent terminology across related APIs and projects.

## C#

- Use modern, idiomatic C# compatible with the configured language version.
- Follow `.editorconfig` and `Directory.Build.props` for formatting, compiler settings, nullable reference types, and code analysis.
- Use file-scoped namespaces and preserve established namespace and import conventions.
- Use explicit types when they improve clarity; use `var` when the assigned type is apparent.
- Prefer properties for exposed state and methods for meaningful operations.
- Keep classes non-sealed unless sealing is justified by a concrete requirement.
- Introduce interfaces when they establish meaningful contracts, not merely to abstract a single implementation.
- Respect nullable annotations and existing contracts. Do not add redundant null checks for non-nullable parameters or use unjustified null-forgiving operators.
- Avoid unnecessary wrappers, forwarding methods, inheritance layers, and abstractions.
- Preserve public API behavior unless a change is explicitly required.

## OpenGE Architecture

- Keep shared concepts, elements, geometry, layouts, drawing, and platform-specific behavior separated according to their existing responsibilities.
- Place each new type in the project that owns its responsibility.
- Follow existing element composition, collection, event, state, and lifecycle patterns.
- Do not introduce rendering or platform dependencies into shared projects without a clear architectural reason.
- Avoid duplicate abstractions and parallel APIs that solve the same problem.

## Documentation

- Document public and protected APIs with XML documentation, following existing conventions.
- Keep documentation concise, accurate, and consistent with actual behavior.
- Document generic type parameters, parameters, return values, exceptions, and examples where appropriate.
- Never document guarantees the implementation does not provide.

## Tests

### Structure

- Use xUnit for all automated tests.
- Mirror the `Source/` directory structure inside `Tests/`. Each production project corresponds to a directory named after the project without the `OpenGE.` prefix.
- Preserve all nested directories relative to their production project.
- Name each test class after its production type, appending `Tests`.
- Place each test class in the directory corresponding to its production type.
- Do not group tests into unrelated directories or organize them by test category when that breaks the mirrored structure.
- Add shared test infrastructure only when genuinely necessary. Keep it separate from production code and avoid unnecessary abstractions.

For example:

```text
Source/OpenGE.Numerics/Geometry/Mesh2D.cs
Tests/Numerics/Geometry/Mesh2DTests.cs
```

```text
Source/OpenGE.Commons/Events/Store.cs
Tests/Common/Events/StoreTests.cs
```

### Naming and Organization

- Name test methods using `Operation_Condition_ExpectedBehavior`.
- Give each test one clearly defined behavioral purpose.
- Use parameterized tests when multiple inputs verify the same behavior.
- Keep test data explicit and understandable.
- Follow existing namespace conventions and ensure namespaces reflect the mirrored test structure.

### Arrange–Act–Assert

- Structure every test using the Arrange–Act–Assert (AAA) pattern.
- **Arrange:** establish the inputs, initial state, and dependencies.
- **Act:** execute the single operation under test.
- **Assert:** verify the observable result and any relevant side effects.
- Keep these phases clearly separated with blank lines. Use `// Arrange`, `// Act`, and `// Assert` comments when they improve clarity or make the phases explicit.
- Avoid mixing setup, execution, and assertions in the same phase.
- Keep each test focused. Extract shared setup only when doing so improves readability without hiding important behavior.

### Test Quality

- Test observable behavior, public contracts, boundary conditions, failure cases, and relevant regressions.
- Assert meaningful outcomes, state changes, events, exceptions, and side effects as appropriate.
- Prefer specific xUnit assertions that clearly communicate expected and actual values.
- Test exception types and relevant exception properties when they are part of the contract.
- Keep tests deterministic, isolated, and independent.
- Dispose of resources owned by tests and clean up temporary state.
- Avoid arbitrary delays, shared mutable state, unnecessary mocking, and tests that depend on execution order.
- Derive expected behavior from implementation contracts, documentation, and established specifications. Do not invent requirements when behavior is ambiguous.
- Never weaken, delete, or skip tests merely to conceal failures.
- Do not modify production code solely to make a test pass.

### Verification

- Run the relevant tests after each logical batch of changes.
- Run the complete test suite after finishing a coherent set of changes.
- Follow the repository's existing build and test commands.
- Never claim a test passed unless it was executed successfully.
- Report failures, skipped tests, untested behavior, and unresolved ambiguities accurately.

## Changes

- Inspect relevant files and their callers before changing behavior or public APIs.
- Make the smallest coherent change that fully addresses the task.
- Preserve compatibility unless breaking changes are explicitly requested.
- Keep code, tests, documentation, and project configuration consistent.
- Report significant decisions, behavior changes, verification results, and remaining issues clearly.