---
id: SPEC-0009
title: Producción en un único servidor Ubuntu con IP pública
type: feature
status: In Progress
owner: jmartinez
created: 2026-10-24
approved-by: jmartinez (2026-10-25)
agents: [architecture, security, testing, database, device-agent, frontend]
related: [ADR-0018, ADR-0019, ADR-0020, ADR-0007, ADR-0010, ADR-0011, SPEC-0002, SPEC-0008]
---

# SPEC-0009 — Producción en un único servidor Ubuntu con IP pública

> Nace del **Seguimiento de ADR-0018** («Pendiente de una spec de despliegue: copias de seguridad, endurecimiento del servidor,
> automatización y la desviación del checklist de SECURITY.md §8 sobre PostgreSQL») y formaliza el procedimiento manual de
> [DEVELOPMENT.md §12](../../DEVELOPMENT.md#12-publicación-en-un-servidor-linux-adr-0018-spec-0009). **El solicitante respondió las preguntas de §19 el
> 2026-10-25** (§19.1, con las propuestas **P-n** que aceptó o modificó en §19.2). **Aprobada el 2026-10-25** con las propuestas P-24, P-25 y P-26 y la
> exclusión C-1 de §19.3 (§19.4). **ADR-0018 fue aceptado el 2026-10-25** y su revisión de `security` se hizo el mismo día con cambios, que esta spec incorpora (§19.5). **La enmienda de §8 (ADR-0019, constitución 3.0.0) y ADR-0020 se aceptaron el 2026-10-26** (§19.6) y la spec pasó a `In Progress`. **El 2026-10-26 y el 2026-10-27 se implementó** (§18) y las revisiones independientes de `architecture`, `security` y `database` dejaron correcciones que ya están aplicadas (§19.7), junto con la decisión de que el repositorio es **público a propósito**. SPEC-0008 no cambia: en producción
> `AGENDA_OUTLOOK_ENABLED` sigue en `false` hasta que esté `Implemented`.

## 1. Objetivo

Que la publicación de ITMas en **un único servidor Ubuntu** sea **recuperable y repetible**: copias de seguridad cifradas fuera del
servidor que se sabe restaurar, un host endurecido, despliegues automatizados con vuelta atrás, avisos cuando algo falla, y lo que se aparta del
checklist de SECURITY.md §8 por esta topología decidido y registrado.

## 2. Contexto

ADR-0018 (`Accepted`) fijó el TLS: un proxy inverso en el host (Caddy como referencia) delante del `frontend`, que es el único puerto publicado y solo
en `127.0.0.1`. DEVELOPMENT.md §12 describe cómo publicar a mano y declara dos límites que esta spec cierra: **no incluye automatización** y **sus
comandos no se han ejecutado todavía en el servidor real**. Lo que hay hoy, comprobado en el repositorio el 2026-10-24:

- **El servidor** es una instancia de **AWS EC2 con Ubuntu 24.04 LTS** (respuesta del solicitante, 2026-10-25): el *security group*, los *snapshots* y el
  cifrado del volumen EBS están disponibles.
- **Las imágenes se construyen en el servidor** (`docker compose up -d --build`; `docker-compose.yml` usa `build:` e imágenes `itmas3/*:local`).
  No hay registro de imágenes, ni CI (SPEC-0007 §2 lo difirió, P-7), ni carpeta `.github/`.
- **No existe ninguna etiqueta de git.** §12.4 y §12.10 mandan desplegar siempre desde una etiqueta.
- **Las copias de seguridad son un requisito previo sin implementar.** §12.9 propone un `pg_dump -Fc` diario, retención local y una copia externa
  cifrada con destino «pendiente de decisión». Lo que hay en el volcado es sensible: la auditoría (`app.audit_events`), el mapa de cuentas
  privilegiadas de los motores SQL, datos del inventario y el *key ring* de Data Protection, que hoy se guarda **sin cifrar** en
  `app.data_protection_keys` (SECURITY.md §8).
- **El volcado no basta para reconstruir el servicio.** No contiene los roles de PostgreSQL (los crea `database/init/01-create-roles.sh` solo con el
  volumen vacío, con las contraseñas de `.env`) ni `.env`, que no está en ningún repositorio (constitución §4.1).
- **Endurecimiento**: §12.3 lo lista (`ufw`, SSH solo por clave, `fail2ban`, `unattended-upgrades`, `daemon.json` con rotación de logs) como pasos manuales
  sin verificación. SECURITY.md §8 añade que Docker publica puertos saltándose `ufw`.
- **Monitoreo**: no hay. `/health*` no se expone (nginx responde `404`), a propósito. `/auth/session`, que §12.7 usa como comprobación externa,
  **no consulta PostgreSQL** para un llamador anónimo (`GetCurrentUserProfileHandler` devuelve `null` antes de tocar la base de datos): una sonda sobre
  esa ruta detecta que el proxy, nginx y la API responden, **no** que la base de datos funcione.
- **PostgreSQL con TLS.** SECURITY.md §5 y §8 piden `Database__SslMode=VerifyFull` contra un servidor administrado. Aquí PostgreSQL es un contenedor
  de la red `data` (`internal: true`, sin salida, sin puertos publicados) y `docker-compose.yml` fija `Database__SslMode: Disable`. La línea del
  checklist agrupa también «backups y cifrado en reposo».

## 3. Alcance

- **Incluye:**
  1. **Copias de seguridad de PostgreSQL**: volcado diario, cifrado, copia externa a S3 de otra cuenta de AWS, retención, verificación y prueba de restauración.
  2. **Endurecimiento del host**: `ufw`, SSH solo por clave, `fail2ban`, `unattended-upgrades` y `daemon.json` de Docker con rotación de logs, con un script
     idempotente y verificable.
  3. **Automatización del despliegue**: GitHub Actions (suites, auditoría, imágenes en GHCR), script de despliegue con vuelta atrás ejecutado por una persona y
     publicación de los paquetes del agente.
  4. **Monitoreo mínimo**: sonda externa a `/auth/session`, comprobación del host y alertas de disco y de vencimiento del certificado, por correo.
  5. **La desviación del checklist de SECURITY.md §8 sobre PostgreSQL** (TLS aceptado como desviación; cifrado en reposo con EBS): decisión, registro y aceptación del riesgo residual.
  6. La documentación y la matriz de agentes que esto exige (§10.5).
  7. Los cambios que dejó la revisión de `security` de ADR-0018 (2026-10-25): el health check de `api` con `AllowedHosts`, la configuración de Caddy, la frontera de confianza de nginx y los requisitos del host (RF-26 a RF-29).
- **Excluye:**
  - Alta disponibilidad, un segundo servidor, balanceador o servicios administrados (ADR-0018: el servidor es un punto único de fallo).
  - Recuperación a un punto en el tiempo (archivado de WAL con pgBackRest o WAL-G): §12.9 la dejó fuera y Q-2 la mantiene fuera (RPO de 24 h).
  - Cambios en `backend/`, en `frontend/src/` (solo cambia `frontend/nginx/`, RF-28), el esquema o los roles de PostgreSQL, y TLS en el contenedor de PostgreSQL (Q-17: se acepta la desviación).
  - Activar Outlook (SPEC-0008), cambiar la credencial de Entra ID a certificado, cifrar el *key ring* y el resto de los ítems de SECURITY.md §8 que esta topología no cambia; de ellos solo se registra la desviación del `.env` en el host (Q-18).
  - Firma de código de los paquetes del agente (SPEC-0002 P-11, riesgo aceptado).
  - Aprovisionar el servidor con una herramienta de configuración (Q-8 eligió un script `sh` idempotente) y crear la infraestructura de AWS (instancia, volumen cifrado, *security group*, cuenta y bucket de destino de las copias): esta spec solo los enumera (§10.6, constitución §8.7) y verifica el resultado (C-1, §19.3).
  - Desplegar desde el CI: queda diferido (Q-11, P-12).
  - La enmienda de la constitución sobre §8 (hallazgo F2 de la revisión de ADR-0018): ya hecha en ADR-0019 (`Accepted`, constitución 3.0.0, 2026-10-26).
  - Normalizar a /64 la partición del límite de frecuencia por IP (cambio de API, spec aparte): se evita con RF-29 (sin registro `AAAA`).

## 4. Actores

| Actor | Descripción |
|---|---|
| Persona administradora del servidor | **Una sola persona** (Q-5): tiene SSH y `sudo`, prepara el host, ejecuta `deploy.sh`, custodia la clave que descifra las copias y el `.env` (con la copia sellada de RF-6) y recibe las alertas. |
| Quien etiqueta y aprueba | Crea la etiqueta `vX.Y.Z` y aprueba el entorno `production` de GitHub (Q-12). Hoy es la misma persona. |
| GitHub Actions | Ejecuta las pruebas, la auditoría y el escaneo, construye y publica imágenes y paquetes del agente en GHCR. **No tiene acceso al servidor** (Q-11). |
| GitHub Container Registry | Registro de imágenes y de los paquetes del agente (Q-9, Q-20). Con los paquetes públicos (recomendado, §19.7) el servidor lo lee sin credencial; con paquetes privados, con una credencial de solo lectura. |
| Destino de las copias | Bucket de S3 en **otra cuenta de AWS** (Q-1). |
| Servicio de sonda externa | Servicio gratuito que consulta `/auth/session`, recibe los latidos del host y envía el correo (Q-15). |
| Colaboradores | Descargan el agente de `/downloads/agent/` (SPEC-0002 RF-18); no cambia para ellos. |
| `security`, `architecture` | Revisan; ver la matriz de §10.5. |

## 5. Requerimientos

### 5.1 Funcionales

**Copias de seguridad**

- **RF-1** Un volcado de la base de datos (`pg_dump -Fc`, como hoy en §12.9) se genera **una vez al día** (Q-2: RPO de 24 h y RTO de un día hábil, sin archivado de WAL) y **antes de cada despliegue** (RF-16).
- **RF-2** Cada volcado se **cifra antes de salir del servidor** y se copia a un bucket de **Amazon S3 de otra cuenta de AWS** distinta de la del servidor (Q-1). El cifrado no es opcional (§12.9). La clave que lo descifra **no reside en el servidor** (P-2).
  La credencial del servidor sobre el destino solo permite **añadir** objetos, y el destino impide borrarlos antes de que venza la retención (P-1, P-25).
- **RF-3** Retención de **7 días en el servidor y 30 días en el destino** (Q-3). El servidor solo borra sus copias locales; el vencimiento en el destino lo aplica el propio destino (RN-4). Borrar lo que excede la retención no borra nada más.
- **RF-4** Cada volcado se comprueba al crearse: hay espacio para él, no está vacío y `pg_restore` lo **lee entero** (no basta con el índice: uno truncado se detecta), y de él se calculan los recuentos de filas de cada tabla (no de la base viva, para que no compitan con las escrituras).
  Un volcado ausente, vacío, ilegible o truncado, o una copia externa que falla, genera un aviso (RF-23). Lo que no llegó al destino se reintenta en la ejecución siguiente (marca `.uploaded` por archivo).
- **RF-5** Existe un **procedimiento de restauración completo** y se **prueba**: antes de publicar y **cada trimestre** (Q-4), en un contenedor temporal en un equipo de trabajo con Docker, **nunca** en el servidor de producción (el script se niega si encuentra `host.conf`).
  Cubre lo que el volcado no trae (roles creados por `database/init/01-create-roles.sh`, contraseñas de `.env`, propietarios y privilegios): restaura en **una sola transacción** (si algo falla no queda nada a medias), compara los recuentos de **todas** las tablas con los del volcado, comprueba que el rol de ejecución de la API conserva solo DML (sin DDL, sin `TRUNCATE`,
  sin propiedad, sin acceso al historial de migraciones) y, con las imágenes indicadas, ejecuta el `migrator` (debe terminar en 0) y levanta la API sana sobre los datos restaurados. Nunca restaura sobre la base de producción: DEVELOPMENT.md §12.9 describe la recuperación con una base nueva.
- **RF-6** El procedimiento define dónde se guardan las dos cosas que **no** están en el volcado ni en el repositorio: el `.env` del servidor y la clave privada que descifra las copias. Las opera **una sola persona** (Q-5) y se guarda **una copia sellada de ambas fuera de línea**,
  en custodia física o en el gestor corporativo, para que otra persona autorizada pueda restaurar si la titular no está (riesgo R-2, §14.4).

**Endurecimiento del host**

- **RF-7** `ufw`: política de entrada `deny`, salida `allow`; solo SSH, 80/tcp y 443/tcp (los de §12.3 y SECURITY.md §8). SSH queda **abierto a Internet** (Q-6) y se protege con RF-8 y RF-9. El *security group* de AWS abre los mismos tres puertos (P-24).
- **RF-8** SSH solo por clave, sin contraseña y sin `root` (§12.3), activado **después** de comprobar que la clave funciona.
- **RF-9** `fail2ban` con la protección de `sshd` activa.
- **RF-10** `unattended-upgrades` activo **solo con actualizaciones de seguridad y sin reinicio automático** (Q-7). Cuando el sistema pide reiniciar (`/var/run/reboot-required`), la comprobación del host avisa (RF-23) y el reinicio se hace en una ventana acordada.
- **RF-11** `/etc/docker/daemon.json` con rotación de logs (§12.3: controlador `local`, 20 MB, 5 archivos) aplicada **antes** de crear los contenedores.
- **RF-12** El endurecimiento se aplica y se **verifica** con un **script `sh` idempotente** (Q-8), con un modo de comprobación y sin herramienta de configuración: cada punto de RF-7 a RF-11 se puede comprobar con comandos documentados.

**Automatización del despliegue**

- **RF-13** Ninguna imagen se publica si el commit no pasó la suite completa de AGENTS.md (constitución §9.5): backend, frontend, pruebas unitarias del agente y pruebas de los scripts (con ShellCheck sobre ellos). Antes de publicar se ejecutan además `npm audit`, NuGet Audit y un escaneo de las imágenes construidas, que **bloquea** solo con severidad alta o crítica con corrección disponible (Q-10, P-11).
- **RF-14** Las imágenes `api`, `migrator` y `frontend` se construyen y escanean en GitHub Actions a partir de una **etiqueta `vMAJOR.MINOR.PATCH`** (Q-12) con un token de solo lectura, y solo tras la aprobación manual del entorno `production` se publican en **GitHub Container Registry** (Q-9) las mismas imágenes escaneadas, con **etiquetas inmutables**
  (si alguna de las tres ya existe no se publica ninguna). El servidor las usa **por *digest*** (SECURITY.md §8), no por etiqueta mutable.
- **RF-15** Existe una definición de Compose para producción que usa esas imágenes **sin romper** `docker compose up --build` local, que sigue construyendo desde el código (constitución §8.1; RNF-7).
- **RF-16** Un **script de despliegue** en el repositorio, `scripts/deploy.sh <etiqueta>`, que ejecuta **una persona administradora en el servidor por SSH, con `sudo`** (Q-11), en este orden: (1) valida el formato de la etiqueta y trae las etiquetas **sin `--force`**: la etiqueta no puede haberse movido y su *commit* debe estar en `origin/main`; (2) resuelve los *digests* de las tres imágenes, muestra el *commit* y los *digests* y **pide confirmación antes de tocar nada** (`--yes` la omite);
  (3) hace el `checkout` y valida con `docker compose config -q` (sin imprimir el `.env`); (4) comprueba que el Compose resultante no publica ningún puerto distinto de `127.0.0.1:${FRONTEND_PORT}` (ADR-0018 §2; SECURITY.md §8) y se detiene si lo hace; (5) hace el volcado previo (RF-1) **si ya existe el volumen de datos**, esté o no `postgres` en marcha (la instalación inicial no tiene qué volcar);
  (6) descarga de GHCR las imágenes por *digest*; (7) asegura `postgres` y **ejecuta el `migrator` con la imagen nueva** mientras la versión actual sigue atendiendo, y registra el nivel del esquema antes y después (`app_migrations.__ef_migrations_history`); (8) levanta `api` y `frontend` con la versión nueva (`docker-compose.yml` más `docker-compose.prod.yml`, nunca el de desarrollo) y espera a que `postgres`, `api` y `frontend` estén `healthy`;
  (9) comprueba `/auth/session` **por el loopback del `frontend`** enviando el `Host` permitido (sin él, `AllowedHosts` responde 400), de modo que funciona también antes de crear el registro DNS (Q-19); (10) registra versión, *commit*, *digests*, esquema, hora y resultado, **sin secretos**, y marca la versión como verificada solo si la salud y la sonda salieron bien (hasta entonces consta «sin verificar»);
  (11) conserva las imágenes de la actual y la anterior y elimina las más antiguas; (12) publica los paquetes del agente (RF-19). Un candado con el PID de su dueño evita dos despliegues a la vez y se recupera si el proceso murió. Nunca ejecuta `docker compose down -v`. Códigos de salida: 0 correcto, 1 uso o rechazo previo, 2 puerto no permitido, 3 fallo antes de la conmutación, 4 fallo después del `migrator`, 5 paquetes del agente.
- **RF-17** Vuelta atrás (Q-13): si el fallo ocurre **antes de que el `migrator` termine con éxito**, el script no ha tocado la versión que atiende: termina con error y avisa (si el `migrator` falló a medias, el esquema **pudo avanzar** y el mensaje lo dice). **Después** del `migrator` no revierte las imágenes: avisa y deja la decisión a una persona (restaurar el volcado previo, §12.10).
  Para esa decisión existe `deploy.sh rollback`, que repone los *digests* anteriores **sin** ejecutar el `migrator`: compara el nivel del esquema actual con el de la versión a la que vuelve y **se niega si difiere**, salvo `--schema-changed-ok` cuando una persona sabe que el cambio es compatible (ADR-0020). Solo una versión verificada pasa a ser «la anterior», y volver a desplegar la misma etiqueta no la sustituye.
  `database` revisa en cada migración si es compatible con la versión anterior (P-14).
- **RF-18** El CI **no tiene acceso al servidor** (Q-11): construye, prueba y publica; una persona ejecuta `deploy.sh`. La publicación exige la aprobación manual del entorno `production` de GitHub (limitado a las etiquetas `v*`), y `main` y las etiquetas `v*` están protegidas (Q-12); los *workflows* de colaboradores externos exigen aprobación (el repositorio es público, §19.7). Desplegar desde el CI queda diferido hasta que el volumen de despliegues lo justifique; el script es el único punto de entrada y no cambiaría (P-12).
- **RF-19** **Paquetes del agente**: se construyen en el CI con el mismo procedimiento que `scripts/build-agent.sh`, con su `SHA256SUMS` generado en esa misma compilación, **solo cuando cambia la versión del agente** (`agent/Directory.Build.props`) y se publican en GHCR junto a las imágenes (Q-14, Q-20; el mecanismo concreto, en ADR-0020, P-26).
  `deploy.sh` los descarga por el mismo canal que las imágenes, verifica sus sumas y que no haya archivos sin listar en `SHA256SUMS`, y los deja en `/downloads/agent/<versión>/` y `/downloads/agent/latest/` (volumen `AGENT_DOWNLOADS_DIR`, SPEC-0002 RF-18). **Una versión publicada no se sobrescribe.** `latest` solo cambia si la versión es la más alta, y con un renombrado de directorio:
  no vale un enlace simbólico, porque la plantilla de nginx usa `disable_symlinks on` en esa ruta. Un fallo aquí no revierte el despliegue (código 5).

**Monitoreo mínimo**

- **RF-20** Una **sonda externa** (fuera del servidor) consulta `https://itmas.sacservices.cloud/auth/session` **cada 5 minutos** y avisa tras **2 fallos consecutivos** (unos 10 minutos) si no responde `200` con `isAuthenticated: false`, y avisa de la recuperación (Q-15). Cubre proxy, nginx y API, **no PostgreSQL** (§2): eso lo cubre RF-23.
- **RF-21** Alerta de **espacio en disco** del host cuando el uso llegue al **80 %**, sobre los sistemas de archivos de Docker y de las copias locales (Q-15, P-18).
- **RF-22** Alerta de **vencimiento del certificado** cuando queden **menos de 14 días** (Q-15, P-18).
- **RF-23** Las alertas llegan **por correo a una lista de distribución del equipo de infraestructura** (Q-15). El host **envía latidos** al servicio externo en lugar de enviar correo (P-17): no guarda credenciales de SMTP y, si dejan de llegar, el servicio avisa de que el servidor o sus tareas se han parado.
  Una **comprobación en el host** revisa que `postgres`, `api` y `frontend` estén `healthy` (Q-16), el servicio de Caddy activo, el disco, el certificado, la copia del día, la ausencia de `/var/run/reboot-required`, que Docker no publique nada fuera de `127.0.0.1` (se vigila cada 5 minutos, no solo en cada despliegue) y que ninguna actualización de seguridad de Ubuntu, de Caddy o de Docker lleve más de 7 días pendiente, aunque `apt` falle (RF-31). Además exige que el
  **último** volcado cifrado tenga su marca de copia externa (`.uploaded`). Reporta el resultado por ese canal.

**Desviación de PostgreSQL**

- **RF-24** La desviación del checklist de SECURITY.md §8 sobre PostgreSQL se registra en SECURITY.md con su motivo, fecha y aceptación del riesgo residual, y `security` la revisa. **TLS:** se acepta `Database__SslMode=Disable` (Q-17, A) por tratarse de la red `data` interna de un solo host, con las copias cifradas como compensación; se revisa si esa red deja de ser interna o el servidor aloja otros servicios (P-20).
  **Cifrado en reposo:** se cifra el volumen EBS del servidor al crearlo (Q-17) y se verifica (CA-19). Se registran también la desviación del `.env` en el host (Q-18, R-4) y la de **SSH abierto** a Internet (Q-6, R-1), que contradice «22 (restringido)» de SECURITY.md §8.

**Documentación**

- **RF-25** DEVELOPMENT.md §12 pasa a describir el procedimiento **realmente ejecutado**, con los scripts de esta spec en lugar de los comandos sueltos; README, ARCHITECTURE, SECURITY, `.env.example` y AGENTS.md se actualizan en el mismo cambio (constitución §10.1, §4.3).

**Frontera de confianza del proxy y del host** (revisión de `security` de ADR-0018, 2026-10-25)

- **RF-26** El health check de `api` y la comprobación de RF-16 (7) envían un `Host` permitido por `AllowedHosts`. Hoy `wget http://localhost:8080/health` recibe `400 Invalid Hostname` con el `APP_ALLOWED_HOSTS` de producción: `api` no queda `healthy` y `frontend` no arranca (`depends_on: service_healthy`).
  **No** se añade `localhost` a `APP_ALLOWED_HOSTS` (constitución §3.9).
- **RF-27** La configuración de Caddy cumple ADR-0018 (decisiones 1, 4 y 6): emisor fijado a Let's Encrypt con correo de contacto; HSTS escalonado (300 s, 86 400 s y 31 536 000 s) sin `includeSubDomains` ni `preload`; sin `Server` ni `Via`; solo HTTP/1.1 y HTTP/2; administración fuera de TCP
  (`admin off` y `restart` en lugar de `reload`, o socket Unix); huella de la clave GPG comprobada; actualización periódica con aviso (`unattended-upgrades` no actualiza ese repositorio); `/var/lib/caddy` accesible solo para su usuario. Se versiona en `deploy/**`.
- **RF-28** La plantilla de nginx cumple ADR-0018 (decisión 3): `real_ip_recursive off` explícito; `X-Forwarded-Proto` aceptado solo desde el rango de confianza; `set_real_ip_from` acotado a la puerta de enlace de `edge` (con `gateway` fijado en el `ipam` de Compose) y parametrizado con `envsubst`,
  **solo después de comprobar en Linux** qué origen ve nginx (CA-22). Una prueba repetible de la plantilla (`scripts/test-nginx-realip.sh`, al estilo de `test-commit-msg-hook.sh`) ejecuta la tabla de «Comprobaciones del contrato» del ADR más los casos de origen y de esquema.
- **RF-29** Requisitos del host (ADR-0018, decisión 6): Docker Engine 28.0 o posterior (la versión concreta, al implementar) sin `DOCKER_INSECURE_NO_IPTABLES_RAW`; **sin registro `AAAA`** salvo decisión de `security`; con P-25, IMDSv2 obligatorio con *hop limit* 1 o bloqueo de `169.254.169.254` desde los contenedores (la red `edge` tiene salida);
  y comprobar que `172.28.10.0/24` no se solapa con la red de la VPC antes de `docker compose up`.

**Reglas de §8.7 de la constitución** (ADR-0019, 3.0.0)

- **RF-30** Lo instalado coincide con lo versionado (§8.7 a): `scripts/harden-host.sh check` y `scripts/install-host-tasks.sh check` comparan cada archivo instalado con su original en `deploy/**` e informan las diferencias. `deploy.sh` no instala ni modifica la configuración del host.
- **RF-31** Roles y parches del software que escucha fuera de loopback, Caddy (80 y 443) y `sshd` (22) (§8.7 d): el rol responsable es la persona administradora; la **cadencia máxima de parches de seguridad es de 7 días** (P-27); los de Ubuntu los aplica `unattended-upgrades` (que puede fallar sin avisar) y los de Caddy y Docker, que vienen de repositorios de terceros, son un paso manual con aviso;
  la comprobación del host avisa si una actualización de seguridad del sistema, de Caddy o de Docker lleva más de 7 días pendiente, si `apt` no puede listarlas, o si el servicio de Caddy no está activo. `sshd` solo admite clave y Caddy no tiene administración en TCP. Las claves y credenciales (Caddy, la de GHCR si existe, la URL de latido, `/etc/itmas3/host.conf`) son de `root` y legibles solo por su usuario.
  Todo lo del servidor se ejecuta como `root` con `sudo`: el clon `/opt/itmas3` es de `root` y nadie va en el grupo `docker`. Las unidades de systemd de las tareas del host aplican sandbox (`NoNewPrivileges` y directorios de solo lectura salvo sus estados).

### 5.2 No funcionales

- **RNF-1 Secretos (constitución §4).** Ninguno en el repositorio, en los *workflows*, en las imágenes, en `docker-compose*.yml` ni en logs. Los de CI viven en los secretos de GitHub (entorno de producción); ninguno pasa por la línea de comandos (`docker login --password-stdin`, variables de entorno).
- **RNF-2 Mínimo privilegio (§3.8).** El `GITHUB_TOKEN` con los permisos mínimos de cada *workflow*; **ninguna credencial de GitHub da acceso al servidor** (Q-11); la del servidor hacia GHCR, si los paquetes son privados, es de solo lectura (con paquetes públicos no hay ninguna); cada acción de terceros y cada herramienta nueva se justifica (constitución §12.5: mantenimiento activo y licencia) y se fija a una versión inmutable.
- **RNF-3 Simplicidad (§2.5).** Scripts en `sh` POSIX como los de `scripts/` (SPEC-0007 RNF-4), sin gestor de configuración ni servicios nuevos en Compose. Cada pieza nueva se difiere hasta que el volumen la exija (P-12).
- **RNF-4 Idempotencia.** El endurecimiento y la instalación de las tareas programadas pueden ejecutarse varias veces sin efectos acumulativos.
- **RNF-5 Disponibilidad.** Sin cambio de la de ADR-0018: la actualización corta el servicio unos segundos (§12.10). Los objetivos de recuperación son los de Q-2 (RPO de 24 h, RTO de un día hábil) y dependen de una sola persona administradora (R-2).
- **RNF-6 Idioma (§12.4).** Documentación en español; scripts, comentarios y mensajes de log en inglés.
- **RNF-7 Desarrollo local intacto.** `docker compose up --build` sigue levantando todo desde el código, sin registro ni credenciales.
- **RNF-8 Verificable (§9.2).** Cada criterio de §9 tiene una prueba automatizada o una verificación documentada en §18, sin direcciones, identificadores ni secretos.

## 6. Reglas de negocio

Son reglas de proceso (no hay dominio de aplicación afectado).

- **RN-1** Una copia que no se ha restaurado con éxito no cuenta como copia: **no se publica el servicio** hasta cumplir RF-5 una vez (§12.9 ya lo llama «requisito previo»).
- **RN-2** Nada llega a producción sin: etiqueta `v*` → suite, auditoría y escaneo en verde → aprobación del entorno `production` → imágenes publicadas → despliegue por una persona con `deploy.sh`. No se despliega desde una rama.
- **RN-3** Ningún script ejecuta `docker compose down -v` ni borra el volumen `postgres-data` (§12.10).
- **RN-4** El servidor no puede descifrar sus propias copias ni leer, listar o borrar las externas: solo **añadir** versiones (P-1, P-2). Volver a escribir una clave añade una versión y no destruye la anterior (versionado y *Object Lock* en el destino), de modo que un servidor comprometido no destruye su recuperación.
- **RN-5** Solo el `frontend`, en `127.0.0.1`, publica un puerto (constitución §8.4; ADR-0018). RF-16 lo verifica en cada despliegue porque Docker se salta `ufw`.
- **RN-6** Los volcados y sus copias son datos sensibles (§2): se tratan como la base de datos, con acceso limitado a la persona administradora.
- **RN-7** Una versión del agente ya publicada no se sobrescribe (Q-14).

## 7. Permisos

Ninguno. La spec no añade endpoints ni permisos de la aplicación (constitución §3.3). El acceso al servidor, a GitHub, a GHCR y al destino de las copias se rige por esos sistemas, no por el catálogo de permisos.

## 8. Comportamiento esperado

1. **Preparar el servidor (una vez).** Volumen EBS cifrado y *security group* con 22, 80 y 443 (fuera de esta spec, C-1) → endurecimiento (RF-7 a RF-12) → Docker → `.env` y primera restauración de prueba (RF-5) → ensayo de `deploy.sh` y de la vuelta atrás **en el propio servidor, antes de crear el registro DNS** (Q-19) → DNS y Caddy (ADR-0018) → §12.7 de DEVELOPMENT.md: verificar desde fuera, cerrar el *bootstrap* de Root y comprobar la IP real.
2. **Copia diaria.** El servidor vuelca, comprueba, cifra con la clave pública y copia al destino; borra sus copias locales de más de 7 días; reporta el resultado al servicio externo.
3. **Publicar una versión.** Se crea la etiqueta `vX.Y.Z` → el CI ejecuta las suites, la auditoría y el escaneo → tras la aprobación del entorno `production` construye y publica las imágenes (y los paquetes del agente si cambió su versión) → la persona administradora ejecuta `scripts/deploy.sh <etiqueta>` en el servidor, que hace los pasos de RF-16.
4. **Despliegue fallido.** Falla la salud o `/auth/session`: si el `migrator` aún no terminó, el script restaura la versión anterior (RF-17), avisa y deja el motivo en el registro; si ya terminó, avisa y no revierte.
5. **Alertas.** La sonda y la comprobación del host avisan por correo a la lista del equipo (con la espera de 2 fallos); la recuperación también se avisa.
6. **Recuperar el servicio.** Con un servidor nuevo: endurecimiento → Docker/Caddy → `.env` y clave desde la copia sellada → restauración del último volcado → comprobaciones de §12.7.

## 9. Criterios de aceptación

Los de infraestructura se verifican **en el propio servidor, antes de crear el registro DNS** cuando es posible (Q-19), y se documentan en §18 (constitución §9.2).

- [ ] **CA-1** (RF-1, RF-2) Dado el servidor en marcha, cuando llega la hora del volcado, entonces hay un volcado **cifrado** con la fecha del día en el bucket de la cuenta de destino, y en el servidor no hay la clave que lo descifra.
- [ ] **CA-2** (RF-3) La retención deja 7 días en el servidor y 30 en el destino y no elimina nada más, y la credencial del servidor no puede leer, listar ni borrar objetos del destino, y volver a escribir una clave solo añade una versión (RN-4).
- [ ] **CA-3** (RF-4, RF-23) Con un volcado ausente o ilegible simulado, llega un aviso por correo a la lista del equipo.
- [ ] **CA-4** (RF-5, RN-1) Con un volcado real, restaurado en un contenedor temporal **fuera** del servidor de producción siguiendo solo el procedimiento escrito, la API queda `healthy`, `/auth/session` responde y los recuentos de las tablas de `app` coinciden con el origen. Se anotan fecha y duración (sin datos).
- [ ] **CA-5** (RF-7) Un escaneo desde fuera muestra abiertos solo SSH, 80 y 443 (TCP, ningún UDP), el *security group* de AWS coincide con `ufw` (P-24) y `docker ps` muestra como único puerto publicado `127.0.0.1:${FRONTEND_PORT}`.
- [ ] **CA-6** (RF-8) SSH con contraseña y como `root` es rechazado; con la clave, aceptado.
- [ ] **CA-7** (RF-9, RF-10) `fail2ban` bloquea una IP de prueba tras los intentos fallidos configurados; `unattended-upgrades` está activo solo con seguridad y sin reinicio automático, su ejecución en seco (`unattended-upgrade --dry-run`) no da errores, y con `/var/run/reboot-required` presente la comprobación del host avisa.
- [ ] **CA-8** (RF-11) `docker info` muestra el controlador `local` con `max-size` y `max-file`, y un contenedor creado después rota sus logs.
- [ ] **CA-9** Tras reiniciar el servidor, todo vuelve solo: `docker compose ps` con los servicios `healthy` y el proxy activo (§12.10).
- [ ] **CA-10** (RF-13, RF-14, RF-18) Un commit con una prueba fallida, o con una vulnerabilidad alta o crítica con corrección, no produce imágenes. Una etiqueta sobre un commit en verde espera la aprobación del entorno `production`, publica `api`, `migrator` y `frontend` en GHCR y el servidor las usa por *digest*. No existe ninguna credencial de GitHub con acceso al servidor.
- [ ] **CA-11** (RF-16) Un despliegue correcto termina con los servicios `healthy`, el `migrator` en 0, `/auth/session` correcto y un registro con versión, *digests*, hora y resultado, sin secretos.
- [ ] **CA-12** (RF-16, RF-17) Con una versión que no llega a `healthy` **antes** del `migrator` (simulada), el script deja la anterior en marcha, termina con error y avisa. Con un fallo **después** del `migrator`, no revierte, avisa y deja el motivo en el registro. Se verifica **antes** de la primera publicación real.
- [ ] **CA-13** (RF-16, RN-5) Si el Compose resultante publicara un puerto distinto de `127.0.0.1:${FRONTEND_PORT}`, el despliegue se detiene antes de levantar nada.
- [ ] **CA-14** (RNF-1, RNF-2) Los *workflows* declaran `permissions` mínimos, no contienen secretos, y la búsqueda de secretos en ellos, en los scripts y en los logs de una ejecución es limpia; el script no imprime el `.env`.
- [ ] **CA-15** (RF-19, RN-7) Cambiar la versión del agente publica los cinco paquetes y `SHA256SUMS`; tras `deploy.sh`, `/downloads/agent/<versión>/` y `/downloads/agent/latest/` los sirven, la suma de cada archivo coincide y SPEC-0002 CA-19 sigue cumpliéndose. Sin cambio de versión no se publica nada, y una versión ya publicada no se sobrescribe.
- [ ] **CA-16** (RF-20, RF-23) Al detener el `frontend` (en el ensayo o en una ventana acordada), el aviso llega tras el segundo fallo de la sonda y al restablecerlo llega la recuperación; con un contenedor `unhealthy` simulado, la comprobación del host avisa.
- [ ] **CA-17** (RF-21) Con el umbral de disco bajado temporalmente, llega la alerta (el real es el 80 %).
- [ ] **CA-18** (RF-22) La comprobación alerta cuando al certificado le quedan menos de los días configurados (`CERT_WARN_DAYS`, 14 por defecto): con un umbral simulado superior a los días reales (`openssl s_client … | openssl x509 -noout -enddate` los muestra) genera la alerta.
- [ ] **CA-19** (RF-24) SECURITY.md §8 refleja la desviación de TLS y la de `.env`, con fecha y aceptante; el volumen EBS figura como cifrado (`Encrypted: true` en la descripción del volumen); `security` no tiene hallazgos críticos ni altos abiertos.
- [ ] **CA-20** Primer despliegue: la IP de conexión en **Inventario** es la del cliente y el `POST /api/v1/device-enrollments` con `Authorization: Enrollment <valor inválido>` y `X-Forwarded-For: 1.2.3.4` da `401` con la IP **real** del cliente en el log de `api` («Device enrollment rejected … from»), nunca `1.2.3.4` (ADR-0018 «Seguimiento»; SPEC-0002 CA-20; DEVELOPMENT.md §12.7, paso 5).
- [ ] **CA-21** (RF-25, RNF-7) La documentación y la matriz de agentes están al día en el mismo cambio y `docker compose up --build` local sigue sano.
- [ ] **CA-22** (RF-28) Se comprueba y se anota el origen que ve nginx en Linux (`curl -s http://127.0.0.1:${FRONTEND_PORT}/` y `docker compose logs --tail=3 frontend`: la puerta de enlace de `edge`); solo entonces se estrecha `set_real_ip_from`. Un `X-Forwarded-For` falso contra el loopback desde el propio servidor se ve como falso, y queda documentado como límite del modelo de confianza.
- [ ] **CA-23** (RF-26) Con el `APP_ALLOWED_HOSTS` de producción, `api` queda `healthy`, `frontend` arranca y la comprobación de RF-16 (7) responde `200` con el `Host` permitido.
- [ ] **CA-24** (RF-27) `curl -sI https://itmas.sacservices.cloud/` muestra un solo `Strict-Transport-Security` y ni `Server`, ni `Via`, ni `Alt-Svc`; el emisor del certificado es Let's Encrypt; `ss -ltnp` y `ss -lunp` no muestran la administración de Caddy en TCP ni UDP/443; el HSTS solo sube de nivel tras una renovación real.
- [ ] **CA-25** (RF-28) La prueba repetible de la plantilla pasa la tabla del ADR y los casos de origen y de esquema (un `X-Forwarded-Proto: https` desde un origen no confiable da `$scheme`).
- [ ] **CA-26** (RF-29) Con `curl --path-as-is`, `/%68ealth`, `//health`, `/./health` y `/api/..%2fhealth` no devuelven `200` de la API a través de Caddy y nginx.
- [ ] **CA-27** (RF-29) Docker Engine está en la versión exigida y sin `DOCKER_INSECURE_NO_IPTABLES_RAW`; no hay registro `AAAA`; desde un contenedor, `169.254.169.254` está bloqueado o exige IMDSv2 con *hop limit* 1; `172.28.10.0/24` no se solapa con la VPC.
- [ ] **CA-28** (RF-30) `scripts/harden-host.sh check` y `scripts/install-host-tasks.sh check` informan «sin diferencias» entre lo instalado y `deploy/**`; con un archivo instalado modificado a mano, informan la diferencia.
- [ ] **CA-29** (RF-31) Con una actualización de Caddy pendiente más de 7 días (simulada), la comprobación del host avisa; con el servicio de Caddy detenido, avisa; `/var/lib/caddy`, `/etc/itmas3/host.conf` y el archivo de credenciales de Docker solo son legibles por su usuario; la huella de la clave GPG de Caddy figura anotada en §18.

## 10. Análisis arquitectónico

### 10.1 Qué cambia y qué no

- **Capas de la aplicación** (`Domain`, `Application`, `Infrastructure`, `Api`) y **`frontend/src/`**: sin cambios. Es infraestructura, scripts y CI; cambia `frontend/nginx/` (RF-28).
- **Compose:** ningún servicio nuevo. La copia, el monitoreo y el despliegue corren en el **host**, igual que Caddy en ADR-0018. Son los componentes que admite el nuevo §8.7 de la constitución (ADR-0019, 3.0.0): se enumeran en ADR-0018, ADR-0020 y esta spec, y `security` los revisa.
- **Compose de producción (RF-15):** una definición adicional (por ejemplo `docker-compose.prod.yml`, ya cubierta por el glob `docker-compose*.yml`) que sustituye `build:` por `image:` con el registro y el *digest*. `docker-compose.override.yml` no sirve: está en `.gitignore` y `.claude/settings.json`.
- **Variables nuevas** (registro, versión): `.env.example` y README, sin valores reales (§4.3).
- **Nginx y los paquetes del agente:** la plantilla usa `disable_symlinks on` en `/downloads/agent/`; `latest` es un directorio, no un enlace (RF-19).

### 10.2 Decisiones que requieren ADR

- **ADR-0018** fija el proxy TLS del host. **Aceptado el 2026-10-25** (`Accepted`); su revisión de `security` se hizo el mismo día, con cambios incorporados aquí (§19.5).
- **ADR-0019** es la enmienda de §8 (constitución 3.0.0): admite los componentes del host fuera de Compose (§8.7). **Aceptado el 2026-10-26** tras una revisión independiente de `architecture` y `security`.
- **ADR-0020**: despliegue continuo — GHCR, imágenes por *digest*, `deploy.sh` ejecutado por una persona sin acceso del CI al servidor, credencial de solo lectura del servidor, publicación de los paquetes del agente como artefacto y política de vuelta atrás (Q-13).
  Es transversal (§2.5) y define una superficie de ataque nueva (§14). **Aceptado el 2026-10-26**; la revisión independiente de `security` se hizo sobre el ADR y el código (§19.7) y el ADR se actualizó con sus resultados y con la visibilidad pública del repositorio.
- Las copias, el endurecimiento y el monitoreo **no** requieren ADR propio: se concretan aquí.

### 10.3 Alternativas y decisión

| Decisión | Alternativas | Lectura de `architecture` | Decidido (2026-10-25) |
|---|---|---|---|
| Canal CI → servidor (Q-11) | a) CI por SSH con usuario `deploy` y comando forzado · b) el servidor consulta e instala solo · c) *runner* autoalojado · d) una persona ejecuta el script | a) exige abrir SSH a los rangos de GitHub; c) ejecuta código de *workflows* en producción: no recomendado; b) guarda un token en el host; d) es lo más simple y no añade superficie. | **d)** |
| Registro (Q-9) | GHCR · registro de la nube de la organización · construir en el servidor | GHCR no añade servicio; con paquetes privados el servidor necesita una credencial de solo lectura, con públicos (el repositorio lo es, §19.7) ninguna. | **GHCR** |
| Copia externa (Q-1) | S3 en otra cuenta · S3 en la misma cuenta con Object Lock · Azure Blob · SharePoint · SFTP · proveedor externo | Criterios de `security`: cifrado en cliente, credencial que solo permita **añadir**, retención del lado del destino, cuenta distinta. | **S3 en otra cuenta de AWS** |
| Sonda y alertas (Q-15) | servicio externo gratuito · *workflow* de GitHub Actions · servicio propio | El servicio externo vigila también al propio servidor; el host envía latidos en vez de correo (P-17). | **Servicio externo gratuito, correo a una lista** |
| Endurecimiento (Q-8) | guía · script idempotente · Ansible | Script idempotente: verificable y sin herramienta nueva. | **Script `sh` idempotente** |
| Paquetes del agente (Q-20) | artefacto en GHCR · adjuntos de una release · copia manual desde el CI | GHCR reutiliza el registro (y la credencial de lectura, si es privado) que `deploy.sh` ya usa. | **Artefacto OCI en GHCR** |

