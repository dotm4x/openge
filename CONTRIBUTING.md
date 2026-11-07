# Contributing

Contribution guidance is maintained in the [`Contributing/`](Contributing/) directory. Start with the document that matches your change, and follow the documents it links to.

> [!NOTE]
> OpenGE is experimental and its identifiers change regularly. These documents describe stable rules and concepts rather than current type, member, or path names. Locate the concept you need in the codebase instead of relying on a name quoted from a guide.

## Working in this repository

- [Repository structure](Contributing/Structure.md): projects, platform separation, build and test commands, CI expectations.
- [Code conventions](Contributing/Conventions.md): naming, C# style, determinism, validation, comments.
- [Commits and review](Contributing/Commits.md): commit format and the checklist to complete before finishing.

## Architecture

- [Architecture patterns](Contributing/Architecture.md): reactive state, options, composition, modules, collections.
- [Ownership and destruction](Contributing/Ownership.md): lifetime responsibility, borrowed references, composition, and teardown.
- [Failure handling](Contributing/Failures.md): recovery boundaries, partial startup, cleanup errors, and diagnostic context.

## Subsystems

- [Rendering](Contributing/Rendering.md): commands, spaces, the renderer, backend hooks, and graphics resources.
- [Scheduling](Contributing/Scheduling.md): scheduler, update channels, and update timing.
- [Platform integration](Contributing/Platforms.md): windowing and input, and the shared/platform boundary.
- [Resource and logging services](Contributing/Services.md): importing, decoding, and logging.

## Documentation and tests

- [Documenting the API](Contributing/Documenting.md): XML documentation, lifecycle contracts, reactive state, and examples.
- [Testing](Contributing/Testing.md): layout, naming, arrange-act-assert, and quality expectations.

Add future contribution rules as focused documents in `Contributing/` and link them from this file.

Shared development practice also follows the [Rayscale Development and Implementation Guidelines](https://gitlab.com/rayscale/development/guidelines).
