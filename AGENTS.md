# AGENTS

All contributor conventions for the codebase live in [`Contributing/`](Contributing/). [`CONTRIBUTING.md`](CONTRIBUTING.md) indexes them; read it first, then open the documents that apply to your change. Agent-specific directives appear at the end of this file.

Recommended reading order:

1. [Repository structure](Contributing/Structure.md): where code lives and which commands must pass.
2. [Code conventions](Contributing/Conventions.md) and [Architecture patterns](Contributing/Architecture.md): how code is written and composed.
3. [Ownership and destruction](Contributing/Ownership.md) and [Failure handling](Contributing/Failures.md): lifetime and recovery contracts.
4. [Documenting the API](Contributing/Documenting.md) and [Testing](Contributing/Testing.md): documentation and test requirements.
5. [Commits and review](Contributing/Commits.md): commit format and the completion checklist.

Do not duplicate or re-derive these rules elsewhere. If a rule is missing or wrong, update the document in `Contributing/` and link it from [`CONTRIBUTING.md`](CONTRIBUTING.md).

## Commit trailers

Never add `Co-authored-by` trailers or any other co-author attribution to commits in this repository. Commit messages end with the body; they carry no trailers.
