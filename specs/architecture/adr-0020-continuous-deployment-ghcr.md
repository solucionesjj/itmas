---
id: ADR-0020
title: Despliegue continuo con GitHub Actions, GHCR y un script ejecutado por una persona
status: Accepted
date: 2026-10-26
deciders: [architecture, security]
related: [SPEC-0009, ADR-0018, ADR-0019, ADR-0010, SPEC-0002]
---

# ADR-0020 — Despliegue continuo con GitHub Actions, GHCR y un script ejecutado por una persona

> **Propuesto el 2026-10-26** como compuerta de SPEC-0009 (§10.2, §16.2). Concreta las decisiones del solicitante del 2026-10-25 (Q-9, Q-10, Q-11, Q-12, Q-13, Q-14 y Q-20)
> y las herramientas elegidas el 2026-10-26 (rclone, age, Trivy). **Aceptado por el solicitante el 2026-10-26**; la revisión independiente de `security` se hace al cerrar SPEC-0009, sobre este ADR y el código (como en ADR-0016 y ADR-0017). Es una operación del host
> en el sentido de ADR-0019 (§8.7): se define aquí y su configuración y scripts viven en el repositorio. **Actualizado el 2026-10-26 y el 2026-10-27** con lo que dejaron las revisiones independientes de la implementación (modelo de ejecución como `root`, orden de los pasos,
> publicación en dos fases, esquema en la vuelta atrás) y con la decisión del solicitante de que el repositorio es **público**.

## Contexto
Producción es un único servidor Ubuntu en AWS EC2 (ADR-0018, SPEC-0009). Hoy las imágenes se construyen en el propio servidor (`docker compose up -d --build`), no hay CI ni etiquetas de git, y
`DEVELOPMENT.md` §12.10 actualiza con `git checkout <etiqueta>` y volviendo a construir. SPEC-0009 pide imágenes en un registro, despliegue por *digest*, una vuelta atrás definida, la publicación de los paquetes
del agente y que **ninguna credencial de GitHub tenga acceso al servidor** (Q-11). La constitución exige que ningún secreto esté en el repositorio ni en la línea de comandos (§4) y mínimo privilegio (§3.8).

## Decisión
1. **Integración continua** (`.github/workflows/ci.yml`, en cada *push* a `main` y en cada PR, y reutilizable): suites de AGENTS.md (backend con Testcontainers, frontend, pruebas unitarias del agente,
   pruebas de los scripts), validación de `docker compose config -q`, `npm audit` y NuGet Audit (ya activo en la compilación, constitución §12.1). Los permisos del `GITHUB_TOKEN` son de solo lectura.
2. **Publicación** (`.github/workflows/release.yml`, al empujar una etiqueta `vMAJOR.MINOR.PATCH`), en tres trabajos: (i) CI; (ii) **construcción y escaneo**, con token de solo lectura: construye `itmas3-api`, `itmas3-migrator` e `itmas3-frontend`
   y cada imagen se escanea con **Trivy**; se bloquea con severidad alta o crítica con corrección disponible, **sin haber publicado nada**; (iii) **publicación**, que **espera la aprobación manual del entorno `production`** de GitHub y solo entonces empuja a GHCR
   (`ghcr.io/solucionesjj/…`) las mismas imágenes que se escanearon, con `packages: write` solo en ese trabajo, que no ejecuta más que herramientas de GitHub y de Docker fijadas por SHA. **Las etiquetas de imagen son inmutables**: si alguna de las tres ya existe, no se publica ninguna.
   No hay ningún secreto de GitHub que dé acceso al servidor: se usa el `GITHUB_TOKEN` del propio *workflow*. El resumen del trabajo lista el *commit* y los *digests*. Acciones y herramientas de terceros se fijan por SHA de commit y Dependabot propone sus actualizaciones.
3. **Paquetes del agente** (Q-14, Q-20): el mismo *workflow* los construye solo si no existe ya `itmas3-agent:<versión del agente>` en GHCR (la versión sale de `agent/Directory.Build.props`), con el procedimiento de `scripts/build-agent.sh`,
   y los publica como una imagen `FROM scratch` que contiene `<versión>/` y su `SHA256SUMS`, sin herramientas nuevas. Una versión publicada no se sobrescribe (RN-7).
