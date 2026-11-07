# Resource and logging services

Importing and logging are shared services with explicit ownership. See [Ownership and destruction](Ownership.md) for the general rules.

## Importing

- A decoder declares the resource type it produces and the file extensions it accepts.
- Decoded resources are owned by the caller; the importer adopts them into its cache and owns everything it stores.
- The importer owns its registered decoders.
- A decoded resource that cannot be adopted is destroyed, and cleanup failures are aggregated with the original error and retried during destruction.
- Import failures keep their original exception type and attach structured context describing the path, the requested resource type, and the decoder. See [Failure handling](Failures.md).

## Logging

- A logger owns its registered outputs.
- Outputs are ordered by priority, highest first, and receive messages that carry content, source, kind, and optional metadata.
- Scope messages to a component through a sourced logger instead of repeating the source string.
- Logging never replaces failure propagation: report and rethrow instead of swallowing an exception in a log call.
