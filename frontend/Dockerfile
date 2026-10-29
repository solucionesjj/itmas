# syntax=docker/dockerfile:1

ARG NODE_VERSION=24

# ---- Build: Angular production bundle ----------------------------------------
FROM node:${NODE_VERSION}-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npm run build

# ---- Test (optional target): docker build --target test ./frontend ----------
FROM build AS test
RUN npm test -- --watch=false

# ---- Runtime: static files + reverse proxy, non-root nginx ---------------------
FROM nginxinc/nginx-unprivileged:stable-alpine AS runtime
# Pick up Alpine security fixes published after the base image was built (the release scan blocks HIGH/CRITICAL with a fix).
USER root
RUN apk upgrade --no-cache
COPY nginx/default.conf.template nginx/security-headers.inc.template /etc/nginx/templates/
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html
# Non-secret defaults; override at runtime.
ENV API_UPSTREAM=http://api:8080 \
    ENTRA_AUTHORITY_ORIGIN=https://login.microsoftonline.com \
    EDGE_TRUSTED_PROXY_FROM=172.28.10.0/24
USER 101
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=3s --start-period=10s --retries=3 \
    CMD wget -q --spider http://127.0.0.1:8080/healthz || exit 1
