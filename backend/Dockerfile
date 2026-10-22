# syntax=docker/dockerfile:1

ARG DOTNET_VERSION=10.0

# ---- Build -------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-alpine AS build
WORKDIR /src
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

# Restore first (cached layer): only project and build metadata files.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY .config/dotnet-tools.json .config/
COPY src/Itmas3.Domain/Itmas3.Domain.csproj src/Itmas3.Domain/
COPY src/Itmas3.Application/Itmas3.Application.csproj src/Itmas3.Application/
COPY src/Itmas3.Infrastructure/Itmas3.Infrastructure.csproj src/Itmas3.Infrastructure/
COPY src/Itmas3.Api/Itmas3.Api.csproj src/Itmas3.Api/
RUN dotnet restore src/Itmas3.Api/Itmas3.Api.csproj && dotnet tool restore

COPY src/ src/
RUN dotnet publish src/Itmas3.Api/Itmas3.Api.csproj -c Release -o /out/api --no-restore /p:UseAppHost=false

# EF Core migration bundle: a single executable that applies pending migrations.
RUN dotnet ef migrations bundle \
        --project src/Itmas3.Infrastructure \
        --startup-project src/Itmas3.Infrastructure \
        --configuration Release \
        --output /out/migrator/efbundle

# ---- Migrator (one-shot): docker compose runs it before the API ---------------
# Reads Database__* environment variables (DesignTimeDbContextFactory); connects as the migrator role.
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine AS migrator
WORKDIR /app
COPY --from=build /out/migrator/ ./
USER $APP_UID
ENTRYPOINT ["./efbundle"]

# ---- API runtime (default target) ---------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine AS api
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
# IANA time zone database: the agenda works in the time zone of the organization (SPEC-0006 RNF-4, P-28) and the API refuses
# to start if it cannot resolve it. The Alpine base image of ASP.NET Core does not ship it; this is data, not a tool.
RUN apk add --no-cache tzdata
COPY --from=build /out/api/ ./
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=3 \
    CMD wget -qO- http://localhost:8080/health/live >/dev/null || exit 1
ENTRYPOINT ["dotnet", "Itmas3.Api.dll"]
