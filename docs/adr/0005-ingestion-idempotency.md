# ADR-0005: Ingestion idempotency mechanism

- **Status**: Accepted (sub-fase 1.2)
- **Related**: agent.md §4 ("la ingesta debe tolerar reenvíos de nodos"), §5.3; CA-01

## Context

Nodes on unreliable networks may retry a send. The ingestion API must tolerate an exact resend without creating a duplicate record or double-firing side effects (alert evaluation).

## Decision

- **Natural-key uniqueness**, not a separate idempotency-token store: a unique compound Mongo index on `inventories` over `(deviceId, timestamp)`, and on `access_events` over `(deviceId, level, user, timestamp, action)`.
- The **node supplies its own `timestamp`** in the ingestion payload (the moment it collected the data), never the server's receipt time — this is what makes the natural key meaningful as a retry-detector: an identical resend has an identical `timestamp`, while genuinely new data has a new one.
- On a Mongo duplicate-key error (code 11000), the repository's `insert()` returns `{created: false}` instead of throwing, and the controller still responds `201` — from the node's point of view, a retry looks exactly like the original success.
- Downstream side effects (the RF-03 diff/alert-engine evaluation) only run when `created === true` — an idempotent retry is not a "new" event to re-evaluate.

## Consequences

- No idempotency-key header or separate collection was needed — the tradeoff is that a node **must** send a stable, meaningful `timestamp` per observation; a node that stamped every retry with `Date.now()` at send-time (rather than collection-time) would defeat this and create duplicates. This is a contract expectation on the node side, out of this system's direct control (agents are out of development scope per agent.md §1), documented here so it's not lost.