4. **El servidor despliega por *digest*, con una persona** (Q-11, P-12): la persona administradora ejecuta `sudo scripts/deploy.sh <etiqueta>` por SSH. Todo lo del servidor lo ejecuta `root` con `sudo` (el clon `/opt/itmas3` es de `root`, nadie va en el grupo `docker`). El script **resuelve los *digests* de la etiqueta en el registro**,
   muestra el *commit* y los *digests* y pide confirmación **antes de tocar nada** (la persona los compara con el resumen del *workflow*; `--yes` la omite). Si los paquetes son privados usa una credencial de **solo lectura** de GHCR guardada por `docker login --password-stdin` como `root` (§ Visibilidad;
   nunca en el repositorio ni en `.env`). Pasos, en este orden: validar el formato de la etiqueta → `git fetch` sin `--force`, y comprobar que la etiqueta no se movió y que su *commit* está en `origin/main` → resolver los *digests* → confirmar → `checkout` →
   `docker compose config -q` → comprobar que el Compose solo publica `127.0.0.1:${FRONTEND_PORT}` → volcado previo **si ya existe el volumen de datos** → descargar las imágenes por *digest* → asegurar `postgres` y **ejecutar el `migrator` con la imagen nueva** (mientras la versión actual sigue atendiendo) →
   levantar `api` y `frontend` con `docker-compose.prod.yml` (`--no-build`) → esperar a que estén `healthy` → comprobar `/auth/session` por el loopback con el `Host` permitido → publicar los paquetes del agente → registrar y limpiar imágenes antiguas. Códigos de salida: 0 correcto, 1 uso o rechazo previo,
   2 puerto no permitido, 3 fallo antes de la conmutación, 4 fallo tras el `migrator`, 5 paquetes del agente.
   El estado vive en `/var/lib/itmas3` (de `root`, modo 700, sin secretos): la versión actual, la anterior (solo una versión **verificada** pasa a ser la anterior, y volver a desplegar la misma etiqueta no la sustituye), el nivel del esquema de cada una y un registro de despliegues.
   Las imágenes del agente son `FROM scratch`: `deploy.sh` las extrae con `docker create <imagen> placeholder` y `docker cp`, comprueba `SHA256SUMS` y que no haya archivos sin listar, y no sobrescribe una versión publicada; `latest` solo se reemplaza si la versión es la más alta.
5. **Vuelta atrás** (Q-13, P-14): si algo falla **antes de que el `migrator` termine con éxito**, no se ha tocado la versión que atiende: el script termina con error y deja el motivo (si el `migrator` falló a medias, avisa de que el esquema pudo avanzar). **Después** del `migrator` no revierte las imágenes: avisa y deja la decisión a una persona.
   Para esa decisión existe `scripts/deploy.sh rollback`, que vuelve a poner los *digests* anteriores **sin** ejecutar el `migrator`. Compara el nivel del esquema actual con el de la versión a la que vuelve y **se niega si difiere**, salvo `--schema-changed-ok` cuando una persona sabe que el cambio es compatible; la alternativa es restaurar el volcado previo (DEVELOPMENT.md §12.10).
6. **Compose de producción** (`docker-compose.prod.yml`): solo sustituye el origen de las imágenes por las variables `ITMAS_API_IMAGE`, `ITMAS_MIGRATOR_IMAGE` e `ITMAS_FRONTEND_IMAGE` (con *digest*), que exporta `deploy.sh`. `docker-compose.yml` no cambia de modo y `docker compose up --build` local sigue construyendo desde el código (constitución §8.1, RNF-7).
7. **Etiquetas y protección** (Q-12): `main` y las etiquetas `v*` protegidas, el entorno `production` con revisores obligatorios y limitado a las etiquetas `v*`, y la aprobación obligatoria de los *workflows* de colaboradores externos son **ajustes del repositorio de GitHub**, no archivos: se documentan en
   DEVELOPMENT.md §12.1 y los configura quien administra el repositorio. Mientras no existan, la compuerta humana de la publicación no está en pie.
8. **Visibilidad** (decisión del solicitante, 2026-10-27: **el repositorio es público a propósito**): el código, los *workflows*, las especificaciones y los ADR los lee cualquiera, así que no pueden contener nada confidencial (constitución §4) y la seguridad no depende de ocultarlos. Consecuencias:
   (a) el servidor **clona por HTTPS sin credencial**: no hay *deploy key* en el host; (b) los *workflows* de PR de *forks* corren con token de solo lectura y sin secretos, y una persona debe aprobar los de colaboradores externos; (c) las imágenes se construyen de código público y no contienen secretos,
   por lo que se **recomienda marcar los paquetes `itmas3-api`, `itmas3-migrator`, `itmas3-frontend` e `itmas3-agent` como públicos** (*Package settings → Change visibility*) y entonces el servidor no guarda **ninguna** credencial de GitHub; GHCR no siempre hereda la visibilidad del repositorio, por lo que se comprueba tras la primera publicación.
   Si se prefieren privados, el servidor usa un token de solo lectura (`read:packages`) de una cuenta de servicio, como describe DEVELOPMENT.md §12.1 y §12.5; (d) la topología y los riesgos aceptados de SECURITY.md son públicos: no se publican identificadores de cuentas de AWS, direcciones, *buckets* ni claves.

