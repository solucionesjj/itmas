---
id: ADR-0009
title: Autenticación de dispositivos (agentes) con código de autorización y API key por dispositivo
status: Accepted
date: 2026-10-08
deciders: [architecture, security]
related: [SPEC-0002, ADR-0002, ADR-0003, ADR-0008]
---

# ADR-0009 — Autenticación de dispositivos

## Contexto
SPEC-0002 introduce el primer cliente no humano de la API: el agente de inventario instalado en los equipos. Hasta ahora
solo existía la autenticación de personas con Microsoft Entra ID mediante BFF (ADR-0002), y la constitución §3.3 solo
admitía tres formas de autorizar un endpoint. El agente no tiene identidad en Entra ID ni sesión de navegador, se
ejecuta en equipos no controlados por ITMas y llega desde Internet.

El solicitante pide un proceso de enrolamiento: al instalar se introduce un código de autorización y el servidor
entrega una API key propia de cada dispositivo. En la v1 no hay interfaz para gestionar códigos (llegará con SPEC-0003).

## Decisión
1. **Código de autorización (v1).** *Sustituido por [ADR-0012](adr-0012-per-device-enrollment-codes.md) (SPEC-0003): ahora cada equipo tiene su propio código de un solo uso.* Era un único valor secreto, guardado en la configuración del servidor
   (`Inventory__EnrollmentCode`, que `.env` define como `INVENTORY_ENROLLMENT_CODE`):
   - Lo genera `scripts/init-env.sh` y tiene al menos 32 caracteres.
   - Si no está configurado, el enrolamiento está desactivado y el endpoint responde `404`.
   - Se compara en tiempo constante, sobre los SHA-256 de ambos valores.
   - Se rota cambiando la variable y reiniciando la API, sin afectar a los dispositivos ya enrolados.
2. **API key por dispositivo.** Son 32 bytes aleatorios (CSPRNG) codificados en base64url y se entregan una sola vez,
   en la respuesta del enrolamiento. En la BD solo se guarda su SHA-256 (índice único), que basta frente a fuerza bruta
   por la entropía de la clave, y la búsqueda se hace por hash. Reenrolar el mismo identificador de hardware sustituye
   el hash, así que la clave anterior queda invalidada (RN-4).
3. **Esquemas de autenticación propios**, separados de la cookie BFF:
   - `DeviceEnrollment`: lee la cabecera `Authorization: Enrollment <código>`.
   - `Device`: lee la cabecera `Authorization: Device <apiKey>` y produce un principal con el claim `itmas3_device_id`.

   Ninguno es esquema por defecto: solo se evalúan en los endpoints cuya política los nombra.
4. **Dos políticas de autorización** que solo aceptan su esquema:
   - `DeviceEnrollment` → `POST /api/v1/device-enrollments`.
   - `EnrolledDevice` → `POST /api/v1/inventory-reports`.

   Una sesión de usuario no satisface estas políticas. A la inversa, una API key no satisface las políticas de usuario,
   porque estas autentican con el esquema por defecto (cookie). Un dispositivo no tiene permisos, políticas de SPEC-0001
   ni acceso a ningún otro endpoint.
5. **CSRF sin cambios.** El agente envía `X-CSRF: 1` y no envía `Origin`, así que `CsrfProtectionMiddleware` se
   mantiene igual.
6. **Límites de frecuencia propios:**
   - Enrolamiento: por IP, 10 por minuto. Frena también la fuerza bruta sobre el código.
   - Envío de inventarios: por credencial, 12 por hora. La partición se calcula con el hash de la cabecera
     `Authorization`, porque el limitador se ejecuta antes de la autenticación del endpoint. Las claves inválidas
     quedan además sujetas al límite global por IP.
7. **Auditoría:** los enrolamientos se registran en `audit_events` (SPEC-0001): `device.enrolled` y `device.relinked`,
   con la IP de origen, el hostname y la versión de los términos aceptados. Los intentos con un código incorrecto se
   registran en el log, sin el código.

## Alternativas consideradas
| Alternativa | A favor | En contra |
|---|---|---|
| Una API key compartida por toda la flota | Mínima complejidad | Si se filtra, compromete todos los dispositivos; no permite revocar uno solo ni atribuir los envíos (RN-1). |
| mTLS con certificado por dispositivo | Credencial no reutilizable fuera del equipo; estándar | Exige una PKI propia y que el borde TLS pase el certificado a la API. Evolución posible. |
| *Client credentials* de Entra ID | Reutiliza el IdP | Equivale a un secreto compartido distribuido a todos los equipos. |
| Códigos de enrolamiento por dispositivo y con caducidad | Menor impacto si se filtra un código | Requiere la interfaz de gestión: llega con SPEC-0003. |

## Consecuencias
- **Positivas:** credencial única y revocable por reinstalación; ningún secreto compartido queda en los equipos; los
  dispositivos y las personas están aislados por esquema y política.
- **Negativas / riesgos:**
  - Hasta SPEC-0003, el código es único y compartido: si se filtra, se pueden enrolar equipos falsos. Mitigación:
    rotación, límites de frecuencia y auditoría.
  - La API key es *bearer*: su seguridad depende de TLS y de la protección en el equipo (SPEC-0002 §14.2).
  - En la v1 no hay revocación desde la interfaz: existe un procedimiento operativo en SECURITY.md.
- **Seguimiento:** SPEC-0003 sustituyó el código único por códigos por equipo, de un solo uso, y añadió la revocación de dispositivos desde la interfaz (ADR-0012).

## Impacto en la constitución
Enmienda **MINOR** (1.1.0): §3.2 admite la autenticación de dispositivos según este ADR, y §3.3 añade las políticas de
dispositivo como cuarta forma de autorizar, limitada a endpoints de dispositivos. No relaja ningún control para personas.
