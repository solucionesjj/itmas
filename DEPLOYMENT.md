# DEPLOYMENT.md

Docker packaging and CI for IT-MAS (sub-phase 1.7 — see `agent.md` §17). This
covers local/demo containerization and continuous integration; it does **not**
cover production infrastructure decisions, which are called out explicitly at
the bottom of this file.

## Running locally with Docker Compose

```
cp backend/.env.example backend/.env   # then fill in real secrets
docker compose up --build
```

This starts three services:

| Service | Host port | Notes |
|---|---|---|
| `mongo` | *(not published to the host)* | Only `backend` reaches it, over the compose network as `mongo:27017`. Data persists in the `mongo-data` named volume. |
| `backend` | `3100` → container `3000` | Reads config from `backend/.env` via `env_file`, plus `MONGO_URI` is overridden to point at the `mongo` service. |
| `frontend` | `8081` → container `80` | Static build served by Nginx, with SPA fallback routing (`frontend/nginx.conf`). |

Host ports `3100`/`8081` were chosen because `80`, `3000`, `8080`, and `27017`
were already bound by other containers on the machine this was built on —
adjust the `ports:` mappings in `docker-compose.yml` for your own environment.

Every environment variable the backend needs is documented in
`backend/.env.example` — that file is the single source of truth; this
document doesn't duplicate the list.

## Building images directly

```
docker build -t itmas-backend backend/
docker build -t itmas-frontend frontend/
```

Both are multi-stage builds: a `node:20-alpine` build stage compiles
TypeScript/bundles the Angular app, and the runtime stage is either a lean
`node:20-alpine` (backend, non-root `node` user, only production
dependencies) or `nginx:alpine` (frontend, static files only). No secrets are
baked into either image — all backend configuration is read from environment
variables at container start.

Note: `argon2` requires a native addon compiled against Alpine's musl libc, so
the backend Dockerfile installs `python3 make g++` in a dedicated `deps`
stage to produce `node_modules` once — those build tools never end up in the
final runtime image.

## CI pipeline

`.github/workflows/ci.yml` runs on every push/PR: for each of `backend/` and
`frontend/` — `npm ci → lint → build → test` (backend also runs `test:e2e`,
which spins up its own `mongodb-memory-server`, no separate Mongo service
needed in CI) `→ npm audit --omit=dev --audit-level=high`.

**Deploy is intentionally not automated** — target infrastructure, secrets
management, and TLS/reverse-proxy setup are environment-specific decisions for
whoever operates a real deployment, not something this pipeline can express.

## AWS Security Group audit extension (EXT-1 — ADR-0013/0014/0015)

`security-group-sync` calls the AWS EC2 API to build the `security_group_rules` catalog. Credentials are **never** configured through IT-MAS's own env vars or database — the AWS SDK's own default credential provider chain resolves them (environment variables, a shared credentials file, or an IAM role/instance profile), exactly as any other AWS SDK consumer on the host.

**Minimal IAM policy** — attach only this to whatever principal (IAM role/user) the backend process runs as:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "ec2:DescribeRegions",
        "ec2:DescribeSecurityGroups",
        "ec2:DescribeSecurityGroupRules",
        "ec2:DescribeNetworkInterfaces",
        "ec2:DescribeInstances"
      ],
      "Resource": "*"
    }
  ]
}
```

No write/mutate EC2 permission is ever needed — this integration is strictly read-only.

New optional env vars (all documented in `backend/.env.example`, all with safe defaults so the module is inert with zero configuration beyond an authenticated AWS context): `AWS_SYNC_REGIONS` (override auto-discovery), `AWS_SYNC_HOUR`/`AWS_SYNC_MINUTE` (daily automated run time, UTC), `AWS_SYNC_RUN_RETENTION_DAYS`.

**Operational security note, found during manual verification of this extension**: whatever host/container runs the IT-MAS backend must carry *only* the IAM policy above — scoped to a dedicated role or credential, never a developer's own broad ambient AWS credentials (e.g. inherited from a shell profile, SSO session, or instance profile meant for other work). During this feature's manual browser testing, the "Sincronizar ahora" button was triggered once in a local dev sandbox that turned out to have live AWS credentials on its PATH with far broader account access than intended for this test — it successfully synced ~1,100 real production security group rules across a real account before being caught and the local test database was destroyed immediately. No IT-MAS code caused this (the sync worked exactly as designed against whatever credentials it found); the risk is entirely in **which credentials are reachable from wherever this runs**. Treat `AWS_SYNC_*` credential exposure with the same care as any other production secret — a read-only policy still means "this process can enumerate every security group and firewall rule you have."

## What this does NOT solve (still manual/ops decisions — agent.md §6, §9)

- **TLS termination**: the API is meant to sit behind a reverse proxy / API
  gateway that terminates TLS (agent.md §6.1). Neither `backend`'s Nest app
  nor `frontend`'s Nginx container here does this themselves — add a
  reverse proxy (e.g. Traefik, an ALB, nginx-ingress) in front of both in any
  real deployment.
- **MongoDB replication**: `docker-compose.yml`'s `mongo` service is a single
  instance for local dev/demo convenience only. agent.md §9 requires a
  replica set in production — never a single instance.
- **Encryption at rest**: agent.md §6.9 requires storage-level encryption for
  MongoDB. That's a deployment/cluster configuration (e.g. an encrypted
  volume, or MongoDB Atlas's built-in encryption) — there is no way to
  meaningfully simulate or verify this in a local container, so it's flagged
  here rather than silently assumed solved.
