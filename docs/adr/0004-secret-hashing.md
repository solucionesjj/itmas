# ADR-0004: Secret hashing algorithm

- **Status**: Accepted (sub-fase 1.1, extended in 1.2)
- **Related**: agent.md §6.4, Assumption #5

## Context

agent.md requires strong password hashing (bcrypt or argon2, never MD5/SHA/plaintext) and a password complexity policy.

## Decision

- **argon2** (the `argon2` npm package, argon2id variant by default) for every secret hash in the system: `users.passwordHash` and `devices.apiKeyHash` alike — one algorithm, one code path, rather than mixing bcrypt for one and argon2 for the other.
- **Complexity policy**: minimum 8 characters, at least one uppercase, one lowercase, one digit — enforced server-side via a shared regex (`PASSWORD_POLICY_REGEX`/`PASSWORD_POLICY_MESSAGE`, defined once in the auth module's `change-password.dto.ts` and imported everywhere else a password is accepted: user creation, admin password reset) and mirrored client-side in the frontend's reactive forms for immediate UX feedback (the server is still the authority — client-side validation is convenience, not enforcement).

## Consequences

- A single shared regex constant means the policy can't drift between "change my own password" and "admin resets someone's password" — both DTOs import the same source.
- Node API keys are high-entropy random secrets (32 random bytes, base64url), not human-chosen passwords, so the complexity policy doesn't apply to them — only the hashing algorithm choice is shared.
