# Repository structure

OpenGE is a solution of small, single-purpose projects. This document covers where code lives, how it is built, and which commands must pass before a change is complete. See [Architecture patterns](Architecture.md) for how those projects fit together and [Testing](Testing.md) for the test layout.

## Projects

```text
Source/Engine/<Area>/          Production projects (one project per area)
Tests/Engine/<Area>/           Test project mirroring the production layout
Tests/Tests.csproj             Single xUnit test project
Build/bin, Build/obj           Build output (see Directory.Build.props)
Contributing/                  Focused contributor documentation
Assets/, docfx.json            Documentation assets and docfx metadata
```

- Each production project lives at `Source/Engine/<Area>/OpenGE.<Area>.csproj` and targets `net10.0`.
- `Directory.Build.props` owns every project's compiler settings, output paths, and documentation generation, and routes binaries and intermediate files to `Build/bin` and `Build/obj`. Do not repeat those properties per project.
- `OpenGE.slnx` is the solution; add new projects there under the `/Source/Engine/` folder.

## Platform separation

Shared projects stay platform-agnostic. Rendering, input, and windowing concepts live in the shared projects; `SDL3` and `Raylib` are the only projects allowed to reference native platform APIs, and they do so by implementing the shared abstractions.

Do not introduce a rendering, windowing, or input dependency into a shared project to solve a platform problem. Extend the abstraction instead. See [Platform integration](Platforms.md).

## Prerequisites and commands

The repository requires the .NET 10 SDK and the pinned CSharpier tool.

```bash
dotnet tool restore --tool-manifest dotnet-tools.json   # restore pinned tools
dotnet build OpenGE.slnx                                # build every project
dotnet test OpenGE.slnx                                 # run the xUnit suite
dotnet csharpier check .                                # verify formatting
dotnet csharpier format .                               # apply formatting
```

CI runs restore, `csharpier check`, a Release build, and the test suite. A change is not complete until the same commands pass locally. Run the smallest relevant command while iterating, and the full suite before finishing.

Do not suppress warnings with `NoWarn`, disable `GenerateDocumentationFile`, or relax nullable annotations to make a build pass.
