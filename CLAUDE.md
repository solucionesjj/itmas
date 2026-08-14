# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository State

Implementation has started (sub-phases 1.1–1.4 — see below). Governance documents:

- `spec.md` — the functional/technical specification for IT-MAS (Management and Audit System).
- `agent.md` — the mandatory operating guide for any AI/developer implementing this project. **This is the authoritative rulebook and takes precedence over general conventions.** Read it in full before writing any code; it governs architecture, security, testing, DevOps, and decision-making for this project, including the sub-phase breakdown of Fase 1 in §17.

Precedence when documents conflict: `spec.md` > `agent.md` > this file. Any ambiguity not resolved by those documents should be resolved using the **Decision Framework** in `agent.md` §13 (security/RBAC first, then spec compliance, then phase scope, then API/data contract stability, then established convention, then simplest reversible choice — documented as an Assumption).

### Node version

The system default `node` may be too old (NestJS/Angular need **Node ≥ 20**). A `.nvmrc` pinning `20` exists at the repo root — run `nvm use` in both `backend/` and `frontend/` before installing/running anything. If nvm isn't loaded in a non-interactive shell, prefix commands with `export PATH="$HOME/.nvm/versions/node/v20.19.5/bin:$PATH"` (adjust the exact version to whatever `nvm ls` shows installed).

### Backend (`backend/` — NestJS + TypeScript + MongoDB)

```
cd backend
npm install
cp .env.example .env        # then fill in real secrets/URIs — never commit .env
npm run start:dev           # dev server, watch mode, http://localhost:3000/api/v1
npm run build                # nest build
npm run lint                 # eslint --fix
npm test                     # unit tests (Jest) — single file: npx jest path/to/file.spec.ts
npm run test:cov             # unit tests with coverage
npm run test:e2e             # integration tests against mongodb-memory-server (real HTTP layer)
npm run device:provision -- --hostname <name> --category <collaborator|infrastructure>  # issues a node API key (printed once)
npm run device:rotate-key -- --device-id <id>   # invalidates a device's old key, issues a new one
```

Requires a reachable MongoDB at `MONGO_URI` to actually run (not needed for unit tests; `test:e2e` spins up its own in-memory instance via `mongodb-memory-server`). `GET /api/v1/health` checks process + Mongo connectivity.

### Frontend (`frontend/` — Angular, standalone components)

```
cd frontend
npm install
npm start                    # ng serve, http://localhost:4300 (or Angular's default 4200)
npm run build                 # ng build
npm run lint                  # ng lint
npm test                      # ng test (Karma/Jasmine — needs a local Chrome/Chromium)
```

