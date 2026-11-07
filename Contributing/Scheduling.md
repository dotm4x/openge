# Scheduling

The scheduler drives the main loop and distributes time to update channels. See [Architecture patterns](Architecture.md) for composition and lifecycle rules.

- The scheduler owns its channels. A channel groups updatable entries with a priority and an optional target rate, and borrows those entries.
- The effective rate of a channel is the lower positive limit imposed by the channel and the scheduler. A non-positive rate imposes no limit, and no channel updates more than once per scheduler iteration.
- A rate-limited channel keeps the remainder of its accumulated time, so the average rate matches the target instead of drifting.
- A single iteration reports at most a bounded delta, so a stalled process cannot inject an outsized step into updates.
- Reported timing distinguishes the delta and target of the current update from the instantaneous rate of recent updates; do not present either as a measured average.
- Channels snapshot their entries before updating, so an entry may register or remove entries while it updates.
- Order-sensitive scheduling uses the cached priority order rather than hash order. See [Code conventions](Conventions.md).
