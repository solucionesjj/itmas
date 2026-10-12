# syntax=docker/dockerfile:1
# Builds the device agent without a local SDK (ADR-0010 §8). Used by scripts/build-agent.sh:
#   docker build --target artifacts --output type=local,dest=agent/dist/<version> agent
# Targets: test (runs the agent tests), artifacts (zip/tar.gz per RID + SHA256SUMS), e2e (Linux smoke test image).

ARG DOTNET_VERSION=10.0

# ---- Build and test -------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
ARG RIDS="win-x64 linux-x64 linux-arm64 osx-x64 osx-arm64"

COPY global.json Directory.Build.props Directory.Packages.props Itmas3.Agent.slnx ./
COPY src/Itmas3.Agent.Collectors/Itmas3.Agent.Collectors.csproj src/Itmas3.Agent.Collectors/
COPY src/Itmas3.Agent/Itmas3.Agent.csproj src/Itmas3.Agent/
COPY tests/Itmas3.Agent.Tests/Itmas3.Agent.Tests.csproj tests/Itmas3.Agent.Tests/
RUN dotnet restore tests/Itmas3.Agent.Tests/Itmas3.Agent.Tests.csproj \
    && for rid in $RIDS; do dotnet restore src/Itmas3.Agent/Itmas3.Agent.csproj -r "$rid" -p:SelfContained=true; done

COPY . .

FROM build AS test
RUN dotnet test tests/Itmas3.Agent.Tests/Itmas3.Agent.Tests.csproj --no-restore

# ---- Publish: self-contained single file per RID --------------------------------
FROM build AS publish
ARG RIDS="win-x64 linux-x64 linux-arm64 osx-x64 osx-arm64"
RUN for rid in $RIDS; do \
      dotnet publish src/Itmas3.Agent/Itmas3.Agent.csproj -c Release -r "$rid" --self-contained \
        -p:PublishSingleFile=true -p:DebugType=none -o "/out/$rid" || exit 1; \
    done

# ---- Package: one archive per RID + checksums ------------------------------------
FROM alpine:3.22 AS package
RUN apk add --no-cache zip tar
COPY --from=publish /out /out
WORKDIR /dist
RUN set -e; for dir in /out/*; do \
      rid="$(basename "$dir")"; \
      if [ "${rid#win-}" != "$rid" ]; then (cd "$dir" && zip -q -X "/dist/itmas-agent-$rid.zip" itmas-agent.exe); \
      else (cd "$dir" && chmod 0755 itmas-agent && tar -czf "/dist/itmas-agent-$rid.tar.gz" --owner=0 --group=0 itmas-agent); fi; \
    done; \
    sha256sum itmas-agent-* > SHA256SUMS

FROM scratch AS artifacts
COPY --from=package /dist/ /

# ---- E2E: run the Linux build in a supported distribution (docker compose --profile agent) ----
FROM ubuntu:24.04 AS e2e
ARG TARGETARCH
# libicu: ICU is required since SQL Server support (SPEC-0005); standard Ubuntu/Debian installations already have it.
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates iptables util-linux libicu74 \
    && rm -rf /var/lib/apt/lists/*
# Binary matching the architecture of the image (amd64 → linux-x64, arm64 → linux-arm64).
RUN --mount=type=bind,from=publish,source=/out,target=/out \
    mkdir -p /opt/itmas-agent \
    && cp "/out/linux-$([ "$TARGETARCH" = arm64 ] && echo arm64 || echo x64)/itmas-agent" /opt/itmas-agent/itmas-agent
ENTRYPOINT ["/opt/itmas-agent/itmas-agent"]
CMD ["collect"]
