# ADR-0003: Node/device authentication mechanism

- **Status**: Accepted (sub-fase 1.2)
- **Related**: agent.md §5.4, §6.2, Assumption #14; RF-01

## Context

agent.md requires nodes to authenticate with "API Key o token único por nodo, rotable... nunca una única key global", identified by their key, and never mixed with user JWT auth on the same endpoint. The authoritative endpoint contract (`agent.md` §5.4 / `spec.md` §12) has no REST endpoint for issuing or rotating a node's key — inventing one would violate the "no inventes endpoints fuera del spec" rule.

## Decision

- **Key format**: `<deviceId>.<secret>` — the `deviceId` half gives an O(1) `findById` lookup (avoiding a linear scan/hash comparison across every device), the `secret` half is argon2-hashed and stored as `devices.apiKeyHash` (an additive field, same pattern as `users.passwordHash`).
- **Provisioning and rotation are CLI-only** (`npm run device:provision`, `npm run device:rotate-key`), not REST — the same bootstrap philosophy already established by the Fase 1.1 Administrator seed (also not a REST call). Both print the plaintext key to stdout exactly once; only the hash is ever persisted.
- **A device must be provisioned before it can send data.** `POST /inventory`/`POST /access-events` never auto-create a `devices` document — the CLI creates the shell record (with `hostname`, `category`, `apiKeyHash`), and ingestion only *updates* `hostname`/`category`/`os`/`lastSeen` from the payload on each authenticated check-in.
- `NodeApiKeyGuard` is entirely separate from `JwtAuthGuard`/`RolesGuard` — never composed on the same route, per the dual-auth rule.

## Consequences

- Operationally, bringing a new node online requires an out-of-band CLI step by whoever manages the fleet, before that node's first successful check-in — this is a deliberate tradeoff for "no undocumented endpoints" over zero-touch auto-registration.
- `POST /inventory`'s `category` field is validated and **does** get synced to the device record on every ingest (a bug caught and fixed during review — the first draft accepted and validated `category` but silently discarded it), so a node can correct its own classification over time without a fresh CLI provision.
