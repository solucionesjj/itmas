# ADR-0001: Backend framework, language, and ODM

- **Status**: Accepted (sub-fase 1.1)
- **Related**: agent.md §5.1, Assumptions #1–#3

## Context

agent.md leaves the backend framework as a choice between Express and NestJS ("preferido NestJS"), requires TypeScript strict, and recommends Mongoose as the MongoDB ODM. A concrete decision was needed before any code could be written.

## Decision

- **NestJS** (latest stable at the time, v11) over Express. NestJS's built-in dependency injection, module boundaries, and native `Guard`/`Interceptor`/`Pipe` primitives map directly onto agent.md's mandatory structure (`src/modules/<domain>/{controller,service,repository,dto,schema}`) and its RBAC requirements (guards are a first-class NestJS concept, not something to hand-roll on top of Express middleware).
- **TypeScript strict mode**, ES modules, Node.js ≥ 20 (LTS).
- **Mongoose** as the ODM, via `@nestjs/mongoose`, for schema validation, typed models, and index declaration colocated with the schema.
- **class-validator** + **class-transformer** for DTO validation (chosen over Zod/Joi — it's NestJS's idiomatic default and integrates with the global `ValidationPipe` with no extra glue).
- **argon2** for all secret hashing (see ADR-0004).

## Consequences

- Every module built since (auth, users, devices, ingestion, alert-rules, alerts, stats, reports) follows the same five-file shape, which made the fork-based delegation model used to build this project tractable — each sub-phase's directive could say "match the existing pattern" without re-explaining it.
- NestJS's `Reflector`-based `@Roles()`/`RolesGuard` combo (built in sub-phase 1.1, first exercised on real endpoints in sub-phase 1.3) satisfies agent.md's "every endpoint must declare its role or it's a bug" rule cleanly, since the guard itself throws when no `@Roles()` metadata is present.