### 10.4 Conformidad con la constitución y documentos vigentes

| Regla | Cómo se cumple |
|---|---|
| §4.1, §4.5, §8.6 | Sin secretos en el repositorio ni en la línea de comandos; secretos de CI en GitHub; el registro se autentica con `--password-stdin`; el destino de las copias, con un rol de instancia mejor que con claves estáticas (P-25). |
| §8.1 | `docker compose up --build` local sin cambios (RNF-7); en producción la aplicación corre como servicios de Compose (`docker-compose.yml` más `docker-compose.prod.yml`). |
| §8.7 enumeración | Los componentes del host fuera de Compose son el proxy TLS (ADR-0018), las copias, el despliegue, el monitoreo, el endurecimiento y los recursos de nube de §10.6. Están enumerados en ADR-0018, ADR-0020 y esta spec, y `security` los revisa. |
| §8.7 (a) | `deploy/**`, `scripts/**` y `.github/workflows/**` versionados; `harden-host.sh check` e `install-host-tasks.sh check` comparan lo instalado con lo versionado (RF-30, CA-28). |
| §8.7 (b) | Puertos hacia fuera: 22, 80 y 443 (RF-7). Con Docker solo `127.0.0.1:${FRONTEND_PORT}`, que `deploy.sh` verifica (CA-13); el frontend se publica solo en loopback mientras Caddy lo preceda. |
| §8.7 (c) | Mínimo privilegio: `backup-db.sh`, `check-host.sh`, `harden-host.sh` e `install-host-tasks.sh` corren como `root` porque leen Docker, PostgreSQL y la configuración del sistema e instalan paquetes; `deploy.sh` también lo ejecuta `root`, con `sudo` desde la cuenta de la persona administradora, y **nadie va en el grupo `docker`** (equivale a `root`). El clon `/opt/itmas3`, `host.conf`, el estado de los despliegues y la credencial de `docker login` son de `root`, porque los temporizadores ejecutan como `root` los scripts del clon. Los riesgos residuales conocidos son R-1 a R-5 y R-7 (§14.4), aceptados. |
| §8.7 (d) | RF-27 y RF-31: administración de Caddy fuera de TCP, parches con cadencia y aviso, claves restringidas y sandbox de systemd. |
| §8.7 (e) | Credenciales de mínimo alcance: lectura de GHCR solo si los paquetes son privados, rol de instancia que solo añade objetos y URL de latido; ninguna credencial de CI con ejecución en el servidor y ninguna *deploy key* (el repositorio es público, el clon es por HTTPS) (RNF-2, ADR-0020). |
| §8.7 (f) | Copias cifradas con `age` antes de salir, clave privada fuera del servidor y restauración probada (RF-2, RF-5, CA-4). |
| §8.4 | RF-16 y RN-5 verifican cada despliegue. |
| §3.8 | El CI no tiene credencial hacia el servidor (Q-11). Quien ejecuta `deploy.sh` usa `sudo`, no el grupo `docker`, que equivale a `root` (DEVELOPMENT.md §12.3): solo la persona administradora. |
| §9.2, §9.4 | Cada CA con prueba o verificación documentada; ninguna prueba automatizada depende de Internet: las de los scripts usan sustitutos de `docker` y del destino, como `test-commit-msg-hook.sh`. |
| §10.1, §10.4 | Documentación en el mismo cambio. Manual de uso: «Ninguno» (§17). |
| §11.5 | Matriz de agentes ampliada (§10.5). |
| §12.5 | Herramientas nuevas (cifrado de copias, `fail2ban`, escáner de imágenes, acciones de GitHub, la herramienta de copia al destino, el servicio de sonda) justificadas en la implementación con su mantenimiento y licencia (tabla siguiente); son paquetes del sistema, acciones de CI o servicios externos, no dependencias de `backend/` ni de `frontend/`. |

