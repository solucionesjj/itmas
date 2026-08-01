# IT-MAS Backend

NestJS API for IT-MAS. This package currently implements sub-phases **1.1 — Autenticación y RBAC base**, **1.2 — Ingesta de inventarios y eventos**, **1.3 — Motor de alertas**, **1.4 — Gestión de usuarios**, **1.5 — Portal de consulta (equipos/estadísticas)**, and **1.6 — Reportes y exportación** (see `/agent.md` §17 at the repo root for the full sub-phase breakdown). Only sub-phase 1.7 (hardening/quality gates) remains.

## Requirements

- Node.js 20 (see `.nvmrc` at repo root — run `nvm use` from the repo root before installing)
- A reachable MongoDB instance for running the app (not required for tests — `mongodb-memory-server` provides an ephemeral instance for the e2e suite)

## Setup

```bash
npm install
cp .env.example .env   # then fill in real secrets/URI — never commit .env
```

## Run

```bash
npm run start:dev   # watch mode
npm run start        # single run
npm run start:prod   # run the compiled dist/main.js
```

The API listens under the `/api/v1` prefix (e.g. `POST /api/v1/auth/login`, `GET /api/v1/health`).

On first boot, if `ADMIN_SEED_USERNAME` doesn't already exist as a user, a default Administrator is created from `ADMIN_SEED_*` env vars with `mustChangePassword: true`.

## Test

```bash
npm run lint        # ESLint (must be clean)
npm run build        # TypeScript build (must succeed)
npm test             # Jest unit tests
npm run test:cov     # Jest unit tests with coverage
npm run test:e2e     # Integration tests (spins up mongodb-memory-server per suite)
```

## Endpoints implemented so far

- `POST /api/v1/auth/login` — rate-limited (`LOGIN_RATE_LIMIT_MAX`/`LOGIN_RATE_LIMIT_WINDOW_SEC`), returns `{ accessToken, refreshToken }`.
- `POST /api/v1/auth/refresh` — rotates the token pair; rejects if the refresh token's embedded `tokenVersion` no longer matches the user's current one.
- `POST /api/v1/auth/logout` — requires a valid access token; bumps `tokenVersion`, invalidating all outstanding refresh tokens.
- `POST /api/v1/auth/change-password` — requires a valid access token and the current password; bumps `tokenVersion` and clears `mustChangePassword`.
- `POST /api/v1/inventory` — requires a node API key (`X-Node-Api-Key` header); persists an append-only inventory record, logs and (per the `resource_change` alert rule) raises an alert when CPU/RAM/disks changed vs. the device's previous inventory.
- `POST /api/v1/access-events` — requires a node API key; persists an OS/database login/logout event and, for an `infrastructure`-category device's `login` outside the configured habitual hours, raises an `off_hours_access` alert.
- `GET /api/v1/health` — process + Mongo connection liveness.
- `GET /api/v1/alert-rules`, `POST /api/v1/alert-rules`, `PATCH /api/v1/alert-rules/:id` — Administrador only. Two rules (`resource_change`, `off_hours_access`) are seeded on first boot if missing; one rule per `type` is enforced (a unique index), so `POST` for an existing type returns `409` — use `PATCH` instead.
- `GET /api/v1/alerts` (paginated, filterable by `type`/`status`/`from`/`to`), `PATCH /api/v1/alerts/:id` (body `{ status: "open"|"reviewed" }`) — Administrador and Auditor.
- `GET /api/v1/users` (optionally filtered by `role`/`active`), `POST /api/v1/users` (body `{ username, email, password, role }`), `PATCH /api/v1/users/:id` (body, all optional: `email`, `role`, `active`, `password`) — Administrador only. Responses never include `passwordHash`/`tokenVersion`.
- `GET /api/v1/devices` (paginated, filterable by `category`/`osName`/`hostname`), `GET /api/v1/stats/devices`, `GET /api/v1/stats/os` — open to all three roles (Administrador/Usuario/Auditor).
- `GET /api/v1/reports/export?reportType=devices|alerts&format=csv|pdf` (plus filters — devices: `category`/`osName`/`hostname`; alerts: `alertType`/`status`/`from`/`to`) — Usuario/Auditor/Administrador can export `reportType=devices`; only Administrador/Auditor can export `reportType=alerts` (enforced in `ReportsService`, not just the route guard). Returns a raw file download (`Content-Type`/`Content-Disposition`), not the JSON envelope.

## Provisioning a node's API key (sub-phase 1.2)

There is no REST endpoint for this — none is in the authoritative API contract, so it's a deliberately out-of-band CLI step (same bootstrap philosophy as the Administrador seed in sub-phase 1.1):

```bash
# Requires MONGO_URI (and the rest of .env) to already be set in your shell/`.env`.
npm run device:provision -- --hostname PC-001 --category collaborator
#  -> prints deviceId + a one-time API key: "<deviceId>.<secret>"

npm run device:rotate-key -- --device-id <deviceId>
#  -> invalidates the previous key for that device, prints a new one
```

Configure the node to send the printed key verbatim as the `X-Node-Api-Key` header on every `POST /api/v1/inventory` / `POST /api/v1/access-events` request. The key is shown once and only its argon2 hash is stored — if it's lost, rotate it.

`timestamp` in both ingestion payloads must be the node's own collection/event time (ISO 8601), not left for the server to fill in: it's also half of the idempotency key (see below), so a genuine retry of the same reading must resend the same `timestamp` to be deduplicated correctly.

## Design notes worth knowing before extending this module

