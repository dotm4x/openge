# Testing

Use the repository's test framework and commands from [Repository structure](Structure.md). Tests describe behavior, not implementation.

## Structure

- Mirror the production layout inside the test project: the folder of a production type matches the folder of its test.
- Name test classes after the production type with a `Tests` suffix, and name test methods `Operation_Condition_ExpectedBehavior`.
- Add shared test infrastructure only when it is genuinely necessary, and keep it out of production projects.
- Prefer small helpers defined next to their tests over shared infrastructure or mocking frameworks.

## Arrange, act, assert

- Structure every test with Arrange, Act, and Assert, separated by blank lines and marked with comments when it aids clarity.
- Give each test one behavioral purpose, and use parameterized tests when several inputs verify the same behavior.

## Quality

- Test observable behavior, boundary conditions, and failure contracts.
- Assert meaningful outcomes, state changes, notifications, and exception types.
- Keep tests deterministic and independent, and release everything the test owns.
- Never weaken, delete, or skip a test to hide a failure, and never change production behavior only to satisfy a test.

## Verification

- Run the smallest relevant selection while iterating, and the full suite before finishing.
- Report failures, skipped tests, and untested behavior accurately. Never claim a test passed without running it.