Herramientas nuevas (versiones y licencias comprobadas el 2026-10-26; el motivo es el de la decisión indicada):

| Herramienta | Uso | Licencia | Mantenimiento y versión | Decisión |
|---|---|---|---|---|
| `age` | Cifrado asimétrico de las copias | BSD-3-Clause | Activo (v1.3.2 en el origen; en el servidor, la del paquete de Ubuntu) | 2026-10-26 |
| `rclone` | Copia al bucket con el rol de instancia | MIT | Activo (v1.75.2 comprobada; `install-host-tasks.sh check` verifica las opciones que se usan) | 2026-10-26 |
| Caddy | Proxy TLS del host | Apache-2.0 | Activo (v2.11.7); repositorio oficial con huella comprobada | ADR-0018 |
| Trivy (`trivy-action`) | Escaneo de las imágenes antes de publicar | Apache-2.0 | Activo (acción v0.36.0 fijada por SHA) | 2026-10-26 |
| `fail2ban` | Protección de `sshd` | GPL (paquete de Ubuntu, sin enlazar con nada del proyecto) | Activo | Q-7 |
| ShellCheck | Análisis estático de los scripts en CI | GPL-3.0 (herramienta de CI, no se distribuye) | Activo; está en la imagen del *runner* | 2026-10-27 |
| Acciones de GitHub (`checkout`, `setup-node`, `setup-dotnet`, `upload-artifact`, `download-artifact`, `docker/login-action`, `setup-buildx-action`, `build-push-action`) | CI/CD | MIT o Apache-2.0 | Activas; fijadas por SHA de commit y actualizadas con Dependabot | 2026-10-26 |

