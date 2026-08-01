# ADR-0008: Reports export — content, format, and mixed RBAC

- **Status**: Accepted (sub-fase 1.6)
- **Related**: RF-19, Assumption #11 (Fase 1 scope resolution); CA-13

## Context

`GET /reports/export` is the one authoritative endpoint for RF-19, but neither spec.md nor agent.md specifies the report's content, the query parameter names, or how a single endpoint should express that Usuario may export one kind of report but not another.

## Decision

- **Two report types**: `devices` (hostname, category, os.name, os.version, lastSeen — the device inventory) and `alerts` (type, deviceId, flattened detail, createdAt, status). No pagination on export — the full filtered dataset is returned, appropriate at the NFR's stated scale.
- **Query param named `reportType`, not `type`** — deliberately avoids colliding with the alert domain's own `type` field (`resource_change|off_hours_access`), which is also a valid filter on the `alerts` report.
- **Mixed RBAC on one endpoint**: the route-level guard admits all three roles (Usuario can export `devices`, per RF-14/CA-13), but the service layer explicitly rejects (`403`) a Usuario requesting `reportType=alerts` — the same "declarative guard can't express a value-conditional rule, so the service adds the extra check" pattern already used for the sub-phase 1.4 self-lockout guard (ADR-0007).
- **CSV** is hand-built (comma/quote/newline escaping implemented directly — no library needed for a format this simple). **PDF** uses `pdfkit`, the one new production dependency introduced for this sub-phase, justified because no pure-computation approach produces valid PDF bytes — chosen over heavier alternatives (e.g. driving headless Chrome) for its small footprint and lack of a browser dependency.
- The response is **the one endpoint in the API that isn't the standard JSON envelope** — a raw file with `Content-Type`/`Content-Disposition` headers.

## Consequences

- A real integration bug surfaced from this naming decision: the frontend was initially built against `type` (matching the query param name used by every *other* list endpoint's filters) before the backend's `reportType` naming was confirmed — caught and fixed during cross-checking, but it's a reminder that this endpoint's param naming is the one place in the API that deliberately breaks the "the field is just called `type`" convention used everywhere else, for a good reason specific to this endpoint.