## Alternativas consideradas
| Alternativa | A favor | En contra |
|---|---|---|
| CI por SSH al servidor con comando forzado (Q-11 a) | Despliegue sin intervención | Exige abrir el 22 a los rangos de GitHub (choca con «22 restringido») y una credencial de producción en GitHub |
| El servidor consulta e instala solo (Q-11 b) | Sin SSH entrante | Un token de lectura y un temporizador en el host; la aprobación no puede cerrar el ciclo |
| *Runner* autoalojado en el servidor (Q-11 c) | Salida solo | Ejecuta código de *workflows* en producción: no recomendado |
| **Una persona ejecuta el script (elegida)** | La más simple, sin superficie nueva | Despliegue manual; el script es el único punto de entrada y admite a) o b) después |
| Construir en el servidor (como hoy) | Sin registro | Sin *digest*, sin separación CI y servidor, y el build compite con la aplicación por memoria |
| Paquetes del agente como adjuntos de una *release* | Más visibles | Un paso de descarga más en el host y un tercer origen de artefactos (con el repositorio público no necesitaría credencial, pero se pierde la verificación por *digest* de GHCR) |
| Paquetes copiados a mano desde el CI | Sin credencial extra | Un paso manual más y fácil de olvidar |

## Consecuencias
- **Positivas:**
  - Se despliega lo que pasó las suites, la auditoría y el escaneo, por *digest*, y el CI no tiene acceso al servidor.
  - Un servidor comprometido no puede publicar imágenes, y un CI comprometido no puede desplegar: hace falta una persona con SSH.
  - Cada despliegue deja un registro (versión, *digests*, hora y resultado).
- **Negativas / riesgos:**
  - **Una sola persona** aprueba el entorno y despliega (R-2): la separación de funciones es una pausa deliberada.
  - **Si los paquetes son privados, la credencial de lectura de GHCR vive en el host** (`/root/.docker/config.json`, de `root`): alcance de solo lectura, permisos `600` y rotación; es un token personal clásico porque GHCR no admite los de granularidad fina. Con paquetes públicos (recomendado, §8 de la decisión) no hay credencial.
  - **Un repositorio público expone la topología** (un servidor, SSH con clave y `fail2ban`, riesgos aceptados R-1 a R-5): no es un secreto, pero quien ataque sabe qué buscar; se mitiga con las medidas ya decididas y con no publicar identificadores.
  - **Los *workflows* de PR corren en un repositorio público**: sin secretos y con token de solo lectura, y con aprobación obligatoria para colaboradores externos, evitan el uso abusivo de los *runners*; el entorno `production` y la protección de etiquetas siguen siendo la compuerta de la publicación.
  - **Los *digests* se resuelven por etiqueta.** Quien pueda escribir en GHCR podría mover una etiqueta; lo cubre la confirmación contra el resumen del *workflow* y, a futuro, la verificación de atestaciones (fuera de alcance).
  - **Tras el `migrator` no hay reversión automática** (Q-13); el borde práctico es que una versión que migra y falla exige una decisión humana.
  - **Sin verificar en el entorno real:** `ci.yml` se ejecutó en GitHub el 2026-10-28 y pasa (SPEC-0009 §18), pero `release.yml` no se ha ejecutado, ni `deploy.sh` en el servidor. Se verifican según CA-10 a CA-15 de SPEC-0009.
- **Seguimiento:** desplegar desde el CI (Q-11 a o b) cuando el volumen de despliegues lo justifique, sin cambiar `deploy.sh`; verificar atestaciones de las imágenes; refinar la vuelta atrás para revertir también cuando el `migrator` no aplicó ninguna migración (P-14).

## Impacto en la constitución
Ninguno. Cumple §4 (sin secretos en el repositorio ni en la línea de comandos), §3.8 (mínimo privilegio: el CI sin acceso al servidor y credencial de solo lectura), §8.1 (`docker compose up --build` sigue funcionando) y §8.7 (ADR-0019, constitución 3.0.0): la operación está
enumerada en este ADR (regla inicial), sus scripts y *workflows* están versionados (a), no publica puertos (b) y sus credenciales son de mínimo alcance, sin ninguna credencial de CI con ejecución en el servidor (e).
