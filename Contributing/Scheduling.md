# Scheduling

The scheduler drives the main loop and distributes time to update channels. See [Architecture patterns](Architecture.md) for composition and lifecycle rules.

- `Scheduler` owns its channels. `UpdateChannel` groups `IUpdatable` entries with a priority and an optional target rate.
- The effective rate of a channel is the lower positive rate of the channel and the scheduler. A non-positive rate imposes no limit.
- A channel never performs more than one update per scheduler iteration.
- A rate-limited channel keeps the remainder of its accumulated time, so the average rate matches the target instead of drifting.
- `UpdateContext.DeltaTime` is the delta of that update, clamped by `Scheduler.MaximumDeltaTime` so a stalled iteration cannot inject an outsized delta.
- `UpdateContext.UpdateRate` is the configured target for the update, while `UpdateChannel.UpdateRate` is the instantaneous rate of the latest update, not a measured average.
- Entries are borrowed. Channels snapshot their entries before updating, so an entry may register or remove entries while it updates.
- Order-sensitive work uses the cached priority order, not hash order. See [Code conventions](Conventions.md).
