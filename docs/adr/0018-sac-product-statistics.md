# ADR-0018: SAC product statistics — model, ingestion, query and growth analysis

- **Status**: Accepted
- **Related**: ADR-0005 (ingestion idempotency), ADR-0008 (reports export), ADR-0009 (dashboard chart implementation), ADR-0010 (hardening: rate limiting and retention), ADR-0013 (an extension documented by its own ADR rather than folded into Fase 1)
- **Scope**: BL-031 (model and ingestion), BL-032 (query and export), BL-033 (ranking and growth dashboard)

## Context

The database engines of each client cloud already produce, once a day, one statistics
row per SAC database: size, debtor and account counts, financial sums, activity
counters, active-user figures, and the generation timestamp. IT-MAS had nowhere to
put it. The two existing ingestion endpoints model something else entirely —
`POST /inventory` is hardware/software inventory of a machine, `POST /access-events`
is a security event — and neither's schema, idempotency rule, or retention policy
fits a daily product metric.

The value of this data is almost entirely in its history: "which databases are
biggest" and "how fast is each one growing" are the questions being asked, and both
need months or years of snapshots to answer. That single fact drives most of the
decisions below, and it is where this collection diverges most sharply from the
existing ones.

## Decision

### Data model — `sac_statistics`

- **Contract names are English camelCase**, mapped from the SQL Server source
  columns (`[Base]` → `databaseName`, `[tamanoTotalBaseDatos]` → `totalSizeGb`, and
  so on; the full table lives in `docs/backlog.md` under BL-031). The source's
  Spanish column names never reach the API, matching every other `/api/v1` resource.
- **Two fields are added beyond the source**: `_id`, and `deviceId` resolved from the
  reporting node's API key. `deviceId` gives provenance ("which engine sent this")
  without a second lookup and, critically, is *not* accepted from the payload — an
  agent cannot claim to be reporting on another device's behalf. The DTO does not
  declare it, so the global `whitelist + forbidNonWhitelisted` pipe turns an attempt
  into a `400`.
- **Only `databaseName` and `generatedAt` are required.** Every metric is optional and
  nullable, exactly like the nullable source columns: a partial row is stored as
  reported rather than rejected or zero-filled, because a zero and a missing value
  mean different things to a growth series. Numerics reject negative values.
- **`generatedAt` is the engine's own time, never server-stamped.** It is the axis
  every aggregation groups on, so a delayed or retried upload must not be filed under
  the moment it happened to arrive.
- **The three financial sums are `Decimal128`, and are serialised as JSON strings.**
  The source columns are `numeric(18,2)`; 18 significant digits exceed the ~15.9 a
  JavaScript double represents exactly (2^53), so stored or transported as a number
  they would silently lose cents in aggregate totals. Input accepts a JSON string
  (the lossless form) or a JSON number (normalised to a string, and rejected if the
  double has already lost digits), and the wire response is always a plain decimal
  string — never BSON's `{"$numberDecimal": …}`.

### Append-only *without* a unique natural key

`inventories` and `access_events` both carry a unique index on their natural key, so a
node's retry is absorbed as an idempotent no-op (ADR-0005, `agent.md` §4). **This
collection deliberately does not.** A resend from the same engine stores an additional
record.

The reason is that there is no trustworthy natural key here. `(deviceId, generatedAt)`
would be a plausible one, but a re-run of the generating job legitimately produces a
new row for the same nominal day with corrected figures, and a unique index would
throw the correction away in favour of the first, wrong value. Deduplicating at read
time keeps the correction and loses nothing: **every aggregation takes the LAST
snapshot per (database, period)**, so duplicates and corrections both resolve to the
most recent figure.

The consequence is accepted explicitly: this collection can hold more rows than there
are logical observations, and a consumer reading raw rows (the BL-032 listing, the
export) sees all of them. Adding the unique index later is a purely additive change if
that trade-off ever stops being worth it.

### Retention — none by default

`inventories`, `access_events` and `audit_log` all get a TTL index with a default
window (ADR-0010). **This collection gets none unless `SAC_STATISTICS_RETENTION_DAYS`
is explicitly set**, in which case the existing `ensure-ttl-index.util.ts` applies it
to `generatedAt`.

A default here would be actively harmful rather than merely conservative: a 180-day
window silently makes year-over-year growth — the reason the data is collected — return
nothing, and it would do so months after deployment, long after anyone would connect
the two. There is deliberately no "un-TTL" path either; `ensureTtlIndex` only creates
or recreates, so removing retention after enabling it is a documented DBA action
(`DEPLOYMENT.md`), not something an unset variable does implicitly.

### Ingestion endpoint

- **`POST /api/v1/sac-statistics`, node-authenticated only** via `NodeApiKeyGuard`
  (`X-Node-Api-Key: <deviceId>.<secret>`). A user JWT gets a `401`: `agent.md` §5.4
  forbids mixing the two auth mechanisms on one endpoint.
- **It lives in `IngestionController`**, next to `POST /inventory` and
  `POST /access-events`, not in `SacStatisticsController` — which serves the JWT read
  half of the same resource path. Path plus method is what makes an endpoint, so
  `POST /sac-statistics` (node key) and `GET /sac-statistics` (JWT) are two endpoints
  with one mechanism each; splitting them across controllers is what keeps that
  boundary visible in the code rather than buried in per-route guard decorators.
- **The body is an array** — one engine reports every database of its run in a single
  call — answered with `201 {received, inserted}`. A top-level array needs
  `ParseArrayPipe`: the global `ValidationPipe` validates a body *DTO*, and an array is
  not one, so without it a batch of garbage would pass straight through. Its validation
  options do **not** inherit from the global pipe, so `whitelist` and
  `forbidNonWhitelisted` are restated on it; that is what makes an unknown field a
  `400`. An empty array is accepted as `{received: 0, inserted: 0}` — an engine with
  nothing to report is not an error.
- **Nothing is written to `audit_log`**, for the same reason `POST /inventory` writes
  nothing: the actor is a node, not a user, and `audit_log`'s contract is about people.
  Provenance lives on the record itself as `deviceId`.
- **No new `DeviceCategory`.** The reporting engine is provisioned as an ordinary
  `infrastructure` device. Adding a `database` category would drag in the alert engine,
  which discriminates on category in `evaluateAccessEvent` — a change with blast radius
  well outside this feature for no gain here.

## Consequences

- **A database engine must be provisioned first**, exactly like any other node
  (`npm run device:provision --category infrastructure`, or `POST /devices` per
  ADR-0016). `POST /sac-statistics` never creates a device.
- **Retention is an operator decision, and an unbounded collection is the default.**
  This is the intended trade-off, but it means disk growth on `sac_statistics` is
  monitored operationally rather than bounded by the application. At one row per
  database per day, a 100-database install accumulates ~36.5k rows a year — small, but
  it never stops.
- **Duplicate rows are visible in raw reads** and invisible in every aggregation. A
  consumer building its own analysis on the raw collection must apply the same
  last-snapshot-per-period rule, or it will double-count.
