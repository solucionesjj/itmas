# ADR-0010: API-wide rate limiting, data retention, and OpenAPI generation

- **Status**: Accepted (sub-fase 1.7)
- **Related**: agent.md §6.8, §5.3, §9, §11 (Quality Gates)

## Context

Sub-fase 1.1 only ever protected `POST /auth/login` against brute force. agent.md also requires broader API-wide rate limiting, configurable retention (TTL) for the three time-series collections, and complete OpenAPI/Swagger documentation — none of which existed before this closing sub-phase.

## Decision

- **Rate limiting — one `ThrottlerModule` registration, two named profiles.** `@nestjs/throttler`'s module is internally `@Global()`; registering it a second time in a feature module (as the original login-only setup did, inside `AuthModule`) silently collides with a second registration elsewhere on the same DI tokens. The fix: a single `ThrottlerModule.forRootAsync()` in `AppModule` with two profiles — `default` (API-wide, env `API_RATE_LIMIT_MAX`/`API_RATE_LIMIT_WINDOW_SEC`, default 100/60s) and `login` (the original brute-force limit, env `LOGIN_RATE_LIMIT_MAX`/`_WINDOW_SEC`). `ThrottlerGuard` is applied globally via `APP_GUARD`; every controller other than `POST /auth/login` carries `@SkipThrottle({login: true})` so only the generous `default` profile applies to it.
- **Retention TTL indexes, created programmatically, not via a static schema annotation** — `inventories`/`access_events`/`audit_log` each get a TTL index on their timestamp field, with the retention period read from `ConfigService` at `onModuleInit()` (same bootstrap pattern as the Administrator/alert-rule seeds), because a static `@Prop({expires: N})` can't be parameterized by an env var at class-definition time. `ensureTtlIndex()` (`src/common/mongo/`) detects an existing index with a *different* `expireAfterSeconds` and drops+recreates it (Mongo refuses to change a TTL value in place), and treats a fresh, never-written-to collection's "ns does not exist" error as "no index yet" rather than crashing boot. Defaults: 180/180/365 days.
- **Body-size cap**: an explicit 1MB limit (Nest's own body parser disabled, a manual `json({limit})` substituted), replacing Express's implicit, undocumented 100kb default — an unbounded body on a public ingestion API is a real DoS surface.
- **OpenAPI via the `@nestjs/swagger` CLI plugin**, not manual `@ApiProperty()` annotation on every DTO — the plugin infers schema shape, enums, and required/optional fields directly from the `class-validator` decorators already on every DTO, for equivalent documentation with far less code. Served at `GET /api/docs` (UI) and `GET /api/docs-json`; `backend/openapi.json` is regenerated on every boot (best-effort — a read-only container filesystem shouldn't crash the app over a convenience artifact).

## Consequences

- **The NFR "ingesta < 500ms" is not currently met** in this sandboxed single-instance environment: a 50-concurrent-request load smoke test (`npm run load:smoke`) measured p95 ≈ 1096ms. This is reported honestly rather than hidden — it reflects an untuned single instance with no connection-pool sizing or horizontal scaling, not necessarily a production-representative result, but it is a real, open gap against the stated NFR and should be re-measured against production-shaped infrastructure (proper Mongo connection pool sizing, horizontal replicas per agent.md §9's "escalado horizontal para cientos de nodos") before considering that NFR closed.
- **Encryption at rest was deliberately not implemented in application code** — it's a MongoDB storage/cluster-level configuration (agent.md §6.9), documented as an operational requirement in `DEPLOYMENT.md` for whoever deploys this, not something a Mongoose schema or NestJS module can express.
