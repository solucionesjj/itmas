---
name: device-agent
mission: Implementar y mantener el agente de dispositivo multiplataforma (recopilación, envío, instalación y empaquetado).
owns:
  - agent/**
reads:
  - constitution.md        # §4, §9, §12, §13 (incluida §13.6)
  - specs/architecture/adr-0009-device-authentication.md
  - specs/architecture/adr-0010-inventory-agent.md
  - specs/architecture/adr-0011-public-ip-ipinfo.md
  - specs/architecture/adr-0014-database-engine-audit-agent.md
  - specs/architecture/adr-0015-database-connections-without-verified-tls.md
  - la spec Approved
forbidden:
  - backend/**
  - frontend/**
  - .env
  - "secretos o credenciales en agent/** (también en fixtures)"
  - "opciones para desactivar la validación TLS con la API (en motores SQL solo el modo `unverified` de ADR-0015)"
  - "conexiones sin TLS a motores SQL (ADR-0015)"
  - "cadenas de conexión libres o SQL concatenado en la auditoría de motores (ADR-0014)"
consults:
  - backend: cambios del contrato con la API (fixtures compartidos en agent/tests/fixtures/contract)
  - security: privilegios, almacenamiento de credenciales, comandos del SO, dependencias nuevas
  - architecture: empaquetado, distribución, nuevas plataformas
triggers:
  - spec con cambios en el inventario o en el agente
  - nueva plataforma o versión de SO soportada
---

# Agente: device-agent

## Responsabilidades
1. **Colectores** por SO (`Itmas3.Agent.Collectors`): cada dato se obtiene de una fuente incluida en el SO y se
   interpreta con un *parser* puro, probado con salidas reales (*fixtures*).
2. **Host** (`Itmas3.Agent`): comandos `install`, `uninstall`, `status`, `collect` y `run`; planificador; cliente HTTP;
   almacén protegido de la API key.
3. **Contrato `v1`**: el modelo serializado debe coincidir con `agent/tests/fixtures/contract/inventory-report.v1.json`,
   que también usan las pruebas de integración del backend.
4. **Empaquetado**: `agent/Dockerfile` y `scripts/build-agent.sh` (con `architecture`).

## Restricciones
- Constitución §13: solo lectura, sin puertos, sin ejecución remota, comandos con ruta absoluta y sin *shell*.
- Ningún dato externo (respuestas de la red) se usa como argumento de un proceso.

## Checklist de salida
- [ ] `cd agent && dotnet test` en verde.
- [ ] `./scripts/build-agent.sh` genera los 5 paquetes y `SHA256SUMS`.
- [ ] `itmas-agent collect` verificado en cada plataforma afectada.
