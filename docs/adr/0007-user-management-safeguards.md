# ADR-0007: Admin user-management safeguards

- **Status**: Accepted (sub-fase 1.4)
- **Related**: agent.md §9 (seed admin forced password change), RF-12, UC-09; CA-09, CA-10

## Context

`POST/PATCH /users` gives an Administrador broad control over accounts, including their own — which opens two real operational risks not explicitly called out by a CA but squarely within "denegar por defecto" territory: a weak/never-changed admin-set password, and an admin locking themselves out.

## Decision

- **Admin-supplied initial password, forced change**: `POST /users` takes the password directly from the admin (validated against the same complexity policy as self-service change, ADR-0004) rather than auto-generating one — but every created account gets `mustChangePassword: true` unconditionally, regardless of role, mirroring the seed-Administrator bootstrap policy from sub-phase 1.1. An admin-set password is never trusted as the account's long-term credential.
- **Password reset via `PATCH`** (the contract has no separate reset endpoint) sets `mustChangePassword: true` and bumps `tokenVersion` — identical effect to self-service change-password, just admin-triggered.
- **Deactivation (`active: false`) also bumps `tokenVersion`** — cuts off refresh immediately; the current short-lived access token is the only bounded residual (see ADR-0002's stateless-API tradeoff).
- **Self-lockout guard**: an Administrador cannot deactivate or demote (change `role` away from `administrator`) their **own** account via this endpoint — a `403` with a clear message. This is enforced server-side (the source of truth) and echoed client-side as a UX nicety (disabling the relevant controls in the admin's own row) so the click never reaches a confusing dead end.

## Consequences

- There is deliberately no way for the last Administrador account to lock itself out through this endpoint — but this guard only protects against *self*-lockout via this one endpoint; an admin deactivating *every other* admin account (if more than one exists) is still possible and not guarded against, since that's a multi-account scenario outside a single request's self-check.
