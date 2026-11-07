# Testing

Use xUnit and the existing build and test commands from [Repository structure](Structure.md). Tests describe behavior, not implementation.

## Structure

- Mirror the production layout: `Source/Engine/Numerics/Geometry/Mesh2D.cs` corresponds to `Tests/Engine/Numerics/Geometry/Mesh2DTests.cs`.
- Name test classes after the production type with a `Tests` suffix, and name test methods `Operation_Condition_ExpectedBehavior`.
- Add shared test infrastructure only when it is genuinely necessary, and keep it out of production projects.
- Prefer small in-test helpers such as `TestWindow`, `RecordingBackend`, and `TestEngine` over shared infrastructure or mocking frameworks.

## Arrange, act, assert

- Structure every test with Arrange, Act, and Assert, separated by blank lines and marked with `// Arrange`, `// Act`, and `// Assert` when it aids clarity.
- Give each test one behavioral purpose, and use parameterized tests when several inputs verify the same behavior.

## Quality

- Test observable behavior, boundary conditions, and failure contracts.
- Assert meaningful outcomes, state changes, events, and exception types.
- Keep tests deterministic and independent, and dispose everything the test owns.
- Never weaken, delete, or skip a test to hide a failure, and never change production behavior only to satisfy a test.

## Verification

- Run the smallest relevant test selection while iterating, and the full suite before finishing.
- Report failures, skipped tests, and untested behavior accurately. Never claim a test passed without running it.
