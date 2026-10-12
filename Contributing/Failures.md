# Failure handling

Handle failures where an operation changes lifecycle state, transfers ownership,
or acquires resources. Each catch must restore a valid state, release an acquired
resource, or add diagnostic context before propagating the error.

## Lifecycle and cleanup

Module shutdown hooks also clean up incomplete startup. They must tolerate partial
initialization. Failed cleanup preserves dependencies until shutdown can be retried.
Scene switching unloads the new root before restoring the previous selection.
If scene recovery fails, shutdown releases any roots that remain loaded.

Use `finally` for required frame completion and borrowed native mappings or passes.
When both an operation and cleanup fail, expose both through `AggregateException`,
with the operation error first. Preserve the stack of an unchanged exception with
`throw;`. A reporting failure must not replace the failure being reported.

The application owns shutdown after its scheduling loop exits. Stop the engine
before destroying it. If stopping fails, retain the engine and its dependencies
for recovery; destruction requires the idle state.

## Diagnostic context

Failures after decoder selection keep their original exception type and attach `Mirage.ResourcePath`,
`Mirage.ResourceType`, and `Mirage.Decoder` to `Exception.Data`. Update failures
attach `Mirage.UpdateChannel`. Catch these at the application boundary to report
the failure. Avoid repeated wrapping when it adds no information.

CI restore, formatting, and build failures must fail their step and job.
