# ADR-0002: User authentication and session revocation model

- **Status**: Accepted (sub-fase 1.1)
- **Related**: agent.md §5.4, §6.3, Assumption #4; CA-06, CA-08

## Context

agent.md mandates short-lived JWT access tokens plus refresh tokens, with logout invalidating "vía rotación/lista de revocación" — but spec.md's data model defines no collection for storing refresh-token or revocation state. A concrete mechanism was needed.

## Decision

- **Access token** (default 15 min, env-configurable `JWT_ACCESS_TTL`): payload `{sub, username, role, mustChangePassword}` — enough for the frontend to render role-gated UI and force a password-change redirect **without** a `/auth/me` endpoint (which isn't in the authoritative contract).
- **Refresh token** (default 7 days, env-configurable `JWT_REFRESH_TTL`): payload `{sub, tokenVersion}`, signed with a **separate secret** from the access token.
- **Revocation via `users.tokenVersion`** (an additive integer field, not in spec.md's example `users` document): incremented on logout, on self-service change-password, on an admin-triggered password reset, and on admin deactivation. `POST /auth/refresh` validates the token's `tokenVersion` against the current DB value and rejects (401) on mismatch — this invalidates *every* outstanding refresh token for that user in one write, with no per-token blacklist collection needed.
- Refresh **rotates** both tokens on every call (new access + new refresh), rather than only reissuing the access token.
- Login is timing-safe against username enumeration: when the username doesn't exist, the code still runs an argon2 verify against a dummy hash before rejecting, so the response time doesn't leak whether the account exists.

## Consequences

- No new collection was added — `users.tokenVersion`/`users.mustChangePassword` are the only additive fields, keeping spec.md's fixed collection list intact (only new *fields*, which agent.md's "no renombres... sin ADR" rule permits more readily than a new collection).
- A deactivated or password-reset user's **current** access token remains valid until it naturally expires (≤15 min) — the API is intentionally stateless (agent.md §4) and `JwtAuthGuard` does not do a per-request DB lookup. This is an accepted, bounded residual-access window, not an oversight; tightening it would mean giving up statelessness for every authenticated request just to shorten a already-short worst case.
