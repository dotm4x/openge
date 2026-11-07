# AGENTS

This repository's contributor rules live in [CONTRIBUTING.md](CONTRIBUTING.md). Read it before making any change and follow it as the source of truth for structure, patterns, documentation, tests, and commits.

Start with these sections:

- [Repository layout](CONTRIBUTING.md#repository-layout) and [Prerequisites and commands](CONTRIBUTING.md#prerequisites-and-commands)
- [Core patterns](CONTRIBUTING.md#core-patterns): reactive state, composition, modules, ownership, determinism, validation
- [XML documentation](CONTRIBUTING.md#xml-documentation)
- [Tests](CONTRIBUTING.md#tests) and [Commits](CONTRIBUTING.md#commits)

Deep dives worth opening for lifecycle or documentation work:

- [Failure handling](Contributing/Failures.md)
- [Ownership and destruction](Contributing/Ownership.md)
- [Documenting the API](Contributing/Documenting.md)

## Non-negotiables

- Build, test, and format with `dotnet build OpenGE.slnx`, `dotnet test OpenGE.slnx`, and `dotnet csharpier check .`. Do not claim success without running them.
- Keep shared projects platform-agnostic; only `Source/Engine/SDL3` and `Source/Engine/Raylib` reference native APIs.
- Preserve the documented ownership and lifecycle contracts, and destroy owned objects before calling `base.OnDestroy()`.
- Document every public and protected member in the established style, and mirror production layout under `Tests/Engine`.
- Make focused changes with no unrelated refactoring.