`src/environments/environment.development.ts` points `apiBaseUrl` at `http://localhost:3000/api/v1` — keep the backend on port 3000 in normal dev, or override this file locally (don't commit a changed port).

## Project Summary

IT-MAS is a centralized platform for **inventory, monitoring, and audit** of IT infrastructure. Distributed agents (collaborator workstations on Windows/Linux/Mac, and servers on Windows/Linux) push hardware/software inventories and access events to a public REST API, which persists them in MongoDB. An Angular portal exposes statistics and alerts, gated by RBAC across three fixed roles: **Administrador**, **Usuario**, **Auditor**.

Fixed stack: **Node.js** (API) + **Angular** (frontend) + **MongoDB** (persistence) + **Docker**, deployed behind a TLS-terminating reverse proxy/API gateway. Do not substitute this stack.

**Current phase: Fase 1 (MVP).** Do not implement functionality scoped to later phases (MFA, SSO/OIDC/LDAP, self-service registration, custom roles, automated remediation, ITSM/SIEM integration, real-time APM, license management, notifications) unless explicitly instructed. Collection agents themselves (the per-OS clients that report data) are **out of scope for development** — assume they already exist and call the API.

Fase 1 is further broken into implementation sub-phases **1.0–1.7** in `agent.md` §17 (foundations → auth/RBAC → ingestion → alert engine → user management → dashboard/stats → reports/export → hardening/quality gates), each mapped to the CA-xx it satisfies and its dependencies on prior sub-phases. Follow that order — most endpoints assume auth/RBAC (1.1) already exists.

## Architecture

Three-layer architecture with distributed ingestion:

```
[Agents/Nodes] --HTTPS--> [Node.js REST API] --> [MongoDB]
                                   |
                            [Alert/Rules Engine]
                                   |
[Angular Portal] <--REST/JWT-- [API Backend]
         ^
         |__ Auth & RBAC (Administrador / Usuario / Auditor)
```

Key architectural principles (agent.md §4):
- **Backend organized by domain/feature**, not by file type: `src/modules/<domain>/{controller,service,repository,dto,schema}`. Controllers only orchestrate; services hold logic; repositories isolate data access.
- **Stateless API** — no in-memory session state; JWT + persistent storage only, to support horizontal scaling.
- **Contract-first**: `/api/v1/...` endpoints and JSON schemas are the authoritative interface. Breaking changes require a new version (`/v2`), never silent changes to `/v1`.
- **Dual auth mechanisms, never mixed on one endpoint**: nodes authenticate via a rotable per-node API key; the portal authenticates via user JWT.
- **Alert rules are configuration-driven** (`alert_rules` collection), never hardcoded thresholds/hours.
- Defense in depth: authorize at every layer; the frontend hiding a menu item is UX only, never a substitute for backend RBAC.
- Every request carries a `request-id` for correlation/idempotency; ingestion must tolerate node retries.

### Data model (MongoDB collections — exact names, do not rename without an ADR)

`devices`, `inventories` (append-only, never overwritten), `access_events`, `alerts`, `users`, `alert_rules`, `audit_log`. Field shapes and required indexes are defined in `spec.md` §11 and `agent.md` §5.3 — follow them exactly (e.g. `inventories: { deviceId: 1, timestamp: -1 }`, `users.username`/`users.email` unique, etc.).

- `deviceId` is assumed to equal the device's `_id` (host-uuid); inventories compare against the most recent prior inventory for that device.
- Timestamps are always UTC `ISODate`. "Habitual hours" logic must handle timezone explicitly and is configurable per install (default UTC until configured).
- `passwordHash` must never be exposed in API responses or logs.

### API contract (authoritative — see agent.md §5.4 / spec.md §12)

Prefix `/api/v1`. Do not invent endpoints, fields, or roles beyond this contract (or beyond a documented, explicitly-approved extension ADR — see ADR-0016 below for the one exception so far):
- Ingestion: `POST /inventory`, `POST /access-events`
- Auth: `POST /auth/login`, `POST /auth/logout`, `POST /auth/refresh`, `POST /auth/change-password`
- Users (Administrador only): `GET /users`, `POST /users`, `PATCH /users/:id`
- Alert rules (Administrador only): `GET /alert-rules`, `POST /alert-rules`, `PATCH /alert-rules/:id`
- Devices — mutation, Administrador only (ADR-0016, post-Fase-1 addition — not in spec.md's original literal contract): `POST /devices`, `POST /devices/:id/rotate-key`
- Query: `GET /devices`, `GET /stats/os`, `GET /stats/devices`, `GET /reports/export`, `GET /alerts`, `PATCH /alerts/:id` (Administrador/Auditor)
- Health: `GET /health`

Every endpoint must declare and enforce its required role — an endpoint with no declared role requirement is treated as an implementation bug. `GET /devices` and `GET /alerts` must support pagination and server-validated filters.

### RBAC model (three fixed roles, least privilege)

- **Administrador**: user management, alert rule configuration, full platform management.
- **Usuario**: read-only access to reports/statistics. No admin functions.
- **Auditor**: reads audit events, manages alert status/lifecycle. No user management, no alert-rule configuration.

RBAC must be enforced in the backend as the source of truth; frontend route guards (`AdministradorGuard`, `AuditorGuard`, `UsuarioGuard`, plus a global `AuthGuard`) only improve UX by hiding menu items — never rely on them for authorization. No self-service registration; only Administrador creates/edits users. No MFA/SSO in Fase 1 (but don't block future addition).

### Sub-phase 1.1 implementation notes (auth/RBAC — done)

`backend/src/modules/{users,auth,audit-log}` and `frontend/src/app/{core,features/login,features/change-password}` implement sub-phase 1.1. Two things extend beyond spec.md's literal `users` example on purpose — know these before touching auth code:
- `users.mustChangePassword` (bool) — forces the seeded/admin-created account through `/change-password` before anything else; the frontend `authGuard` redirects there when the decoded JWT carries this claim.
- `users.tokenVersion` (number) — the refresh-token revocation mechanism (spec.md defines no token-storage collection). Refresh tokens embed `{sub, tokenVersion}`; `/auth/logout` and `/auth/change-password` increment it server-side, which invalidates every previously issued refresh token for that user. `/auth/refresh` rotates both tokens on each call.

Access tokens carry `{sub, username, role, mustChangePassword}` — there is intentionally no `/auth/me` endpoint (not in the authoritative contract); decode the JWT client-side instead.

### Sub-phase 1.2 implementation notes (ingestion — done)

`backend/src/modules/{devices,inventories,access-events,ingestion}` implement `POST /api/v1/inventory` and `POST /api/v1/access-events`. No frontend work in this sub-phase (ingestion is agent-to-API, not portal-facing).

- **Node auth is a separate mechanism from user JWT** (agent.md §5.4 dual-auth rule, never mixed on one endpoint): nodes send an `X-Node-Api-Key: <deviceId>.<secret>` header, checked by `NodeApiKeyGuard` against `devices.apiKeyHash` (argon2, additive field beyond spec.md's `devices` example — same pattern as `users.passwordHash`). The `deviceId` prefix gives an O(1) lookup instead of scanning every device's hash.
- **No REST endpoint provisions/rotates a node's key** — none is in the authoritative contract, so `backend/scripts/provision-device.ts` / `rotate-device-key.ts` (run via `npm run device:provision -- --hostname <name> --category <collaborator|infrastructure>` / `npm run device:rotate-key -- --device-id <id>`) do it out-of-band, same bootstrap philosophy as the 1.1 Administrador seed. **A device must be provisioned before it can send data** — `POST /inventory` updates an existing device's `hostname`/`category`/`os`/`lastSeen`, it never creates the device row.
- **Idempotency** (agent.md §4 — ingestion must tolerate node retries) is natural-key based: a unique Mongo index on `(deviceId, timestamp)` for `inventories` and `(deviceId, level, user, timestamp, action)` for `access_events`. A resend with the identical `timestamp` hits a duplicate-key error that the repository treats as a successful no-op (`201`, no second document) rather than an error — which is why `timestamp` in both ingestion DTOs is the **node's own** collection/event time, required in the payload, never server-stamped.
- **RF-03's diff logic (`InventoryDiffService`) logs every detected CPU/RAM/disk change**; as of sub-phase 1.3, `IngestionService` also hands the result to `AlertEngineService`, which decides whether to actually raise an alert (see below) — the diff service itself still knows nothing about alerting.

### Sub-phase 1.3 implementation notes (alert engine — done)

`backend/src/modules/{alert-rules,alerts,alert-engine}` implement `GET/POST/PATCH /api/v1/alert-rules` (Administrador only), `GET/PATCH /api/v1/alerts` (Administrador + Auditor), and the engine that connects ingestion to both. **This is the first sub-phase where `RolesGuard`/`@Roles()` (built in 1.1) actually guards a real endpoint** — `@UseGuards(JwtAuthGuard, RolesGuard)` + `@Roles(...)` is now the pattern to copy for 1.4's user-management endpoints.

- **One rule per `type`, enforced by a unique Mongo index** on `alert_rules.type` — the engine always reads "the enabled `resource_change` rule" / "the enabled `off_hours_access` rule" as a singular concept. `POST /alert-rules` for a `type` that already has a rule returns `409`; use `PATCH /alert-rules/:id` instead.
- **Two default rules are seeded on first boot** (`AlertRulesService.onModuleInit()`, same bootstrap pattern as the 1.1 Administrador seed): `resource_change` enabled with `resources: [cpu, ram, disks]`; `off_hours_access` enabled with `habitualHours: {from: "07:00", to: "19:00"}`. These are agent.md Assumption #7's defaults — now actually seeded, not just written down. The engine always reads from the DB; it never hardcodes these.
- **`AlertEngineService.evaluateResourceChange`** fires only for resources the enabled `resource_change` rule lists in `config.resources`, and only on a genuinely new (non-duplicate-retry) inventory with a detected diff. **`evaluateAccessEvent`** fires only for `action: 'login'` on an `infrastructure`-category device (never `collaborator`, never `logout`), outside the enabled `off_hours_access` rule's `habitualHours` window — timezone-aware via `HABITUAL_HOURS_TZ` (env, default `UTC`) and `habitual-hours.util.ts`'s `Intl.DateTimeFormat`-based conversion, which also handles an overnight-wrapping range (e.g. `22:00`–`06:00`).
- `NodeApiKeyGuard` (from 1.2) now also resolves the device's `category` onto the request (`@DeviceCategoryParam()`) so `IngestionService` can pass it to the alert engine without a second DB lookup.
- `POST /alert-rules` and `PATCH /alert-rules/:id` validate that `config` matches the rule's `type` (`resources` for `resource_change`, `habitualHours` for `off_hours_access`) in `AlertRulesService`, not via class-validator alone — that cross-field constraint (checking against a sibling `type` property) isn't clean to express in decorators.
- `AuditLogAction` gained `update_alert_rule` and `update_alert_status`, recorded on every alert-rule mutation and every alert status change (CA-11/CA-12).
- **Watch out for `JsonLoggerService` meta collisions**: its log entries already have their own `timestamp` field (the log record's time) — never pass a meta field literally named `timestamp`, it silently overwrites the entry's own one. Use a different key (e.g. `eventTimestamp`) for domain timestamps you want to log.

### Sub-phase 1.4 implementation notes (user management — done)

`backend/src/modules/users` gained a `UsersController` (`GET/POST/PATCH /api/v1/users`, Administrador only via the same `@UseGuards(JwtAuthGuard, RolesGuard)` + `@Roles(...)` pattern 1.3 established). `frontend/src/app/features/admin/users` implements the management UI, guarded by a new `frontend/src/app/core/guards/role.guard.ts`.

- **`POST /users`**: the Administrador supplies the new user's initial password directly (same complexity policy as self-service change-password, reused from `auth/dto/change-password.dto.ts` — not redefined). The created user always gets `mustChangePassword: true` regardless of role, `createdBy` set to the admin's own id. Duplicate `username`/`email` → `409`, not a raw 500.
- **`PATCH /users/:id`**: partial `email`/`role`/`active`/`password`. A `password` reset forces `mustChangePassword: true` and bumps `tokenVersion` (invalidating the target's outstanding refresh tokens — the exact mechanism `AuthService.changePassword` already uses, just admin-triggered). Setting `active: false` also bumps `tokenVersion`, bounding a deactivated account's residual access to its current short-lived access token — intentional given this API's stateless design (`JwtAuthGuard` doesn't do a per-request DB lookup), not a gap to "fix" later.
- **Self-lockout guard**: an Administrador gets `403` attempting to deactivate or demote their own account via this endpoint — a real operational-lockout risk, not speculative validation. They can still edit their own email or reset their own password through it.
- User API responses are always mapped through `user-response.mapper.ts`'s `toUserResponse()` — `passwordHash`/`tokenVersion` never leave the process; don't bypass this mapper when adding new user-returning code paths.
- **Frontend**: `createRoleGuard(...roles)` (`core/guards/role.guard.ts`) is a factory, not one guard class per role — agent.md §5.2 names `AdministradorGuard`/`AuditorGuard`/`UsuarioGuard`, but only `administradorGuard` is actually instantiated so far (instantiate `auditorGuard`/`usuarioGuard` from the same factory when 1.5/1.6 need them — don't hand-write near-duplicate guard classes). A new `core/layout/shell.component.ts` wraps the authenticated routes with a nav that shows "Usuarios" only for Administrador (CA-09) — hiding the link is UX only, `administradorGuard` on the `/admin/users` route itself is the real enforcement.

### Sub-phase 1.5 implementation notes (dashboard, devices, alerts UI — done)

Scope was widened mid-flight: the original plan had no sub-phase for the alerts *UI* even though its backend (`alert-rules`/`alerts`) shipped in 1.3 — folded into 1.5 since dashboard/equipos/alertas are all portal-consultation screens for the same roles (see agent.md §17's note on this).

- **Backend**: `devices/devices.controller.ts` is that module's **first HTTP controller** (previously internal-only, used by ingestion/node-auth) — `GET /api/v1/devices`, paginated (`{items,total,page,limit}`, same shape as `alerts`' `QueryAlertsDto`), filters `category` (exact enum), `osName`/`hostname` (case-insensitive partial, regex-escaped via `common/util/escape-regex.util.ts` before hitting Mongo — never interpolate raw client input into a `$regex`). A new `stats` module (`GET /api/v1/stats/devices` → `{total,collaborator,infrastructure}`, `GET /api/v1/stats/os` → `[{os,count}]` via `$group`, unassigned devices bucketed as `"unknown"` rather than dropped) reuses `DevicesRepository` rather than a second query path onto the same collection.
- **RBAC contrast**: unlike `/users`/`/alert-rules` (Administrador-only) or `/alerts` (Administrador+Auditor), these three query endpoints declare `@Roles(ADMINISTRATOR, USER, AUDITOR)` — all three roles, but still explicit (never omit `RolesGuard` just because everyone's allowed).
- **Frontend**: `features/dashboard/` (KPI stat-tile row + `os-distribution-chart.component` — a horizontal bar chart built dependency-free in SVG/HTML+CSS per the dataviz skill's palette/mark-spec guidance, not a charting library, to keep the bundle small) replaces the old placeholder home. `features/devices/devices-list.component` and `features/alerts/alerts-list.component` are new Material-table pages with pagination/filters; alerts additionally supports the `PATCH` status action. Nav (`shell.component`) now shows "Equipos"/"Dashboard" to all authenticated roles and "Alertas" only via a new `adminOrAuditorGuard` (Usuario is 403'd server-side on `GET /alerts` already, from 1.3 — the nav/route guard just avoids sending them to a dead end).

### Sub-phase 1.6 implementation notes (reports export — done)

Closes out the portal/query feature set of Fase 1 — only 1.7 (hardening/quality gates) remains after this.

- **Backend**: `modules/reports/` — `GET /api/v1/reports/export?reportType=devices|alerts&format=csv|pdf` plus optional filters (`category`/`osName`/`hostname` for devices; `alertType`/`status`/`from`/`to` for alerts — the report's own selector is named `reportType`, not `type`, precisely to avoid colliding with the alert domain's own `type` field in the same query string). **Mixed RBAC on one endpoint**: the controller-level `@Roles()` admits all three roles (Usuario can export the devices report, RF-14/CA-13), but `ReportsService.generate()` throws `403` if a Usuario requests `reportType=alerts` — a declarative guard can't express "conditional on a query value," so that check lives in the service, the same pattern `UsersService`'s self-lockout guard already established in 1.4. No pagination — a report is the full filtered set (device/alert counts are in the hundreds per the NFRs; fine for an on-demand authenticated download, not an unbounded public resource). CSV is hand-built with correct escaping (`modules/reports/csv.util.ts`); PDF uses **`pdfkit`** (new production dependency — the only way to emit real PDF bytes without a library, and lighter than any headless-browser alternative; `npm audit --omit=dev` still reports 0 vulnerabilities). This is the **only endpoint in the whole API that isn't the JSON envelope** — a raw file download via `@Res()` with `Content-Type`/`Content-Disposition` headers; `AllExceptionsFilter` still handles the 401/400/403 paths normally, only the 200 success path is a raw buffer.

### Sub-phase 1.7 implementation notes (hardening + Quality Gates — done, Fase 1 functionally complete)

- **Rate limiting**: `ThrottlerModule` from `@nestjs/throttler` is internally `@Global()` — only **one** `forRootAsync()` registration exists in the whole app (`app.module.ts`), with two named profiles: `default` (generous API-wide, `API_RATE_LIMIT_MAX`/`WINDOW_SEC`, default 100/60s) applied to every route via `APP_GUARD`, and `login` (the strict brute-force limit, `LOGIN_RATE_LIMIT_MAX`/`WINDOW_SEC`) which the global guard *also* applies to every route by default — so every controller except `POST /auth/login` carries `@SkipThrottle({ login: true })` to opt back out of it. A second, module-local `ThrottlerModule.forRootAsync()` (as this used to be, split into `AuthModule`) silently collides with the global one on the same DI tokens — don't reintroduce that.
- **Retention**: TTL indexes on `inventories`/`access_events`/`audit_log` timestamps, created programmatically in each repository's `onModuleInit()` (env-configurable via `*_RETENTION_DAYS`, since a static schema-level TTL can't read `ConfigService`). `src/common/mongo/ensure-ttl-index.util.ts` recreates the index if the configured value changed (Mongo disallows an in-place TTL change) and tolerates a brand-new collection ("ns does not exist").
- **Body size**: explicit 1MB cap in `main.ts` (`bodyParser: false` + manual `json({limit})`) — replaces Nest's implicit, undocumented 100kb default. `AllExceptionsFilter` now also maps raw (non-`HttpException`) errors carrying their own `.status`/`.statusCode` — e.g. body-parser's `PayloadTooLargeError` — to the real HTTP status instead of a generic 500.
- **OpenAPI/Swagger**: uses the `@nestjs/swagger` **CLI plugin** (`nest-cli.json` → `compilerOptions.plugins`), not manual `@ApiProperty()` on every DTO — it infers types/enums/required/nested schemas straight from existing `class-validator` decorators. `GET /api/docs` (Swagger UI) and `GET /api/docs-json`; `backend/openapi.json` regenerates on every boot (best-effort — won't crash boot on a read-only filesystem). `@nestjs/swagger` is pinned to `11.4.5`, not the latest `11.4.6`, because `11.4.6` pulls a vulnerable `js-yaml`.
- **Load benchmark** (soft, not a CI gate): `backend/scripts/load-smoke.ts` (`npm run load:smoke`) fired 50 concurrent `POST /inventory` at a real running instance — measured p95 ≈ 1096ms against the NFR target of <500ms, in this single-instance sandboxed dev environment with no connection-pool tuning. Documented honestly rather than hidden; not indicative of a properly scaled/tuned production deployment.
- **Docker/CI** (built this sub-phase): `backend/Dockerfile` + `frontend/Dockerfile` (multi-stage), root `docker-compose.yml`, `.github/workflows/ci.yml` (lint→build→test→audit), root `DEPLOYMENT.md`.
- **Accessibility (WCAG AA)**: closed the sub-phase-1.5 debt — the hand-built OS-distribution chart now has an accessible fallback (`aria-label`/visually-hidden data), icon-only actions across the users/alerts/reports UIs got `aria-label`s, and `angular-eslint` template a11y rules are on as a regression gate.
- **Encryption at rest**: an infrastructure decision (MongoDB cluster/storage-level encryption), not application code — documented as an operational requirement in `DEPLOYMENT.md`.
- **Fase 1 (1.0–1.7) is functionally complete.** ADRs for major architectural decisions live in `docs/adr/`; CA-01..14 → test mapping lives in `docs/ca-traceability.md`.

### Portal device provisioning implementation notes (post-Fase-1 addition — done, see ADR-0016)

Not one of the numbered 1.0–1.7 sub-phases (those are fixed by agent.md §17 and Fase 1 is already functionally complete) — this is a deliberate, explicitly-requested scope decision reversing part of ADR-0003 ("no REST endpoint provisions/rotates a node's key" → now the portal can, alongside the still-valid CLI scripts). Same "extension documented via its own ADR rather than silently folded into Fase 1" treatment as EXT-1 elsewhere in this project's history.

- **Backend**: `devices.controller.ts` gains `POST /devices` (`201`) and `POST /devices/:id/rotate-key` (`200`, `404` if missing), both `@Roles(ADMINISTRATOR)` at the method level — overriding, for just these two routes, the class-level `@Roles(ADMINISTRATOR, USER, AUDITOR)` that still governs `GET /devices`. Both call `DevicesService.provision()`/`rotateKey()` verbatim, the exact methods `backend/scripts/provision-device.ts`/`rotate-device-key.ts` already used — no key-generation/hashing logic was duplicated. The only service change was `rotateKey`'s not-found case moving from a plain `Error` to `NotFoundException`, so the REST layer gets a real `404` (harmless to the CLI script, which just logs and exits either way). Two new `AuditLogAction` values, `create_device`/`rotate_device_key`, follow the exact `AuditLogService.record(action, actorId, target, detail)` convention every other mutation endpoint uses; `detail` never contains the key or its secret half.
- **The one-time-reveal security property holds through the full HTTP path, not just the service layer**: `POST /devices`'s response is the only place in the entire API where `apiKey` plaintext appears, and it never appears again afterward (confirmed by hand against a real running instance — a subsequent `GET /devices` for the same device returns no `apiKey`/`apiKeyHash` field). There is no generic response-body-logging middleware in this app that could leak it into a log line either — `AllExceptionsFilter` only logs `message`/`path`/`method`/`status`, and `JsonLoggerService` is only ever called explicitly with hand-picked, non-secret fields.
- **Frontend**: `features/devices/` gains a "Crear dispositivo" button and a per-row "Rotar clave" icon button on the existing devices list, gated by the same inline `authService.currentUser()?.role === 'administrator'` check `shell.component` already uses for "Usuarios" — UX only, the backend's own `@Roles()` is the real enforcement (verified directly against the API with a Usuario token during manual testing: `403`). Three new dialog components: `device-form-dialog` (create), `rotate-key-confirm-dialog` (the "la clave anterior dejará de funcionar inmediatamente" confirmation), and `api-key-reveal-dialog` (shared by both the create and rotate flows — full key, `cdkCopyToClipboard`, "Guarda esta clave ahora — no se mostrará de nuevo" warning, `disableClose: true` so it can't be dismissed by an accidental backdrop click/Escape).
- **Testing gotcha worth knowing before touching `devices-list.component.ts` again**: `MatDialogModule`'s own `@NgModule` declares `providers: [MatDialog]` (redundant with `MatDialog`'s `providedIn: 'root'`, but real) — importing `MatDialogModule` into a standalone component's `imports` array to get the `MatDialog` *service* therefore creates a component-scoped instance that silently shadows any `TestBed`-level provider override, making the component untestable via a mocked `MatDialog`. Fix was to drop `MatDialogModule` from `DevicesListComponent`'s `imports` (its template doesn't use any `mat-dialog-*` directives — it only injects the service to call `.open()`), not to reach for `TestBed.overrideProvider()` gymnastics. `UsersListComponent` has this same latent, never-unit-tested issue; leave it alone unless a future change actually needs to unit-test it.

## Technical Standards (from agent.md §5, apply once code exists)

- **Backend**: Node.js LTS ≥ 20, TypeScript strict, ES modules. NestJS is the preferred framework (DI + native RBAC guards) — Express is acceptable only with an equivalent modular architecture, documented via ADR. Validate all input with schemas (Zod/class-validator/Joi); never pass raw input into Mongo queries; sanitize keys against `$`/`.` injection. Centralized error handling, consistent error shape `{ error: { code, message, requestId } }`, never leak stack traces. No implicit `any`, no `console.log` (use the structured logger).
- **Frontend**: Angular (latest stable) with TypeScript strict, Angular Material (or equivalent), WCAG AA accessibility. Feature-based lazy-loaded modules, dumb vs. container components. RxJS or signals for state; `takeUntilDestroyed`/async pipe to avoid subscription leaks. HTTP interceptors attach JWT and handle 401 (redirect to login) / 403 (access-denied message).
- **Database**: Mongoose recommended as ODM. Parameterized queries only — never interpolate strings into queries. TTL/purge-based retention policies for `inventories`, `access_events`, `audit_log`. Storage-level encryption at rest.
- **Auth**: short-lived JWT (~15 min) + refresh token (~7 days); logout invalidates via revocation list/rotation. Passwords hashed with Argon2 (bcrypt acceptable) — never MD5/SHA/plaintext.
- **Security**: TLS/HTTPS mandatory everywhere (API is public-facing). Rate limiting + brute-force protection on `/auth/login`. `npm audit` clean of high/critical vulnerabilities. No secrets committed or logged.

## Testing Strategy (agent.md §8)

- **Unit (Jest)**: inventory-diff logic, alert-rule evaluation, per-role permission checks, password hashing/validation.
- **Integration**: API endpoints against an ephemeral/in-memory MongoDB (`mongodb-memory-server`), including auth and RBAC paths.
- **Contract**: JSON schema validation for `/inventory`, `/access-events`, and query responses.
- **E2E (Cypress)**: per-role portal flows — login/redirect, Usuario report export, Auditor alert-status update without admin access, Administrador user creation + alert config, 403 enforcement for Usuario on admin routes.
- **Security tests**: 401 without token, 403 by role, rate limiting, NoSQL injection resistance.
- **Load**: simulate hundreds of nodes; ingestion must stay under 500ms per inventory.
- **Coverage target: ≥ 80%** on critical business logic — PRs that drop below this are rejected.

## Quality Gates (agent.md §11 — a change must pass all of these)

Lint clean (backend + frontend) · successful build · unit/integration/contract tests green · ≥80% coverage on critical logic · RBAC verified via tests demonstrating correct 401/403 per sensitive endpoint · `npm audit` clean of high/critical + secret scan clean · no hardcoded secrets or sensitive data in logs · OpenAPI/Swagger contracts updated when endpoints change · an ADR recorded for any architectural or contract/model decision · relevant CA-xx acceptance criteria verified.

## Working Conventions

- Feature branches (`feature/`, `fix/`, `chore/`); no direct commits to `main`; PRs required.
- Conventional Commits (`feat:`, `fix:`, `test:`, `refactor:`, `docs:`, `chore:`).
- Every change should be traceable to a requirement ID from `spec.md` (RF-xx / UC-xx / CA-xx).
- Never return raw Mongo documents from the API — define explicit DTOs/schemas for input and output.
- Record architectural decisions as ADRs; record assumptions made due to missing information in `agent.md` §15 (Assumptions), not scattered inline.
- Stay within Fase 1 scope; anything from later roadmap phases (see `spec.md` §20) should be flagged rather than silently implemented.
