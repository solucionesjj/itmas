# ADR-0015: Scheduling mechanism — `@nestjs/schedule` with imperative `SchedulerRegistry`

- **Status**: Proposed — implemented in this change set.
- **Related**: ADR-0013 (scope), ADR-0006 (the `onModuleInit()`-reads-config idiom this mirrors).

## Context

The daily AWS security-group sync needs to run automatically at a configurable time, in addition to its manual REST trigger. No cron/scheduling mechanism of any kind exists anywhere in this codebase today (confirmed: no `@nestjs/schedule`, `node-cron`, `@Interval`/`@Timeout` usage anywhere in `backend/src` or `package.json`).

## Decision

Add `@nestjs/schedule` as a production dependency, registered once via `ScheduleModule.forRoot()` in `AppModule` (same "single global registration" discipline already established for `ThrottlerModule` — see the comment in `app.module.ts` warning against a second, colliding registration).

**Not** the static `@Cron('0 2 * * *')` decorator: a decorator's cron expression is fixed at compile time and can't read `ConfigService`, the same reason TTL indexes are built imperatively in `onModuleInit()` rather than declared statically in a schema. Instead, `SecurityGroupSyncService.onModuleInit()` reads `AWS_SYNC_HOUR`/`AWS_SYNC_MINUTE` (default `02:00`, UTC) via `ConfigService`, constructs the cron job, and registers it with `SchedulerRegistry.addCronJob()`. This lets an operator change the sync time via env var across a redeploy without a code change, matching how every other configurable threshold in this system works (habitual hours, retention days, rate limits).

Time zone: UTC only, via dedicated `AWS_SYNC_HOUR`/`AWS_SYNC_MINUTE` env vars — deliberately **not** reusing `HABITUAL_HOURS_TZ`, which governs an unrelated concern (the `off_hours_access` alert rule's habitual-hours window) and would conflate two independent configuration axes if shared.

## Consequences

- Adds one production dependency. Peer-dependency-compatible with the existing `@nestjs/common`/`@nestjs/core` v11 (`@nestjs/schedule` supports `^10.0.0 || ^11.0.0`).
- The automated run and the manual `POST /security-group-sync/run` endpoint call the exact same `SecurityGroupSyncService.runSync(triggerType, triggeredBy)` — the cron handler is a thin wrapper (`runSync('automated', undefined)`), never a separate code path, so a bug fix or behavior change to the sync algorithm can't accidentally apply to only one of the two triggers.
- No distributed-lock mechanism is added (e.g. to prevent two app instances double-running the cron in a horizontally-scaled deployment) — out of scope at this stage; flagged here for whoever scales this deployment to multiple backend instances, mirroring how the seed-on-boot idempotent-upsert pattern already tolerates a duplicate run safely (the sync's own upsert-by-natural-key logic means a double-run is wasteful, not incorrect).