### 10.5 Matriz de agentes propuesta (para `agents/README.md`; se aplica al implementar)

Hoy `.github/` no figura en ninguna fila ni en ningún `owns`: sin una fila, ningún agente puede modificar los *workflows*. P-23 se confirmó: se crea `deploy/` para la configuración del host.

| Ruta | Agente propietario | Revisión obligatoria |
|---|---|---|
| `.github/workflows/**` (CI/CD) — **fila nueva** | architecture | security (permisos del token, secretos de entornos, acciones de terceros, publicación en GHCR); testing (las suites que invoca); device-agent (si publica los paquetes del agente) |
| `deploy/**` (configuración del host: Caddyfile, `daemon.json`, `sshd`, `fail2ban`, `unattended-upgrades`, unidades de systemd o cron) — **fila nueva** | architecture | security (se ejecuta como `root` en el servidor y define su exposición) |
| `scripts/**` (despliegue, copias, restauración, endurecimiento, comprobaciones) — **fila existente**, se amplía el motivo | architecture | security (corren como `root`, también `deploy.sh` con `sudo`); testing (pruebas de los scripts); database (copia y restauración) |
| `docker-compose*.yml` (incluido el de producción) — **fila existente** | architecture | security |
| `frontend/nginx/**` (plantilla de nginx, RF-28) — **fila existente** | frontend | security (CSP, cabeceras, proxy); testing (prueba repetible de la plantilla) |
| `scripts/build-agent.sh`, `agent/Dockerfile` si el CI los modifica — **existentes** | device-agent (con architecture) | security |
| `SECURITY.md` (desviaciones de PostgreSQL y del `.env`, checklist) — **existente** | security | architecture |

