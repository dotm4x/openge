# Resource and logging services

Importing and logging are shared services with explicit ownership. See [Ownership and destruction](Ownership.md) for the general rules.

## Importing

- Decoders derive from `Decoder`, declare the `ResourceType` they produce, and accept the file extensions they support.
- Resources returned by `OnDecode` are owned by the caller. The importer adopts them into its cache and owns everything it stores.
- The importer owns its registered decoders.
- Failures after a decoder has been selected keep the original exception type and attach `OpenGE.ResourcePath`, `OpenGE.ResourceType`, and `OpenGE.Decoder` to `Exception.Data`. See [Failure handling](Failures.md).
- A decoded resource that cannot be adopted is destroyed. Cleanup failures are aggregated with the original error and retried during destruction.

## Logging

- `Logger` owns its registered outputs.
- Outputs are ordered by `LogOutputPriority`, highest first, and receive `LogMessage` values with content, source, kind, and optional metadata.
- Use `Logger.From(source)` to scope messages to a component instead of repeating the source string.
- Logging must not replace failure propagation: report and rethrow rather than swallowing an exception in a log call. Update failures attach `OpenGE.UpdateChannel` to the exception.
