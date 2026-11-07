# Repository structure

OpenGE is a solution of small, single-purpose projects. This document covers how the code is organized and which commands must pass before a change is complete.

## Organization

- Production code lives in a source tree organized by area, with one project per area.
- Tests form a single project whose folder structure mirrors the production layout.
- Platform-specific implementations live in their own projects, separate from shared code.
- A console playground project lives apart from the source tree and references every engine project. It is the place to try the engine by hand; it is not production code and must not be referenced by anything else.
- Build settings, output locations, and documentation generation are configured once at the repository root. Do not repeat them per project.
- New projects join the existing solution under the appropriate folder.

## Platform separation

Shared projects stay platform-agnostic. Only dedicated platform projects reference native APIs, and they do so by implementing the shared abstractions.

Do not introduce a rendering, windowing, or input dependency into shared code to solve a platform problem. Extend the abstraction instead. See [Platform integration](Platforms.md).

## Commands

The repository requires a current .NET SDK and its pinned formatter.

```bash
dotnet tool restore     # restore pinned tools
dotnet build            # build every project
dotnet test             # run the test suite
dotnet csharpier check .   # verify formatting
dotnet csharpier format .  # apply formatting
```

Install the local git hooks once per clone:

```bash
dotnet tool restore      # restore pinned tools
dotnet husky install     # enable the repository hooks
```

CI restores tools, checks formatting, builds in Release, and runs the tests. A change is not complete until the same commands pass locally. Run the smallest relevant command while iterating, and the full suite before finishing.

Do not suppress warnings, disable documentation generation, or relax nullable annotations to make a build pass.