Además: `agents/architecture.md` añade `.github/workflows/**` y `deploy/**` a `owns`; `agents/security.md` añade a `triggers` «workflows de CI/CD, publicación de imágenes, copias de seguridad y exposición del host»; `agents/database.md` añade a `triggers` «restauración de copias». Los nombres `deploy/` y `.github/workflows/` los fija el plan. **Aplicado el 2026-10-26** en el mismo cambio que la enmienda (ADR-0019, Seguimiento 0), salvo la fila de `frontend/nginx/**`, que ya existía.

### 10.6 Recursos de nube de los que dependen los controles (constitución §8.7)

Los crea quien administra AWS (C-1), no esta spec: la spec los enumera y verifica. Las plantillas de sus políticas, con marcadores en lugar de identificadores de cuenta, están en `deploy/aws/` (§8.7 a).

| Recurso | Propiedad exigida | Se verifica en |
|---|---|---|
| Instancia EC2 | Ubuntu 24.04 LTS; IMDSv2 obligatorio con *hop limit* 1; rol de instancia con permiso de **solo añadir** objetos (`s3:PutObject`) al prefijo de copias del bucket de destino | CA-27, CA-2 |
| Volumen EBS | Cifrado | CA-19 |
| *Security group* | Entrada solo 22/tcp, 80/tcp y 443/tcp | CA-5 |
| Bucket de destino (otra cuenta) | Versionado y *Object Lock*; política que permite `PutObject` solo al rol de instancia y no concede al servidor leer ni borrar; vencimiento a los 30 días por regla de ciclo de vida | CA-1, CA-2 |

## 11. Cambios de frontend

Sin cambios en `frontend/src/`. **Cambia la plantilla de nginx** (`frontend/nginx/default.conf.template`, RF-28): `real_ip_recursive` explícito, `X-Forwarded-Proto` acotado por origen y `set_real_ip_from` acotado a la puerta de enlace y parametrizado; con revisión de `security` y la prueba repetible de la plantilla.

## 12. Cambios de backend

Ninguno: sin endpoints, contratos ni casos de uso nuevos. Un *endpoint* de salud accesible desde fuera que compruebe PostgreSQL sería otra spec (hoy `/health*` devuelve `404` desde fuera, a propósito, SECURITY.md §4); Q-16 se resuelve con una comprobación en el host. Normalizar a /64 la partición del límite de frecuencia por IP también sería un cambio de API (spec aparte): RF-29 lo evita sin registro `AAAA`.

## 13. Cambios de base de datos

Ninguno en el esquema, las migraciones, los roles ni la configuración de PostgreSQL (Q-17 aceptó `SslMode=Disable`). La restauración usa el procedimiento de `database/init` sobre un volumen vacío.

## 14. Consideraciones de seguridad

### 14.1 Amenazas (STRIDE)