- `users` gains two fields beyond spec.md's example payload: `mustChangePassword` (forces a password change for the seeded admin / admin-created users) and `tokenVersion` (refresh-token revocation counter, bumped on logout/change-password — avoids adding a new collection for token state). See `agent.md` §15 Assumption #11 area / §5.3 for the collection model.
- Access tokens carry `{ sub, username, role, mustChangePassword }`; there is no `/auth/me` endpoint (not part of the contract) — the frontend decodes the JWT client-side.
- `RolesGuard` (in `src/common/guards`) denies by default if an endpoint has no `@Roles(...)` metadata — this is intentional per agent.md's "no declared role = implementation error" rule. Wired (with `JwtAuthGuard`) on `alert-rules`/`alerts` (1.3) and `users` (1.4).
- Brute-force protection (`@nestjs/throttler`) is applied only to `POST /auth/login`. Broader API-wide rate limiting is deferred to sub-phase 1.7.
- `devices` gains two fields beyond spec.md's example payload: `apiKeyHash` (argon2 hash of a node's API key secret half — same approach as `users.passwordHash`) and, as of every ingestion, its `hostname`/`category`/`os`/`lastSeen` are refreshed from the node's own reported values (unset until its first successful ingestion; never set at provisioning beyond `hostname`/`category`).
- Node API keys are `<deviceId>.<secret>` — the `deviceId` half gives `NodeApiKeyGuard` an O(1) lookup instead of scanning every device's hash to find a match. Node-key auth (`NodeApiKeyGuard`) and user JWT auth (`JwtAuthGuard`/`RolesGuard`) are completely separate guards, never combined on one endpoint (agent.md §5.4). The guard also resolves the device's `category` onto the request (`@DeviceCategoryParam()`) so the alert engine doesn't need a second DB lookup.
- Idempotency for both ingestion endpoints (agent.md §4: "la ingesta debe tolerar reenvíos de nodos") is natural-key based, not a separate idempotency-token store: a unique Mongo index on `(deviceId, timestamp)` for `inventories` and `(deviceId, level, user, timestamp, action)` for `access_events`. A duplicate-key error (code 11000) from a resend is treated as a successful no-op retry (`201`, no second document), not an error — and the alert engine is only ever invoked on a genuinely new record, so a retry can't double-fire an alert either.
- **Alert engine (sub-phase 1.3)**: `AlertEngineService` reads `alert_rules` from the DB — it never hardcodes thresholds. `evaluateResourceChange` fires only for resources the (single, DB-configured) `resource_change` rule enables; `evaluateAccessEvent` fires only for a `login` on an `infrastructure`-category device outside the `off_hours_access` rule's `habitualHours` window (converted via `HABITUAL_HOURS_TZ`, default UTC — `habitual-hours.util.ts` also handles an overnight-wrapping range like `22:00`–`06:00`). `alert_rules` has a unique index on `type`: exactly one rule per type is the model this project uses, so a second `POST` for an existing type is a `409`, not a new rule — `PATCH` is how you change it.
- Two default rules are seeded on first boot if missing (`AlertRulesService.onModuleInit()`, same bootstrap pattern as the sub-phase 1.1 Administrador seed): `resource_change` enabled with `resources: [cpu, ram, disks]`, and `off_hours_access` enabled with `habitualHours: { from: "07:00", to: "19:00" }` (agent.md Assumption #7's defaults, now the actual seeded values rather than just a written assumption).
- `PATCH /alert-rules/:id` and `POST /alert-rules` validate that `config` matches the rule's `type` (`resources` for `resource_change`, `habitualHours` for `off_hours_access`) at the service layer — class-validator alone can't express that cross-field constraint cleanly against a sibling `type` property.
- **User management (sub-phase 1.4)**: the Administrador supplies a new user's initial password directly in `POST /users` (same complexity policy as self-service change-password) — the created user always gets `mustChangePassword: true` regardless of role, same bootstrap-security posture as the seeded Administrator. `PATCH /users/:id` accepts a `password` field for an admin-triggered reset: it forces `mustChangePassword: true` and bumps `tokenVersion` (invalidating the target's outstanding refresh tokens), reusing the exact revocation mechanism `AuthService.changePassword` already established. Setting `active: false` also bumps `tokenVersion` — the API is intentionally stateless (`JwtAuthGuard` never does a per-request DB lookup), so a deactivated account's only residual access is its current short-lived access token, not a design gap.
- **Self-lockout guard**: an Administrador cannot deactivate or demote their own account via `PATCH /users/:id` (403) — a real operational-lockout risk, not speculative validation. They can still edit their own email or reset their own password through the same endpoint.
- User API responses are mapped through `toUserResponse()` (`user-response.mapper.ts`) to guarantee `passwordHash`/`tokenVersion` never leave the process, rather than relying on callers to remember to strip them.
- **Reports (sub-phase 1.6)**: `GET /reports/export` is the only endpoint that isn't the standard `{error:{code,message,requestId}}` JSON API — a success response is a raw file (`@Res()` without passthrough, headers set manually); error paths (401/400/403) still go through `AllExceptionsFilter` normally. Mixed RBAC on one route: the guard admits all three roles, but `ReportsService.generate()` throws a `403` if a Usuario requests `reportType=alerts` — a declarative `@Roles()` can't express "conditional on a query value." No pagination (a report is the full filtered set). CSV is hand-escaped (`csv.util.ts`); PDF uses `pdfkit` (new prod dependency, `npm audit --omit=dev` clean).
