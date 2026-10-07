---
id: ADR-0006
title: Logging estructurado, OpenTelemetry opcional y Problem Details
status: Accepted
date: 2026-10-07
deciders: [architecture, security]
related: [SPEC-0000]
---

# ADR-0006 — Logging estructurado, OpenTelemetry opcional y Problem Details

## Contexto
Se requieren logs estructurados con niveles, sin datos sensibles, preparados para observabilidad, y un manejo
centralizado de errores sin filtrar información interna.

## Decisión
- Logging nativo de .NET con formateador JSON en consola (`simple` en Development local), scopes con `TraceId`/`SpanId`,
  mensajes con `LoggerMessage` generado en compilación.
- `HttpLogging` limitado a método, ruta, estado y duración (sin query, cabeceras, cookies ni cuerpos); deshabilitado en `/health*`.
- OpenTelemetry (trazas, métricas, logs) con exportador OTLP **solo** si `OTEL_EXPORTER_OTLP_ENDPOINT` está definido.
- `GlobalExceptionHandler` (`IExceptionHandler`) + `AddProblemDetails` + `UseStatusCodePages`: toda respuesta de error
  es Problem Details con `traceId` e `instance`. `DomainException` → 422 con su mensaje (seguro por diseño);
  `NotFoundException` → 404; resto → 500 genérico.

## Alternativas consideradas
| Alternativa | A favor | En contra |
|---|---|---|
| Serilog | Ecosistema de *sinks* | Dependencia extra; OTel cubre la exportación de forma estándar |
| Middleware de excepciones propio | — | `IExceptionHandler` es el mecanismo oficial |

## Consecuencias
- Positivas: listo para cualquier backend OTLP (Azure Monitor, Grafana, Datadog…) sin cambios de código.
- Negativas: sin exportador configurado, la telemetría queda solo en la salida de los contenedores.

## Impacto en la constitución
Recogido en §3.7 y §12.2.
