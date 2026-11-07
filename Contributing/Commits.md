# Commits and review

Commit messages follow Conventional Commits, as used throughout this repository's history:

```text
<type>(<scope>): <imperative subject>

<body explaining motivation, behavior, and verification>
```

- Use a lowercase type, such as `feat`, `fix`, `refactor`, `docs`, `chore`, or `ci`, and an optional lowercase scope in parentheses.
- Write the subject in the imperative mood, without a trailing period, and keep it short.
- Explain motivation, behavior changes, and verification in the body rather than in the subject.
- Prefer one focused commit per change, and keep unrelated refactoring out of feature and fix commits.
- Preserve published history. Do not rewrite it.

## Before you finish

- [ ] The solution builds with no new warnings.
- [ ] The test suite passes.
- [ ] The formatter check passes.
- [ ] Public and protected members are documented per [Documenting the API](Documenting.md).
- [ ] Ownership, lifecycle, and failure behavior match the documented contract.
- [ ] Tests cover the new or changed behavior, including failure paths.
- [ ] The diff contains no unrelated changes.
