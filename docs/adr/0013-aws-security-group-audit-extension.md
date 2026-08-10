# ADR-0013: AWS Security Group rule audit — new IT-MAS extension

- **Status**: Proposed — implementation follows in this same change set (unlike ADR-0012, which documented a gap with no accompanying code, this ADR is being executed immediately after being written).
- **Related**: agent.md §13 (Decision Framework step 3: "¿Está fuera del alcance de Fase 1? → No lo implementes; regístralo para roadmap"), §15 (Assumptions), Quality Gate #9; docs/adr/0012-agent-backend-contract-extension-proposal.md (precedent for a "Proposed" ADR status); new RF-21..27, CA-15..20.

## Context

The user asked for a module to inventory, track, and audit AWS Security Group firewall rules across every region/VPC of a single AWS account, with a two-person review/authorization workflow (Auditor reviews, Administrador authorizes), daily automated + manual sync, an in-portal "new since last run" indicator, and filterable/sortable/exportable catalog UI.

`spec.md` and `agent.md` — the documents that govern this repository and take precedence over any convention — contain zero mentions of AWS, cloud, or firewalls anywhere, including in the Fase 2-4 roadmap (verified via exhaustive `grep` across both files and `docs/`). IT-MAS's entire data model assumes agents reporting inventory from physical/virtual hosts (`devices`/`inventories`/`access_events`), not a pull-based integration against a cloud provider's API. Per the Decision Framework, this would normally be rejected and logged for future roadmap discussion rather than implemented.

The user explicitly confirmed, after this gap was surfaced, that this should be built as **a new formal IT-MAS extension**: new RF-xx/CA-xx, this ADR, and otherwise following every existing convention (RBAC, DTOs, `/api/v1` prefix, MongoDB, audit logging, testing strategy) as if it were native Fase 1 work.

## Decision

Two new backend modules, two new MongoDB collections, one new Angular feature. Full design in the approved implementation plan (see PR description / commit history for this branch); summarized decisions with lasting architectural weight:

1. **New collections, not an extension of `devices`**: `security_group_rules` (the living catalog; no TTL — review/authorization state must persist indefinitely) and `aws_sync_runs` (per-run observability log; TTL retention via `AWS_SYNC_RUN_RETENTION_DAYS`, same `ensureTtlIndex()` idiom as `inventories`/`access_events`/`audit_log`). AWS Security Groups are not IT-MAS "devices" — conflating the two collections would force an artificial device row for every EC2 instance/ENI/RDS/ALB found, none of which are collection-agent-reporting hosts as `devices` assumes.

2. **"Dispositivo asociado" = the AWS resource(s) attached to the security group** (`attachedResources: {resourceType, resourceId, resourceName?}[]`), discovered via `DescribeNetworkInterfaces` (covers EC2/RDS/ALB/NLB/Lambda-in-VPC, all of which attach via an ENI) plus a batched `DescribeInstances` to resolve EC2 Name tags — **not** a cross-reference to IT-MAS's own `devices` collection, which has no guaranteed relationship to what AWS reports.

3. **Origin/destination directional mapping**: AWS's rule model is one-sided — an ingress rule only names its source, an egress rule only names its destination; the other side is implicitly "this security group's protected resources," which AWS never names as an endpoint. The local side reuses the group's own `securityGroupId` rather than inventing a placeholder: `ingress → source=remoteEndpoint, destination=securityGroupId`; `egress → source=securityGroupId, destination=remoteEndpoint`. This never fabricates data AWS didn't provide.

4. **`createdAt` fallback**: the EC2 API exposes no creation timestamp for a security-group rule. `createdAt` is set once, at first observation by IT-MAS's own sync, and never touched again — matching the user's own stated fallback ("si es posible obtenerla de AWS, de lo contrario la fecha actual").

5. **RBAC**: `GET /security-group-rules`, `GET /security-group-rules/groups`, and `GET /security-group-rules/export` are open to all three roles (Administrador, Auditor, **and Usuario** — confirmed with the user as read-only catalog access, matching Usuario's existing read-only posture on `/devices`/`/reports`). `PATCH /:id/review` is Auditor-only; `PATCH /:id/authorize` is Administrador-only — two separate, role-fixed endpoints rather than one generic status-patch endpoint, so `@Roles()` alone is the real two-person-integrity enforcement mechanism (this app's `UserRole` is a single value per user, so role disjointness makes cross-review-and-authorize by the same account structurally impossible; a defensive same-actor check in the service is added anyway to make the intent explicit in code). `security-group-sync`'s endpoints (manual trigger, run history, summary) are Administrador+Auditor only — Usuario has no operational need to trigger or inspect sync runs, only to see their result in the catalog.

6. **Export gets its own endpoint** (`GET /security-group-rules/export`), not a new `reportType` on the existing shared `/reports/export`: this feature's filter shape (three independent date ranges) and required sort (`securityGroupId` then `ruleId`) don't fit the existing `QueryReportsDto`/single-field-sort pattern without bloating it for every other report type. The existing `reports/csv.util.ts` and `reports/pdf.util.ts` pure functions are reused directly.

7. **Single AWS account, auto-discovered regions**: confirmed with the user — one account, all of its *enabled* regions via `DescribeRegionsCommand({AllRegions:false})` rather than a hardcoded region list that goes stale, with an optional `AWS_SYNC_REGIONS` env override to narrow scope deliberately. Multi-account (AWS Organizations) support is out of scope; `awsAccountId` is still stored per-record so a future multi-account extension doesn't require a schema migration.

8. **Synchronous, in-process execution**: no queue/worker. Acceptable at the scale of one AWS account's regions/security groups; revisit only if that scale assumption changes materially (mirrors ADR-0006's analogous synchronous-alert-evaluation tradeoff).

9. **No new notification infrastructure**: `GET /security-group-sync/summary` derives `pendienteSinceLastRun` from `status='pendiente' AND createdAt >= lastSuccessfulRun.startedAt` — no email/webhook, no persisted read/unread state, per the user's confirmed decision to keep this in-portal only.

## Consequences

- This module lives structurally alongside Fase 1's modules (same RBAC/DTO/audit-log/testing conventions) but is tracked separately as **Extensión EXT-1** rather than folded into the Fase 1.0-1.7 or Fase 2-4 numbering, since it was never part of the original roadmap approval.
- `security_group_rules` has no TTL by design — unlike every other collection in this system, it is expected to grow unbounded with the AWS account's actual security-group-rule count (including `eliminado` records, kept for audit trail). If retention becomes a concern, that is a distinct future decision, not covered here.
- Adding a second AWS account or provider (Azure NSGs, GCP firewall rules) later would need a new ADR — this one commits to a single-account, EC2-specific integration.
