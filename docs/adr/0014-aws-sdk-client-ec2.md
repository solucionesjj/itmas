# ADR-0014: AWS SDK client selection — `@aws-sdk/client-ec2`

- **Status**: Proposed — implemented in this change set.
- **Related**: ADR-0013 (scope); agent.md §5.1 ("minimiza dependencias; audita con `npm audit`").

## Context

The new `security-group-sync` module needs to call AWS APIs to discover regions, list security groups and their rules, and resolve which resources (EC2 instances, ENIs, RDS, load balancers, Lambda) a security group is attached to. No AWS SDK dependency exists anywhere in this codebase today (confirmed: the only `@aws-sdk/*` reference in `package-lock.json` is `@aws-sdk/credential-providers`, an unused optional peer dependency of `mongoose`/MongoDB's Atlas IAM-auth path, unrelated to calling EC2 APIs).

## Decision

Add `@aws-sdk/client-ec2` (AWS SDK for JavaScript v3, modular per-service client) as a production dependency. All the operations this module needs — `DescribeRegions`, `DescribeSecurityGroups`, `DescribeSecurityGroupRules`, `DescribeNetworkInterfaces`, `DescribeInstances` — live in this single EC2 client; `GetCallerIdentity` (STS) is added via `@aws-sdk/client-sts` only if the SDK v3's default credential-resolution doesn't already surface the account ID more cheaply (implementation detail, not an architectural fork).

Credentials use the SDK's standard default provider chain (environment variables or an IAM role/instance profile) — never stored in IT-MAS's own database or configuration files, consistent with agent.md §6.11 ("secretos solo en variables de entorno / gestor de secretos"). The minimal read-only IAM policy this integration needs is documented in `.env.example`/`DEPLOYMENT.md`: `ec2:DescribeRegions`, `ec2:DescribeSecurityGroups`, `ec2:DescribeSecurityGroupRules`, `ec2:DescribeNetworkInterfaces`, `ec2:DescribeInstances`, `sts:GetCallerIdentity` — no write/mutate permission on any AWS resource.

`EC2Client` instances are obtained through an injectable `AwsEc2ClientFactory` provider, never instantiated inline — this is the seam `overrideProvider(AwsEc2ClientFactory)` uses in tests (via `aws-sdk-client-mock`, dev-only) so CI never makes a real AWS call.

## Consequences

- Adds one production dependency (and its transitive `@smithy/*` packages) to `backend/package.json`. `npm audit --omit=dev` confirmed clean of new vulnerabilities from this dependency at time of writing (the only pre-existing high-severity finding, `js-yaml` via `@nestjs/swagger`, predates this change and is tracked separately).
- Rate limits / API throttling from AWS (`DescribeSecurityGroupRules` etc. have per-account request-rate limits) are the SDK's own built-in retry/backoff responsibility — not reimplemented here.
- If a future extension needs multi-account or a different cloud provider, that's a separate ADR; this one commits only to single-account AWS EC2.
