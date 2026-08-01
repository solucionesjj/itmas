# ADR-0006: Alert engine and rule configuration

- **Status**: Accepted (sub-fase 1.3)
- **Related**: agent.md §4 ("las reglas de alertas son configurables... el motor lee configuración, no la hardcodea"), Assumption #7; CA-02, CA-03, CA-11

## Context

Two alert types are required — `resource_change` and `off_hours_access` — driven by admin-configurable rules in `alert_rules`, with sensible behavior out of the box (CA-02/CA-03 must work without requiring manual setup first).

## Decision

- **One enabled rule per `type`**, enforced by a unique index — `POST /alert-rules` on a type that already has a rule returns `409`; changing behavior means `PATCH`-ing the existing rule, not creating a second one. This keeps "which rule applies" unambiguous without needing rule-priority logic.
- **Default rules seeded on first boot** (same `onModuleInit()` check-then-create pattern as the Administrator seed): `resource_change` enabled with `resources: ['cpu','ram','disks']`; `off_hours_access` enabled with `habitualHours: {from: '07:00', to: '19:00'}` (UTC by default, timezone overridable via `HABITUAL_HOURS_TZ`). These are the literal defaults agent.md's Assumption #7 already specified — seeding them is what makes CA-02/CA-03 pass on a fresh deployment with zero admin configuration, while remaining fully editable afterward.
- **The engine only ever reads rule config from the DB** at evaluation time — no resource/threshold/hour value is hardcoded in `AlertEngineService`. Disabling a rule via `PATCH /alert-rules/:id` immediately stops it from firing on the next ingestion (verified manually, not just asserted).
- **`off_hours_access` scope**: only evaluated for `action: 'login'` on `category: 'infrastructure'` devices (CA-03 says "acceso a **servidor**" — collaborator workstations are never subject to this rule), for both `level: 'os'` and `level: 'database'` access events.
- **Overnight-wrapping hour ranges** (e.g. `from: '22:00', to: '06:00'`) are handled correctly, not just the same-day case — a real config an admin might reasonably set for a night-shift habitual window.
- **Alert evaluation only runs on genuinely new ingestion records** (see ADR-0005) — an idempotent retry never double-fires an alert.

## Consequences

- Two alert types are a hard architectural assumption baked into the `AlertRuleType` enum and the unique-per-type index; adding a third alert type in a later phase means a schema/index migration, not just a new DB document.
- Because the engine is synchronous within the ingestion request (no message queue), alert evaluation adds to the ingestion request's latency — acceptable at the NFR's stated scale ("hundreds of nodes", <500ms target), revisit if that scale assumption changes materially.
