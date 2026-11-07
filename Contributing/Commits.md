# Commits and review

Commit messages follow Conventional Commits, as used throughout this repository's history:

```text
<type>(<scope>): <imperative subject>

<body explaining motivation, behavior, and verification>
```

- Use one of the accepted lowercase types: `feat`, `fix`, `refactor`, `docs`, `chore`, `ci`, `build`, `test`, `style`, `perf`, or `revert`.
- Add an optional lowercase scope in parentheses. Scopes are free-form, so an area can be renamed without changing the rules.
- Write the subject in the imperative mood, without a trailing period, and keep it short.
- Explain motivation, behavior changes, and verification in the body rather than in the subject.
- Prefer one focused commit per change, and keep unrelated refactoring out of feature and fix commits.
- Preserve published history. Do not rewrite it.

## Local hooks

The repository ships git hooks that mirror the checks below, so most problems are caught before they reach CI:

- **Before committing:** staged C# files are formatted and the formatted result is re-staged. Committing still aborts if formatting fails.
- **On the commit message:** the message is validated against the format and the allowed types above. The subject limit is enforced as well.
- **Before pushing:** the formatter check runs over the repository and the test suite must pass.

The hooks live in the hooks folder next to the tools manifest and run through the pinned tools, so install them once per clone as described in [Repository structure](Structure.md). Change a rule by editing that configuration, never by editing the hook scripts. Bypass the hooks only in an emergency; CI enforces formatting, build, and tests regardless.

## Before you finish

- [ ] The solution builds with no new warnings.
- [ ] The test suite passes.
- [ ] The formatter check passes.
- [ ] Public and protected members are documented per [Documenting the API](Documenting.md).
- [ ] Ownership, lifecycle, and failure behavior match the documented contract.
- [ ] Tests cover the new or changed behavior, including failure paths.
- [ ] The diff contains no unrelated changes.
