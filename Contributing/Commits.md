# Commits and review

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
- Prefer one focused commit per change, and keep unrelated refactoring out of feature and fix commits.
- Preserve existing history. Do not rewrite published commits.

## Before you finish

- [ ] `dotnet build OpenGE.slnx` succeeds with no new warnings.
- [ ] `dotnet test OpenGE.slnx` passes.
- [ ] `dotnet csharpier check .` passes.
- [ ] Public and protected members are documented per [Documenting the API](Documenting.md), and docfx-visible references resolve.
- [ ] Ownership, lifecycle, and failure behavior match the documented contract.
- [ ] Tests cover the new or changed behavior, including failure paths.
- [ ] The diff contains no unrelated changes.