| Amenaza | Mitigación |
|---|---|
| **S/E** Una cuenta de GitHub o del CI comprometida publica una imagen maliciosa | No hay credencial de GitHub con acceso al servidor (Q-11): publicar no despliega. Aprobación manual del entorno `production`, protección de `main` y de las etiquetas `v*`, secretos solo en ese entorno y `permissions` mínimos. Con una sola persona, la aprobación es una pausa deliberada, no una separación de funciones (R-2). Quien despliega ve la etiqueta y el *digest* antes de ejecutar `deploy.sh`. |
| **T** Cadena de suministro: una acción de terceros, imagen base o paquete comprometidos que llegan a producción | Acciones y herramientas fijadas a versión inmutable; imágenes desplegadas por *digest*; suite, auditoría de dependencias y escaneo de imágenes antes de publicar (Q-10); revisión de `security` en cada herramienta nueva (§12.5). |
| **T** Imagen manipulada en el registro tras publicarse | El servidor despliega por el *digest* registrado en el CI (RF-14), no por etiqueta mutable. |
| **I** Los volcados contienen la auditoría, el mapa de cuentas privilegiadas, datos del inventario y el *key ring* sin cifrar | Cifrado asimétrico antes de salir; la clave que descifra fuera del servidor, con una copia sellada fuera de línea (RF-6); destino en otra cuenta con acceso restringido; el volcado local en un directorio `700` de `root`; RN-6. |
| **I** El `.env` o un secreto aparecen en la salida de un script o de CI | `docker compose config -q` (sin `-q` imprime los valores); los scripts no imprimen variables; secretos enmascarados en CI; búsqueda de secretos en CA-14. |
| **I** Credencial de solo lectura de GHCR guardada en el servidor (solo si los paquetes son privados) | Con paquetes públicos no existe. Si existe: alcance de solo lectura de paquetes de una cuenta de servicio, archivo de `root` con permisos `600`, introducida con `--password-stdin` y rotada; no da acceso al código. |
| **I/D** Repositorio público: cualquiera lee la topología y los riesgos aceptados, y abre PR cuyos *workflows* consumen *runners* | Nada confidencial en el repositorio ni en las imágenes (§4 de la constitución; sin identificadores de AWS, direcciones ni claves); *workflows* de PR con token de solo lectura y sin secretos, y aprobación obligatoria de los de colaboradores externos; reporte privado de vulnerabilidades (R-7). |
| **T/S** Una etiqueta movida o un *commit* que no está en `main` se despliega | `deploy.sh` trae las etiquetas sin `--force`, exige que el *commit* esté en `origin/main`, muestra *commit* y *digests* y pide confirmación antes de tocar nada; etiquetas de imagen inmutables en la publicación. |
| **I** La URL de latido hacia el servicio externo es un secreto (quien la conozca puede fingir que el servidor vive) | Archivo de `root` con permisos `600` en el host; nunca en el repositorio, en `.env.example` ni en logs. |
| **T/R** Un servidor comprometido borra o cifra sus propias copias (extorsión) | La credencial del destino solo permite añadir y el destino impide el borrado dentro de la retención; la cuenta de destino es distinta de la del servidor (Q-1); preferir un rol de instancia a claves estáticas (P-25); RN-4. |
| **D** Disco lleno (logs, volcados, imágenes acumuladas por despliegue) | Rotación de logs (RF-11), retención local (RF-3), limpieza de imágenes antiguas (RF-16), alerta de disco (RF-21). |
| **D** Fuerza bruta contra SSH | Solo clave, sin `root`, `fail2ban` y actualizaciones de seguridad automáticas. **SSH está abierto a Internet por decisión del solicitante** (Q-6, R-1). |
| **D** El certificado no se renueva (Let's Encrypt) y el sitio deja de ser accesible | Alerta de vencimiento a menos de 14 días (RF-22), que salta cuando la renovación automática de Caddy ya ha fallado. HSTS escalonado (RF-27): un HSTS largo con una renovación fallida bloquearía el sitio hasta un año. |
| **E** El grupo `docker` equivale a `root`, y un clon o una configuración que otro usuario pueda escribir se ejecutaría como `root` desde los temporizadores | Nadie va en el grupo `docker`: todo se ejecuta con `sudo` como `root`; el clon, `host.conf`, el estado y la credencial de `docker login` son de `root`, y los scripts se niegan a leer `host.conf` si otro usuario puede escribirlo o no es del usuario que ejecuta. El CI no tiene acceso al servidor. |
| **R** No saber quién desplegó qué | Registro de despliegues (RF-16) y la historia de ejecuciones de Actions con la aprobación del entorno. |
| **I** Escucha en la red entre `api` y `postgres` (`SslMode=Disable`) | Red `data` con `internal: true`, sin puertos publicados y solo en el host; desviación aceptada en Q-17 (R-3). |
| **I** Los volcados siguen conteniendo datos de personas ya eliminadas hasta que vence la retención | La retención de 30 días del destino es el plazo máximo; se documenta. |
| **S** Un proceso local del servidor (o un contenedor de `edge`) fija su IP y el esquema con `X-Forwarded-*` y evade los límites por IP | Modelo de confianza documentado (ADR-0018, decisión 3: el loopback del host es de confianza total; quien ya ejecuta código en el servidor puede más); `real_ip_recursive off`, `X-Forwarded-Proto` acotado por origen y `set_real_ip_from` a la puerta de enlace (RF-28, CA-22). |
| **S** Un fallo de Let's Encrypt emite el certificado con otra CA (ZeroSSL) sin aviso | Emisor fijado a Let's Encrypt y correo de contacto (RF-27). |
| **E** La administración de Caddy en `localhost:2019` sin autenticación y un Caddy sin parcheo automático, único proceso expuesto a Internet | `admin off` (RF-27); actualización periódica con aviso; huella de la clave GPG; `/var/lib/caddy` solo para su usuario. |
| **D** Todos los clientes comparten la IP de la puerta de enlace y un anónimo agota los límites de `/auth/login` y anónimos para todos; con `AAAA`, un /64 rota direcciones | Comprobar el origen en Linux antes de abrir el DNS (CA-22); sin registro `AAAA` (RF-29). |
| **I/E** Una SSRF o un RCE en la API alcanza las credenciales del rol de instancia por IMDS desde la red `edge`, que tiene salida | IMDSv2 obligatorio con *hop limit* 1 o bloqueo de `169.254.169.254` (RF-29, P-25). |

### 14.2 Datos personales y sensibles

Los volcados incluyen cuentas de usuario de ITMas (nombre y usuario de Entra ID) y datos de inventario de equipos. No hay datos nuevos. Las copias se tratan como sensibles (RN-6).
La documentación y el manual son públicos: no llevan IPs, nombres de equipo, identificadores de AWS ni detalles internos del servidor (constitución §10.4).

### 14.3 Revisión de `security`

Obligatoria para: ADR-0018 (aceptación) y ADR-0020, el destino y el cifrado de las copias, la credencial de lectura de GHCR y los permisos de los *workflows*, el endurecimiento, las herramientas nuevas, la decisión sobre PostgreSQL (Q-17) y la actualización de SECURITY.md. La aceptación de riesgos residuales la hace el solicitante (`agents/security.md`).

### 14.4 Riesgos residuales (decididos el 2026-10-25 y recogidos en la aprobación, §19.4)

| ID | Riesgo | Decisión |
|---|---|---|
| R-1 | SSH abierto a Internet (SECURITY.md §8 pide «22 restringido») | Aceptado con solo clave, sin `root` y `fail2ban` (Q-6). |
| R-2 | Una sola persona administradora: el RTO de un día hábil, la custodia de la clave y la aprobación del entorno dependen de ella; no hay separación de funciones | Aceptado; se mitiga con la copia sellada de la clave y del `.env` (Q-5) para que otra persona autorizada pueda restaurar. |
| R-3 | `Database__SslMode=Disable` (checklist pide `VerifyFull`) | Aceptado: red `data` interna de un solo host y copias cifradas (Q-17, P-20). |
| R-4 | Secretos en un `.env` `600` del host, visible con `docker inspect` para quien controle el daemon (checklist pide un gestor de secretos) | Aceptado y registrado en SECURITY.md (Q-18); el resto de las desviaciones del checklist queda en sus specs. |
| R-5 | El loopback del host es de confianza total para `X-Forwarded-*` (un proceso local puede fijar su IP y el esquema) | Aceptado expresamente (§8.7 c) al aceptar ADR-0018 (2026-10-25), con el endurecimiento de RF-28; consta en ADR-0018 (decisión 3). |
| R-6 | La lectura de §8.1 (Caddy fuera de Compose) sin enmienda formal de la constitución | **Cerrado el 2026-10-26:** enmienda de §8 en ADR-0019 (`Accepted`, constitución 3.0.0). |
| R-7 | El repositorio es público: la topología (un servidor, SSH con clave y `fail2ban`) y los riesgos aceptados son legibles por cualquiera | Aceptado por el solicitante (2026-10-27, «público a propósito»); la seguridad no depende de ocultarlos; sin identificadores ni secretos, reporte privado de vulnerabilidades y aprobación de los *workflows* externos. |

## 15. Pruebas

| Criterio | Tipo | Prueba |
|---|---|---|
| CA-1, CA-2, CA-3 | Pruebas de scripts (unitarias, con sustitutos de `docker` y del destino) + verificación documentada en el servidor | `scripts/test-*.sh` del volcado, el cifrado, la retención y el aviso (patrón de `test-commit-msg-hook.sh`); resultado en §18. |
| CA-4 | Verificación documentada | Restauración real según el procedimiento escrito, fuera del servidor; resultado en §18. |
| CA-5 a CA-9 | Verificación documentada | Comandos de verificación de RF-12 ejecutados en el servidor; resultado en §18. |
| CA-10, CA-14 | Verificación documentada | Ejecuciones de los *workflows* (una con fallo provocado, una con etiqueta); revisión de `permissions` y búsqueda de secretos. |
| CA-11, CA-12, CA-13 | Pruebas de scripts + verificación documentada | Prueba del script de despliegue con sustitutos (éxito, fallo antes y después del `migrator`, puerto no permitido); ensayo real de la vuelta atrás en el servidor antes del DNS. |
| CA-15 | Pruebas de scripts + verificación documentada | Sustitutos para la regla de no sobrescribir y el cambio de `latest`; descarga de `/downloads/agent/` y comprobación de sumas. |
| CA-16, CA-17, CA-18 | Verificación documentada | Fallo y umbrales simulados; resultado en §18. |
| CA-19, CA-21 | Revisión | `security` sobre SECURITY.md; descripción del volumen EBS; `docker compose config -q` y `docker compose up --build` locales. |
| CA-20 | Verificación documentada | DEVELOPMENT.md §12.7 (paso 5) tras el primer despliegue. |
| CA-22 a CA-27 | Prueba de script + verificación documentada | `scripts/test-nginx-realip.sh` para CA-25; las comprobaciones de DEVELOPMENT.md §12.7 (pasos 5 a 7) y de ADR-0018 «Seguimiento» en el servidor para el resto; resultado en §18. |

## 16. Plan de implementación

### 16.1 Tareas

| # | Tarea | Agente | Archivos |
|---|---|---|---|
| 1 | **Hecho (2026-10-26):** ADR-0018, ADR-0019 (constitución 3.0.0) y ADR-0020 aceptados | architecture (security) | `specs/architecture/**`, `constitution.md`, `agents/**` |
| 2 | Script de endurecimiento con modo de comprobación y configuración del host (RF-7 a RF-12, RF-30) | architecture (security) | `scripts/**`, `deploy/**` |
| 3 | Compose de producción por imagen, variables y health check de `api` con un `Host` permitido (RF-15, RF-26) | architecture (security) | `docker-compose*.yml`, `.env.example`, README |
| 4 | CI: suites, auditoría, escaneo y construcción/publicación de imágenes en GHCR con aprobación del entorno (RF-13, RF-14, RF-18) | architecture (security, testing) | `.github/workflows/**` |
| 5 | `deploy.sh` con vuelta atrás y su prueba (RF-16, RF-17) | architecture (security, testing) | `scripts/**` |
| 6 | Copias: volcado, cifrado, copia al bucket, retención local, verificación, restauración y su prueba (RF-1 a RF-6); plantillas de las políticas de AWS (§10.6) | architecture (security, database, testing) | `scripts/**`, `deploy/**` |
| 7 | Monitoreo: comprobación del host, latidos, disco, certificado, `reboot-required`, Caddy y parches (RF-20 a RF-23, RF-31) | architecture (security) | `deploy/**`, `scripts/**` |
| 8 | Publicación de los paquetes del agente y su descarga por `deploy.sh` (RF-19) | device-agent (architecture, security) | `scripts/build-agent.sh`, `.github/workflows/**`, `scripts/**` |
| 9 | Desviaciones de PostgreSQL y del `.env` en SECURITY.md (RF-24) | security (architecture) | `SECURITY.md` |
| 10 | Documentación y matriz de agentes (RF-25, §10.5) | architecture | `DEVELOPMENT.md`, `README.md`, `ARCHITECTURE.md`, `AGENTS.md`, `agents/**`, `specs/**` |
| 11 | Configuración de Caddy y requisitos del host: emisor, HSTS escalonado, sin `Via`, sin HTTP/3, administración fuera de TCP, Docker mínimo, sin `AAAA`, IMDS (RF-27, RF-29) | architecture (security) | `deploy/**`, `scripts/**`, `DEVELOPMENT.md` |
| 12 | Plantilla de nginx: `real_ip_recursive`, esquema por origen, `set_real_ip_from` acotado y parametrizado, `gateway` de `edge`, y su prueba repetible (RF-28) | frontend (security, testing, architecture) | `frontend/nginx/**`, `docker-compose*.yml`, `scripts/**` |
| 13 | Ensayo en el servidor antes del DNS, primer despliegue y verificación de los CA | persona administradora + security + testing | §18 de esta spec |

**Estado (2026-10-27):** las tareas 1 a 12 están **implementadas en el árbol de trabajo, sin commit**, con las correcciones de las revisiones (§19.7); la 13 depende de GitHub, AWS y el servidor real y sigue pendiente.

### 16.2 Dependencias y compuertas

- **Cumplidas el 2026-10-26:** esta spec `Approved` (2026-10-25), ADR-0018 `Accepted` con su revisión de `security`, ADR-0019 `Accepted` (constitución 3.0.0) y ADR-0020 `Accepted`. La spec está `In Progress`.
- Los datos operativos de §19.3 (cuenta y bucket de destino, lista de distribución, servicio de sonda, correo de Let's Encrypt) **no se escriben en el repositorio** y hacen falta para **configurar el servidor** (tareas 6, 7 y 13), no para escribir los scripts: estos leen un archivo `/etc/itmas3/host.conf` y el repositorio solo lleva una plantilla con marcadores.
- La tarea 12 antes de abrir el DNS: el origen que ve nginx se comprueba en Linux (CA-22) antes de estrechar `set_real_ip_from`. La tarea 3 incluye el arreglo del health check que, sin él, impide que `frontend` arranque (RF-26).
- Las tareas 4 y 5 antes que la 8 (los paquetes usan el mismo registro y credencial). La 6 (restauración probada) antes del primer despliegue real (RN-1).
- Primera vez que se ejecutan los comandos de §12: tras ellos se corrige DEVELOPMENT.md con lo que difiera.

## 17. Manual de uso

| Proceso (`id`) | Acción | Permiso | Pantallas (`routes`) |
|---|---|---|---|
| — | **Ninguno.** Nada cambia para las personas usuarias. El manual es público (ADR-0013): no debe describir el servidor, las copias ni el despliegue. | — | — |

## 18. Trazabilidad (al implementar)

- Commits/PRs: **sin commit todavía** (decisión del solicitante). Mensaje propuesto, con los IDs que exige el hook: `SPEC-0009, ADR-0018, ADR-0019, ADR-0020: production on a single Ubuntu server (hardening, backups, continuous deployment, monitoring) and §8.7 amendment`.
- Archivos principales:
  - **Constitución y decisiones:** `constitution.md` (3.0.0), `specs/architecture/adr-0018-*.md` (revisión), `adr-0019-*.md`, `adr-0020-*.md`, `agents/README.md`, `agents/architecture.md`, `agents/security.md`, `agents/database.md`.
  - **CI/CD:** `.github/workflows/ci.yml`, `.github/workflows/release.yml`, `.github/dependabot.yml`, `docker-compose.prod.yml`.
  - **Compose y nginx:** `docker-compose.yml` (health check de `api` con `Host` permitido, `gateway` de `edge`, `EDGE_TRUSTED_PROXY_FROM`), `frontend/nginx/default.conf.template`, `.env.example`.
  - **Scripts:** `scripts/deploy.sh`, `backup-db.sh`, `restore-db.sh`, `harden-host.sh`, `install-host-tasks.sh`, `check-host.sh`, `scripts/lib/itmas-host.sh`.
  - **Configuración del host:** `deploy/**` (Caddy, SSH, fail2ban, actualizaciones, `daemon.json`, unidades de systemd, `host.conf.example`, plantillas y verificaciones de AWS en `deploy/aws/`).
  - **Pruebas de los scripts:** `scripts/test-deploy.sh`, `test-backup.sh`, `test-host-tasks.sh`, `test-nginx-realip.sh`; las comprobaciones de privilegios de la restauración viven en `scripts/lib/restore-assertions.sql`.
  - **Documentación:** `DEVELOPMENT.md` §12 (reescrita), `README.md`, `ARCHITECTURE.md`, `SECURITY.md` (§5, §7, §8 y riesgos residuales), `AGENTS.md`, `specs/README.md`, `specs/features/README.md`, y los textos inexactos de `adr-0011` y `backend/src/Itmas3.Api/Security/ConnectionIp.cs` (solo un comentario).
- Pruebas (2026-10-27, en el equipo de trabajo, con Docker): `./scripts/test-nginx-realip.sh` 10 de 10 (y fallan 2 con la plantilla antigua del esquema); `./scripts/test-deploy.sh` 172 de 172; `./scripts/test-backup.sh` 132 de 132, que incluyen **el `backup-db.sh` y el `restore-db.sh` reales de extremo a extremo**
  sobre PostgreSQL 18 (volcado, lectura completa, recuentos desde el volcado, restauración en una transacción con los roles de `database/init`, recuentos que difieren, volcado truncado y ACL con más de DML; solo `age` —paso directo si no está instalado—, `rclone` y `curl` son sustitutos);
  `./scripts/test-host-tasks.sh` 164 de 164; `./scripts/test-commit-msg-hook.sh` 54 de 54; ShellCheck (`--shell=sh --severity=warning`, versiones 0.9.0, la del *runner*, y la última) sin avisos sobre `scripts/*.sh` y `scripts/lib/*.sh`; `docker compose config -q` con y sin `docker-compose.prod.yml`.
  Cubren CA-2, CA-3, CA-11 a CA-13, CA-15 (la parte con sustitutos), CA-25 y CA-28 y el comportamiento de los controles de CA-5 a CA-9, CA-16 a CA-18 y CA-29. El `Caddyfile` se validó con Caddy 2.11 en un contenedor y una sola cabecera HSTS salió en la respuesta.
- **No verificado (necesita GitHub, AWS o el servidor real):** CA-1, CA-2 (credencial solo de añadir y `rclone` contra S3), CA-4 (la parte de la API y del `migrator` con las imágenes reales), CA-5 a CA-10, CA-14, CA-15 (publicación en GHCR), CA-16 a CA-27 en el servidor y CA-29 (huella de Caddy). Los *workflows* no se han ejecutado en GitHub (conviene un ensayo en un repositorio de prueba antes de la primera etiqueta real),
  y las opciones de `ProtectSystem` de las unidades de systemd y el comportamiento de `rclone` con `--s3-no-head` contra un bucket con *Object Lock* quedan por confirmar en el servidor. La instancia debe ser `x86_64`: el CI construye `linux/amd64`.
- **Declarado de más:** la spec no afirma que `deploy.sh` haya corrido contra GHCR ni contra un Docker real: sus pruebas usan sustitutos de `docker`, `git` y `curl`.
- Cambios en archivos de otros roles, pendientes de su revisión: `SECURITY.md` (security), `backend/src/Itmas3.Api/Security/ConnectionIp.cs` (solo un comentario; backend) y `adr-0011` (architecture).
- Revisiones: `architecture`, `security` y `database` (2026-10-26) y `testing` (2026-10-27) revisaron la implementación de forma independiente y sus hallazgos están aplicados (§19.7). Quedan `frontend` (plantilla de nginx) y `device-agent` (paquetes del agente).
- Manual de uso actualizado: no aplica.

## 19. Decisiones y preguntas abiertas

### 19.1 Respuestas del solicitante (2026-10-25)

| Q | Decisión | Efecto en la spec |
|---|---|---|
| Q-1 | Almacenamiento de objetos: **S3 en otra cuenta de AWS**, con credencial solo de añadir y retención/Object Lock en el destino. | RF-2, RF-3, RN-4, §10.3, §14.1. |
| Q-2 | Volcado **diario**; RTO de un día hábil; sin WAL. | RF-1, RNF-5. |
| Q-3 | **7 días** en el servidor y **30 días** en el destino. | RF-3, CA-2. |
| Q-4 | Prueba de restauración **antes de publicar y cada trimestre**, fuera de producción. | RF-5. |
| Q-5 | **Una sola persona** administra y custodia; **copia sellada** de la clave y del `.env` fuera de línea. | RF-6, R-2. |
| Q-6 | SSH **abierto**, solo clave y `fail2ban`. | RF-7, R-1. |
| Q-7 | Solo seguridad, **sin reinicio automático**, con aviso de `reboot-required`. | RF-10, RF-23. |
| Q-8 | **Script `sh` idempotente**; servidor en **AWS EC2** con **Ubuntu 24.04 LTS**. | RF-12, §2. |
| Q-9 | **GitHub Container Registry**. | RF-14. |
| Q-10 | `npm audit`, NuGet Audit y **escaneo de imágenes**, bloqueando alta/crítica con corrección. | RF-13. |
| Q-11 | **Una persona ejecuta `deploy.sh`** por SSH; el CI no accede al servidor. | RF-16, RF-18, RNF-2, §14.1. |
| Q-12 | Etiqueta **`vX.Y.Z`**, entorno `production` con **aprobación manual**, protección de `main` y de las etiquetas. | RF-14, RF-18, RN-2. |
| Q-13 | **Revertir solo antes del `migrator`**; después, avisar y decidir a mano. | RF-17, CA-12. |
| Q-14 | Paquetes del agente **solo si cambia la versión**; **inmutables**; `latest` al final. | RF-19, RN-7, CA-15. |
| Q-15 | **Servicio externo gratuito**; **correo a una lista**; sonda cada **5 min**, **2 fallos** seguidos; disco **80 %**; certificado **14 días**. | RF-20 a RF-23. |
| Q-16 | **Comprobación en el host** del estado `healthy` de los contenedores. | RF-23. |
| Q-17 | **A:** aceptar `SslMode=Disable`; volumen **EBS cifrado**. | RF-24, R-3, CA-19. |
| Q-18 | **Solo se registra** la desviación del `.env` en el host. | RF-24, R-4. |
| Q-19 | Ensayo **en el propio servidor, antes del DNS**. | RF-16 (7), §8, CA. |
| Q-20 (surge de Q-11) | Paquetes del agente como **artefacto en GHCR**, descargado por `deploy.sh`. | RF-19, §10.3. |
| P-23 | Carpeta **`deploy/`** para la configuración del host. | §10.5. |

### 19.2 Propuestas

Aceptadas tal como se redactaron: **P-1, P-2, P-3, P-4, P-5, P-8, P-9, P-10, P-11, P-12, P-13, P-14, P-16, P-17, P-18, P-19, P-21, P-22, P-23.**
Con matiz: **P-15** (`latest` se cambia con un renombrado de directorio, no un enlace: nginx usa `disable_symlinks on`) y **P-20** (se añade el cifrado del volumen EBS). **Modificadas:** **P-6** (custodia: una persona con copia sellada, no dos personas con acceso) y **P-7** (SSH abierto, no restringido).

### 19.3 Propuestas nuevas y datos pendientes

Propuestas nuevas que surgieron de las respuestas, **confirmadas por el solicitante al aprobar** (§19.4):

- **P-24** El *security group* de AWS abre solo 22, 80 y 443, igual que `ufw` (defensa en profundidad; con SSH abierto, es la segunda capa).
- **P-25** El servidor escribe en el bucket con un **rol de instancia de EC2** y una política entre cuentas que solo permita añadir objetos, en lugar de claves estáticas guardadas en el host (RNF-1, constitución §4). `security` lo confirma al redactar el plan.
- **P-26** Los paquetes del agente se publican en GHCR como una imagen mínima (`FROM scratch`) y `deploy.sh` los extrae con `docker`, sin una herramienta nueva (constitución §12.5); ADR-0020 fija el mecanismo definitivo.
- **C-1** La infraestructura de AWS (instancia, volumen EBS cifrado, *security group*, cuenta y bucket de destino) se crea **fuera** de esta spec; la spec solo verifica el resultado.

### 19.5 Revisión de `security` de ADR-0018 (2026-10-25)

Un revisor independiente revisó ADR-0018 contra el código y la documentación oficial (veredicto: **`Accepted` con cambios**, sin hallazgos críticos ni altos). Por instrucción del solicitante, **tras la aprobación de esta spec**,
se incorporaron sus correcciones al ADR, a DEVELOPMENT.md §12 y a esta spec; la enmienda de la constitución quedó para el solicitante y se resolvió el 2026-10-26 (§19.6).

| Hallazgo | Dónde se aplicó |
|---|---|
| F1 Confianza amplia en `X-Forwarded-*`, `real_ip_recursive` implícito | ADR-0018 decisión 3 (modelo de confianza real y endurecimiento acordado); RF-28, CA-22, CA-25, R-5, tarea 12 |
| **F2 Lectura de §8.1: exige enmienda de §8** | **Aplicada el 2026-10-26:** ADR-0019 (`Accepted`, constitución 3.0.0); R-6 cerrado; §10.4 y §10.6 |
| F3 Health check de `api` contra `AllowedHosts` | RF-26, RF-16 (7), CA-23, tarea 3; aviso en DEVELOPMENT.md §12 |
| F4 Comprobaciones débiles; paso 5 de §12.7 | ADR-0018 «Seguimiento»; DEVELOPMENT.md §12.7 (pasos 5 a 7); CA-20, CA-25 |
| F5 Emisor ACME (ZeroSSL de respaldo) | ADR-0018 decisión 1; RF-27, CA-24; DEVELOPMENT.md §12.6 |
| F6 HSTS de un año desde el primer día | ADR-0018 decisión 4 (escalonado); RF-27; DEVELOPMENT.md §12.6 |
| F7 Administración y parcheo de Caddy, clave TLS | ADR-0018 decisiones 4 y 6 y Consecuencias; RF-27; DEVELOPMENT.md §12.6 |
| F8 Límites compartidos y partición IPv6 | ADR-0018 Consecuencias y decisión 6; RF-29, CA-27 |
| F9 `Via` y HTTP/3 | ADR-0018 decisión 4; RF-27; DEVELOPMENT.md §12.6 |
| F10 Versión mínima de Docker | ADR-0018 decisión 6; RF-29, CA-27; DEVELOPMENT.md §12.3 (la versión concreta se fija al implementar) |
| F11 SSH abierto frente a «22 (restringido)» y otros textos | RF-24 registra la desviación de SSH. **Corregidos con la implementación (2026-10-26):** SECURITY.md, el comentario de `ConnectionIp.cs` y ADR-0011, que decían que el cliente no puede falsear la IP |
| IMDS, solape de la VPC | RF-29, CA-27, §14.1 |

Implementados el 2026-10-26 (§18): el arreglo del health check en `docker-compose.yml`, la plantilla de nginx (`frontend/nginx/`) y los archivos de `deploy/**`.

Datos operativos que se necesitan antes de las tareas 6 y 7 y que **no se escriben en el repositorio**: la cuenta de AWS de destino y quién la administra, el nombre del bucket y la región, la lista de distribución que recibirá los avisos, el buzón de contacto de Let's Encrypt, y el servicio externo concreto (debe admitir una sonda HTTP y latidos; se elige con `security` por licencia y mantenimiento, constitución §12.5).

### 19.4 Aprobación (2026-10-25)

jmartinez aprobó la spec **con las propuestas P-24, P-25 y P-26 y la exclusión C-1 de §19.3**. La aprobación recoge las decisiones de §19.1, entre ellas las que
originan los riesgos residuales R-1 a R-4 de §14.4. Las compuertas de §16.2 se cumplieron el 2026-10-26 (§19.6).

### 19.6 Aceptación de ADR-0019 y ADR-0020 e inicio de la implementación (2026-10-26)

- **ADR-0019 (enmienda de §8):** el solicitante eligió la versión **MAJOR 3.0.0** y el **texto consolidado** de la revisión independiente (reglas (a) a (f), sin la (g) opcional), y **aceptó** el ADR con ese texto. `constitution.md` pasó a 3.0.0 en el mismo cambio, con las filas de `agents/**` (ADR-0019, Seguimiento 0).
  Esas reglas añaden a esta spec RF-30, RF-31, CA-28, CA-29 y §10.6.
- **ADR-0020 (despliegue continuo):** el solicitante lo **aceptó**; la revisión independiente de `security` se hace al cerrar la spec, sobre el ADR y el código (como en ADR-0016 y ADR-0017).
- **Herramientas (constitución §12.5):** cifrado con **`age`**, subida con **`rclone`** (con el rol de instancia), escáner **Trivy** y scripts genéricos de sonda y latido (el servicio concreto se elige al desplegar).
- **Alcance:** el solicitante pidió **implementar toda SPEC-0009**; los textos inexactos de SECURITY.md, ADR-0011 y `ConnectionIp.cs` se corrigen **con la implementación** (tareas 9 y 10).
- **P-27 (nueva):** la cadencia máxima de parches de seguridad de RF-31 es de **7 días**. Propuesta por defecto, ajustable al revisar.

### 19.7 Revisiones de la implementación y repositorio público (2026-10-26 y 2026-10-27)

- **Decisiones del solicitante en la implementación:** versión MAJOR 3.0.0 de la constitución; implementar toda la spec; textos inexactos corregidos con la implementación; `age`, `rclone` y Trivy; scripts genéricos de sonda y latido (el servicio se elige al desplegar);
  paquetes del agente como artefacto OCI en GHCR; la sonda avisa tras **2 fallos seguidos** (unos 10 minutos).
- **Revisiones independientes de `architecture`, `security` y `database`** (solo lectura, 2026-10-26). Sus hallazgos están aplicados: el modelo de ejecución como `root` con `sudo` y sin grupo `docker` (el clon, `host.conf`, el estado y la credencial son de `root`); la detección de la instalación inicial por el volumen de datos y no por el contenedor;
  la comprobación de que el *commit* de la etiqueta está en `origin/main` y de que la etiqueta no se movió; la confirmación **antes** del `checkout`; el nivel del esquema en la vuelta atrás; que una versión no verificada ni el redespliegue de la misma etiqueta sustituyan a «la anterior»; el reintento de copias que no llegaron al destino (marcas `.uploaded`);
  la lectura completa del volcado, los recuentos desde el propio volcado y la restauración en una sola transacción con comprobación de privilegios; la publicación en dos fases (construir y escanear con token de solo lectura, publicar tras la aprobación con etiquetas inmutables); la cabecera HSTS única y las capacidades de Caddy reducidas;
  la comprobación continua de los puertos publicados y de los parches pendientes de Ubuntu, Caddy y Docker; locks con PID; y la validación de `host.conf` antes de leerlo.
- **Repositorio público a propósito (2026-10-27):** el solicitante confirmó que `solucionesjj/itmas` es público. Se ajustaron la documentación y ADR-0020 (§8): el clon por HTTPS sin *deploy key*; los paquetes de GHCR públicos (recomendado, con el servidor sin ninguna credencial de GitHub) o privados con un token de solo lectura;
  la aprobación obligatoria de los *workflows* de colaboradores externos; el reporte privado de vulnerabilidades; y el riesgo residual R-7. La visibilidad de los paquetes se comprueba tras la primera publicación (DEVELOPMENT.md §12.5).
- **Revisión independiente de `testing`** (2026-10-27, con 90 mutaciones sobre una copia): las suites detectaban los fallos gruesos pero dejaban pasar el debilitamiento de varios controles. Aplicado: un defecto real de `deploy.sh rollback` (la versión fallida pasaba a ser «la anterior»; RF-17); la comprobación de `ufw` ahora distingue el protocolo (`443/udp` o una regla sin protocolo ya no pasan);
  `check-host.sh` detecta cualquier dirección que no sea loopback (no solo `0.0.0.0`) y vigila también `docker-compose-plugin` y `docker-buildx-plugin`; el espacio libre se calcula con `printf` (mawk antiguo imprimía notación científica); `install-host-tasks.sh` crea `agent/dist` bajo el prefijo de las pruebas y no en el repositorio;
  las comprobaciones de privilegios de `restore-db.sh` están en un archivo SQL con **una prueba negativa por línea**, y hay pruebas de atomicidad (un volcado que falla a medias no deja tablas) y de fugas (contenedores, redes y volúmenes); casos de nginx para `real_ip_recursive` y la ruta de 16 MB; escenarios nuevos de `deploy.sh`
  (rollback tras un fallo, fallos antes del `migrator`, cada forma de puerto no permitido, también antes de un rollback, contenido del registro, la sonda que responde otra cosa, y la precedencia del entorno sobre `host.conf`); y en CI cada suite es un paso aparte, con `age` real instalado y sin poder omitir la parte con Docker.
  Cada mutación que sobrevivía se repitió tras añadir la prueba y ahora se detecta. **Sin cubrir** (necesitan imágenes reales, `root`, AWS o el servidor): `--migrator-image` y `--api-image` de `restore-db.sh`, el rechazo de un `host.conf` de otro usuario, `prune_images` y el borrado de la imagen del agente, `age`/`rclone` reales contra S3, `unattended-upgrade --dry-run`, `release.yml`, la validación del `Caddyfile` en CI y la entrada por SSH con clave (CA-6).
- **Pendiente:** la revisión de `frontend` y `device-agent`, el ensayo de los *workflows* en GitHub y el de `deploy.sh` en el servidor (tarea 13), y los datos operativos de §19.5 que no se escriben en el repositorio.
