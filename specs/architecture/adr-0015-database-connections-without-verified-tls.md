---
id: ADR-0015
title: Conexiones a motores con certificados no válidos (enmienda MAJOR 2.0.0)
status: Accepted
date: 2026-10-11
deciders: [architecture, security]
related: [SPEC-0005, ADR-0014]
---

# ADR-0015 — Conexiones a motores con certificados no válidos

## Contexto
ADR-0014 (punto 5) y la constitución 1.3.0 (§13.6) exigen que las conexiones del agente a motores de bases de datos
vayan cifradas y autentiquen al servidor, "sin ninguna opción para desactivar la validación".

El solicitante indica (SPEC-0005 §19.2 y §19.4) que algunos servidores no tienen un certificado válido y que hay que
auditarlos igualmente. Las conexiones sin TLS **no** se usarán.

La comunicación del agente con la API de ITMas (§13.3: HTTPS con validación que no puede desactivarse) no forma parte
de esta decisión.

## Decisión
Sustituye el **punto 5** de ADR-0014.

1. **Modo de TLS por conexión**, elegido por el administrador del equipo en `sql add` / `sql update`:

   | Modo | Comportamiento |
   |---|---|
   | `verify` (por defecto) | TLS con el certificado validado contra el almacén de confianza del sistema y el nombre de *host*. |
   | `pinned` | TLS aceptando solo el certificado fijado (archivo o huella SHA-256). Es la opción recomendada para certificados autofirmados. |
   | `unverified` | TLS sin validar el certificado: el canal va cifrado, pero no protege frente a la suplantación del motor. |

2. **Siempre con TLS.**
   - No existe un modo sin cifrado, tampoco para las conexiones al propio equipo.
   - Un motor que no acepte TLS no se puede auditar hasta que el DBA lo active.
   - El agente exige cifrado en cada controlador: `Encrypt=Mandatory` en SQL Server y `SslMode=Require` como mínimo en
     PostgreSQL, MySQL y MariaDB.
3. **Elección expresa de `unverified`.**
   - Nunca se aplica por defecto ni automáticamente, ni siquiera si `verify` falla.
   - En modo interactivo, el agente explica el riesgo, propone `pinned` como alternativa y pide confirmarlo escribiendo
     `ACEPTO EL RIESGO`. Sin consola interactiva, exige la opción `--tls unverified`.
   - La lista de conexiones no puede fijarse desde el servidor (§13.3).
4. **Visibilidad.** El modo de cada conexión viaja en la instantánea y `sql list` lo muestra. ITMas señala con un aviso
   las instancias auditadas con `unverified`.
5. **Controles compensatorios.**
   - La cuenta del agente es dedicada, de mínimo privilegio y de solo lectura de metadatos (ADR-0014, punto 4): si se
     intercepta, solo permite leer el catálogo.
   - Con `unverified`, el agente no habilita opciones del controlador que empeoren la exposición, como obtener la clave
     pública RSA del servidor en MySQL. La implementación lo verifica en cada controlador.
   - El manual recomienda `verify` o `pinned` y explica cómo provisionar un certificado en cada motor.

## Alternativas consideradas
| Alternativa | A favor | En contra |
|---|---|---|
| Mantener la regla estricta (ADR-0014) | Máxima protección del canal | No permite auditar los servidores con certificados no válidos. |
| Admitir conexiones sin TLS (`plain`) | Cubre motores sin TLS | Catálogo y, según el método de autenticación, credenciales en claro. Descartado por el solicitante. |
| Solo `pinned` para certificados no válidos | Protege frente a la suplantación | No sirve cuando el certificado cambia sin aviso (p. ej. SQL Server sin certificado provisionado lo regenera en cada reinicio). |
| Una opción global del agente | Más simple | Rebaja todas las conexiones por un solo motor. |
| Pasar a `unverified` automáticamente si `verify` falla | Sin intervención | Degradación silenciosa que un MITM podría forzar. |

## Consecuencias
- **Positivas:** se pueden auditar los servidores sin certificado válido. El canal siempre va cifrado. La excepción es
  expresa, por conexión y visible.
- **Negativas / riesgos (aceptados por el solicitante):** con `unverified`, un atacante en la red puede suplantar el
  motor. Puede enviar datos falsos y, según el método de autenticación, capturar o reutilizar la credencial del agente.
- **Seguimiento:** revisar periódicamente las instancias con aviso de TLS y pasarlas a `pinned` o `verify`.

## Impacto en la constitución
**MAJOR (2.0.0)**, porque relaja una regla (§1.2). Una viñeta de §13.6 pasa de:

> DEBE ir cifrada y autenticar al servidor según ADR-0014, sin ninguna opción para desactivar la validación.

a:

> DEBE ir cifrada. DEBE autenticar al servidor, salvo que el administrador del equipo elija expresamente, para esa
> conexión, aceptar un certificado no válido (ADR-0015). Esas conexiones DEBEN quedar señaladas en ITMas. Esta
> excepción no se aplica a la comunicación con la API (§13.3).

`security` confirma que la excepción queda acotada a la validación del certificado en las conexiones a motores: el
cifrado sigue siendo obligatorio y §13.3 no cambia.
