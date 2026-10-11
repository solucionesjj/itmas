---
id: SPEC-0005
title: Auditoría de accesos a motores SQL
type: feature
status: Approved
owner: jmartinez
created: 2026-10-10
approved-by: jmartinez (2026-10-11)
agents: [architecture, security, database, backend, frontend, device-agent, testing]
related: [SPEC-0001, SPEC-0002, SPEC-0003, SPEC-0004, ADR-0009, ADR-0010, ADR-0012, ADR-0014]
---

# SPEC-0005 — Auditoría de accesos a motores SQL

> Derivada de la definición funcional aportada por el solicitante (2026-10-10) y de sus respuestas a cuatro rondas de
> preguntas (2026-10-11), recogidas en §19. Las decisiones conservan su referencia P-n para poder rastrearlas.
> No quedan preguntas abiertas.
> Las enmiendas que la hacen posible ya están aplicadas: constitución 1.3.0, §5 y §13.6 ([ADR-0014](../architecture/adr-0014-database-engine-audit-agent.md)),
> y 2.0.0, certificados no válidos en §13.6 ([ADR-0015](../architecture/adr-0015-database-connections-without-verified-tls.md)).
> Aprobada el 2026-10-11.

## 1. Objetivo

Que el equipo de infraestructura audite a tiempo los accesos a los motores SQL:

1. El agente recolecta cada día, en cada motor configurado, las cuentas (de motor y de base de datos), sus roles y sus
   permisos con el **máximo detalle**: servidor, base de datos, esquema, objeto y columna.
2. La API los recibe y los almacena.
3. Una pantalla los muestra con **etiquetas de nivel de riesgo** (roles de color estándar de MD3, siempre con
   ícono y texto) y permite marcar o desmarcar como válidos, con un solo clic, la cuenta, cada rol y **cada permiso**.

## 2. Contexto

### Definición original (solicitante)

> **Problemática: auditorías manuales a sistemas SQL.** Auditar los accesos creados en los motores SQL es muy lento y a
> veces no se hace porque se priorizan actividades más urgentes, ya que no está automatizado. Se espera un sistema de
> recolección que permita al equipo de infraestructura ser oportuno y tener listos los insumos para identificar fallas
> en la configuración de los accesos que puedan convertirse en un riesgo o una vulnerabilidad explotable.
>
> 1. **Agente automatizado para la captura.** Un servicio de Windows/Linux/Mac que envíe diariamente los usuarios
>    creados en el motor SQL, tanto a nivel de motor como de base de datos, y los permisos que tienen configurados.
>    Hoy se entra a cada motor: al menos 5 minutos por motor.
> 2. **Captura de datos.** El agente envía la información de forma segura a una API que la almacena. Como puede llegar
>    información masiva de muchos nodos, la API recibe los datos y los guarda en una especie de cola para que otro
>    proceso los vaya procesando sin perder los registros que van llegando. Hoy el analista tabula los datos en Excel:
>    al menos 5 minutos por motor.
> 3. **Visualización de datos.** Sobre el Excel se buscan anomalías: se evalúa si los usuarios creados corresponden a
>    los que creó previamente el administrador. Se espera visualizar los datos de forma sencilla y que el analista o el
>    administrador puedan marcar un acceso como válido. Hoy se marcan casillas en Excel; el indicador de éxito es poder
>    marcar o desmarcar un registro como válido con un solo clic.

### Decisiones de la primera ronda que cambian la definición (§19.1)

- **Sin cola en el MVP.** La API procesa cada envío en la propia petición, como el inventario. La cola llegará en una
  spec posterior, cuando el volumen lo haga necesario (§10.4 fija el criterio).
- **Motores remotos y varias conexiones.** Un mismo agente audita varios motores, que pueden estar en otros servidores
  y fuera de un directorio. Cada motor tiene una cuenta propia para el agente; no se usa autenticación integrada.
- **Riesgo y validación por permiso.** Además de la validación de la cuenta, cada permiso tiene su propio estado de
  validación y una etiqueta de nivel de riesgo: Crítico, Alto o General.
- **Colores estándar de MD3** (segunda y tercera ronda). Se descarta la paleta roja, naranja y azul, y tampoco se crean
  colores personalizados: solo los roles del tema (§11.1). Todo estado de error, advertencia o éxito lleva un ícono
  representativo y texto.
- **Siempre TLS, con certificados no válidos admitidos** (tercera ronda). No hay conexiones sin TLS. Para los servidores
  sin certificado válido, el administrador del equipo puede elegir expresamente, en cada conexión, no validar el
  certificado (ADR-0015, constitución 2.0.0).

### Situación actual en ITMas

- SPEC-0002 dejó un **agente de dispositivo** (Windows, Linux y macOS) que se ejecuta como servicio, se enrola,
  guarda una API key por equipo y envía un inventario diario por HTTPS.
- SPEC-0003 exige **registrar el equipo** antes de instalar el agente; eliminar el equipo revoca su API key.
- La constitución 1.3.0 (§13.6) ya permite que el agente lea, en solo lectura, el catálogo de seguridad de motores de
  bases de datos configurados en el equipo (ADR-0014).

### Coste actual (estimación del solicitante)

Unos 10 minutos por motor y auditoría (5 de captura y 5 de tabulación), más la revisión en Excel. Con la recolección
automática, la captura y la tabulación desaparecen, y la revisión se reduce a lo nuevo o lo cambiado.

## 3. Alcance

**Incluye**
- Módulo de auditoría SQL en el agente existente, desactivado por defecto.
- Varias conexiones por agente, configuradas localmente, a motores locales o remotos:
  - Microsoft SQL Server 2019, 2022 y 2025;
  - PostgreSQL 18;
  - MySQL 9 LTS;
  - MariaDB 12 LTS.
- Recolección diaria de las cuentas, los roles y los permisos en todos los niveles: servidor, base de datos, esquema,
  objeto y columna; también la propiedad (*ownership*) y, en PostgreSQL, los atributos de rol y los privilegios por
  defecto.
- Recepción en la API con procesamiento en la propia petición, estado actual de cada instancia y registro de cambios.
- Clasificación de riesgo calculada en el servidor (Crítico, Alto, General) según un catálogo por motor.
- Validación con un clic de la cuenta, de cada pertenencia a un rol y de cada permiso.
- Pantallas de instancias, cuentas, detalle de una cuenta y vista de permisos de una instancia.
- Permisos `sql-audit.*`, auditoría de las validaciones, términos v2 del agente y manual de uso.

**Excluye**
- La cola de ingesta (spec posterior; §10.4).
- Cualquier cambio en los motores: ITMas no crea, modifica ni revoca accesos.
- Contenido de las bases de datos, consultas, sesiones, *logs* del motor y hashes o contraseñas.
- Oracle y otros motores no listados; bases de datos gestionadas en la nube, salvo que sean alcanzables como un motor
  de la lista con una cuenta propia y TLS (P-3).
- Configuración de las conexiones desde la interfaz (constitución §13.3 y §13.6).
- Validación en bloque (decisión del solicitante, P-16).
- Alertas y notificaciones. Exportación a Excel (P-11).

## 4. Actores

| Actor | Descripción |
|---|---|
| DBA | Crea en cada motor la cuenta de solo lectura del agente (guía en el manual) y, preferiblemente, activa TLS con un certificado confiable o fijable (RF-3). |
| Administrador del equipo | Persona con privilegios de administrador en el equipo donde está el agente. Configura las conexiones con `itmas-agent sql …`. |
| Agente | Servicio de SPEC-0002. Lee el catálogo de seguridad de cada conexión y envía una instantánea diaria por conexión con la API key del equipo. |
| Analista de infraestructura / administrador | Usuarios de ITMas que revisan y validan. No son roles (constitución §3.4): lo que pueden hacer depende de los permisos `sql-audit.*` que les conceden sus políticas (§7). |

## 5. Requerimientos

### 5.1 Funcionales

**Agente: conexiones**
- **RF-1** La auditoría SQL es un **módulo del agente existente**, desactivado por defecto. Un agente sin conexiones
  configuradas no se conecta a ningún motor y se comporta como hoy.
- **RF-2** El administrador del equipo gestiona **varias conexiones** con comandos locales que exigen privilegios de
  administrador (ADR-0014, punto 3):

  | Comando | Efecto |
  |---|---|
  | `itmas-agent sql add` | Pide alias, motor, *host*, puerto o instancia con nombre, base de datos inicial (PostgreSQL), usuario, contraseña y TLS (RF-3). Prueba la conexión y la lectura del catálogo; si falla, explica la causa y no guarda nada. |
  | `itmas-agent sql update <alias>` | Cambia los datos de una conexión, por ejemplo para rotar la contraseña. Vuelve a probarla antes de guardar. |
  | `itmas-agent sql list` | Lista las conexiones con su motor, destino, TLS y el resultado de la última recolección. Nunca muestra la contraseña. |
  | `itmas-agent sql test <alias>` | Prueba la conexión y la lectura del catálogo, y avisa si la cuenta tiene más privilegios de los necesarios. |
  | `itmas-agent sql remove <alias>` | Borra la conexión y su credencial. |

  - Hasta 50 conexiones por agente. El alias es único en el agente.
  - La contraseña se pide oculta, o se lee de un archivo o de una variable de entorno, nunca como argumento.
  - Se guarda protegida como la API key: DPAPI de máquina en Windows, y `0600` con propietario `root` en Linux y
    macOS.
  - No se admiten cadenas de conexión libres: el agente compone cada cadena con el *builder* del controlador.
- **RF-3** **Protección del canal** (ADR-0015). Toda conexión va cifrada con TLS, también al propio equipo. Cada
  conexión tiene un modo de TLS:

  | Modo | Comportamiento | Cómo se elige |
  |---|---|---|
  | `verify` (por defecto) | TLS con el certificado validado contra el almacén de confianza del sistema y el nombre de *host* | Opción por defecto |
  | `pinned` | TLS aceptando solo el certificado fijado (archivo o huella SHA-256) | Indicando el certificado |
  | `unverified` | TLS sin validar el certificado: acepta certificados no válidos | Elección expresa tras un aviso |

  - `unverified` nunca se aplica solo, ni aunque `verify` falle. `sql add` y `sql update` explican el riesgo, proponen
    `pinned` y piden escribir `ACEPTO EL RIESGO`. Sin consola interactiva, exigen `--tls unverified`.
  - Un motor que no acepte TLS no se puede auditar: `sql add` lo explica y no guarda la conexión.
  - El modo viaja en la instantánea; la interfaz señala con un aviso las instancias con `unverified` (RF-20).

**Agente: recolección**
- **RF-4** Cada día, junto con el inventario (P-12), el agente recolecta por cada conexión:

  | Bloque | Contenido |
  |---|---|
  | Instancia | Motor, versión, edición, nombre de la instancia, modo de autenticación del motor. |
  | Cuentas de nivel motor | *Logins* de SQL Server, roles de PostgreSQL, cuentas `usuario@host` de MySQL y MariaDB, y roles de servidor. Por cada una: nombre, tipo, habilitada o bloqueada, fechas de creación y modificación, base de datos por defecto y atributos de contraseña (política, caducidad, contraseña vencida, `VALID UNTIL`). |
  | Cuentas de nivel base de datos | Solo en SQL Server: usuarios, roles de base de datos y roles de aplicación de cada base de datos, con el *login* asociado o la marca de **huérfano** y el esquema por defecto. |
  | Pertenencia a roles | Roles de servidor, de base de datos y de PostgreSQL, MySQL y MariaDB, incluidos los roles anidados. |
  | Permisos | Todos los niveles: servidor (o global), base de datos, esquema, objeto (tabla, vista, procedimiento, función, secuencia, tipo…) y columna. Por cada permiso: titular, objeto asegurable, permiso, estado (`GRANT`, `GRANT WITH GRANT OPTION`, `DENY`) y quién lo concedió. |
  | Propiedad | Propietario de cada base de datos y de cada esquema u objeto cuyo propietario no sea el predeterminado. Se trata como el permiso `OWNERSHIP`. |
  | PostgreSQL | Atributos de rol (`SUPERUSER`, `CREATEROLE`, `CREATEDB`, `REPLICATION`, `BYPASSRLS`, `LOGIN`), tratados como permisos de servidor, y privilegios por defecto (`ALTER DEFAULT PRIVILEGES`). |
  | Bases de datos | Nombre, estado y propietario. |

- **RF-5** Para que el volumen sea manejable, no se envían los permisos **predeterminados de los objetos del propio
  motor** concedidos a `public`: objetos `is_ms_shipped` en SQL Server, y ACL sin modificar de `pg_catalog` e
  `information_schema` en PostgreSQL. La instantánea informa cuántos se omitieron. Cualquier permiso modificado o
  concedido expresamente sobre esos objetos sí se envía.
- **RF-6** El agente **nunca** lee hashes ni contraseñas, datos de tablas de usuario, texto de procedimientos ni
  sesiones. Solo consulta el catálogo de seguridad con consultas fijas de solo lectura, y recorre las bases de datos
  abriendo una conexión por base de datos, nunca con `USE` (constitución §13.6).
- **RF-7** Si no se puede leer una conexión o una base de datos (sin conexión, certificado no válido, sin permiso,
  base de datos fuera de línea, tiempo agotado), la instantánea se envía igualmente con esa parte marcada como **no
  disponible** y el motivo. Nunca se rellenan valores supuestos (SPEC-0002 RN-6).
- **RF-8** El agente envía **una instantánea por conexión**, con la API key del equipo y los reintentos de
  SPEC-0002 RF-8. Cada instantánea lleva un `submissionId` (UUID) para que un reintento no se procese dos veces.

**API: recepción y procesamiento**
- **RF-9** `POST /api/v1/sql-audit-snapshots`:
  1. autentica el equipo y valida el tamaño y el contrato (§12.2);
  2. procesa la instantánea **en la misma petición** y en una transacción;
  3. responde `202`.

  Se responde `202`, como en el inventario, para que una cola futura no cambie el contrato con el agente.
- **RF-10** Un `submissionId` ya recibido del mismo equipo responde `202` sin volver a procesarse.
- **RF-11** El procesamiento de una instantánea:
  1. registra la instancia si es nueva (RN-1) y actualiza sus datos;
  2. calcula el riesgo de cada elemento con el catálogo (§8.6);
  3. compara con el estado actual y marca los elementos **nuevos**, **cambiados** y **eliminados**;
  4. anota cada diferencia en el **registro de cambios** de la instancia.
- **RF-12** Una instantánea con fecha de recolección anterior a la última procesada de la instancia se registra, pero
  no cambia el estado actual (SPEC-0002 RN-5).
- **RF-13** Si una parte vino como no disponible (RF-7), sus elementos conservan su estado anterior y se indica que no
  están actualizados. No se marcan como eliminados.

**Riesgo**
- **RF-14** Cada elemento tiene un nivel de riesgo calculado en el servidor con el catálogo de §8.6:

  | Nivel | Estilo MD3 (§11.1) | Ejemplos |
  |---|---|---|
  | **Crítico** | Rol `error` + ícono `error` | `sa`, `root`, `sysadmin`, superusuario, `db_owner`, `CONTROL SERVER`, `ALL PRIVILEGES` globales: privilegios amplios |
  | **Alto** | Rol `tertiary` + ícono `warning` | `INSERT`, `UPDATE`, `DELETE`, `EXECUTE`, `ALTER`, `DROP`, `db_datawriter` |
  | **General** | Rol `on-surface-variant` + ícono `info` | `SELECT`, `CONNECT`, `USAGE`, `db_datareader` |

  - Los elementos son la cuenta, cada pertenencia a un rol y cada permiso.
  - Un `DENY` no lleva color de riesgo: se muestra como "Denegado".
  - El riesgo de una cuenta es el máximo de sus elementos, incluido el que hereda de los roles de los que es miembro
    (P-24).
  - Cada nivel lleva siempre su ícono y su texto: el significado nunca depende solo del color (§11.1).
- **RF-15** El agente no clasifica: si el catálogo cambia, el servidor recalcula el riesgo de los elementos actuales
  al desplegarse, sin tocar el agente.

**Validación**
- **RF-16** Se validan por separado, cada uno con su casilla "Válido":
  1. **la cuenta**: que exista y tenga esos atributos (habilitada, tipo, política de contraseñas…);
  2. **cada pertenencia a un rol**;
  3. **cada permiso**.

  Un clic marca la casilla y otro la desmarca, sin diálogos de confirmación. El cambio se guarda al momento, se ve sin
  recargar y, si falla, la casilla vuelve a su estado anterior con un aviso.
- **RF-17** Estados de validación de cada elemento:

  | Estado | Significado |
  |---|---|
  | Pendiente de validar | Nuevo, o desmarcado. |
  | Válido | Marcado por un usuario y sin cambios desde entonces. |
  | Cambió desde la validación | Solo en la cuenta: validada, pero cambió un atributo (P-9). |
  | Eliminado del motor | Ya no figura en la última instantánea. No se puede marcar. |

  Un permiso o una pertenencia no "cambian": cambiar el estado de un permiso (por ejemplo, de `GRANT` a
  `GRANT WITH GRANT OPTION`) produce un permiso nuevo, pendiente, y el anterior pasa a eliminado (RN-3).
- **RF-18** Una cuenta está **completamente validada** cuando la cuenta y todos sus roles y permisos actuales son
  válidos. Las pantallas muestran por cuenta el número de elementos pendientes.
- **RF-19** Toda validación y desvalidación queda en la auditoría existente (SPEC-0001) con el usuario y la fecha.

**Pantallas**
- **RF-20** **Listado de instancias**: equipo que la audita, alias, motor y versión, destino (*host* y puerto), fecha
  de la última recolección, número de cuentas, elementos pendientes y **pendientes críticos**. Con búsqueda, filtro por
  motor y orden. Llevan un aviso las instancias con partes no disponibles y las auditadas en modo `unverified` (RF-3).
- **RF-21** **Cuentas de una instancia**: una fila por cuenta con:
  - nivel ("Motor" o el nombre de la base de datos), nombre, tipo y habilitada;
  - etiqueta de riesgo y resumen de roles;
  - elementos pendientes y casilla "Válido" de la cuenta;
  - fecha en que se detectó.

  Se puede filtrar por nivel, base de datos, tipo, riesgo y estado de validación, y buscar por nombre. Por defecto se
  muestran las cuentas con elementos pendientes (P-15).
- **RF-22** **Detalle de una cuenta**:
  - cabecera con sus atributos, riesgo y casilla "Válido";
  - tabla de roles con riesgo y casilla;
  - tabla de permisos (objeto asegurable con su ruta `base.esquema.objeto.columna`, permiso, estado, quién lo
    concedió, riesgo, fecha de detección y casilla), con filtros por riesgo, validación, base de datos y tipo de
    objeto;
  - registro de cambios e historial de validaciones.
- **RF-23** **Permisos de una instancia** (P-25): todos los permisos de la instancia en una tabla, con filtros por
  riesgo, validación, base de datos, cuenta y tipo de objeto, por ejemplo "críticos pendientes". Cada fila tiene su
  casilla "Válido".

### 5.2 No funcionales

- **RNF-1** Recolección: prioridad baja, 30 s como máximo por consulta y 5 minutos por conexión; solo lecturas de
  catálogo.
- **RNF-2** Procesamiento en la petición: una instancia con el volumen de RNF-5 se procesa en menos de 30 s, con
  operaciones por lotes (inserciones y actualizaciones en bloque, no entidad a entidad).
- **RNF-3** **Criterio para introducir la cola** (spec posterior), si se cumple cualquiera de estas condiciones:
  - el percentil 95 del procesamiento supera 10 s;
  - los envíos rechazados por tiempo o por carga superan el 1 % en una semana;
  - el número de conexiones auditadas supera 10 veces el volumen inicial.

  La API publica la duración del procesamiento como métrica (ADR-0006).
- **RNF-4** Tamaño: una instantánea por conexión, con un límite de **16 MB** solo en la ruta de este endpoint, sin
  compresión (P-8). El valor se revisa al medir la primera instancia real (P-2b). El resto de la API mantiene 1 MB.
- **RNF-5** Capacidad (P-2b): el diseño soporta 800 instancias y, por instancia, 200 bases de datos,
  20 000 cuentas y 200 000 permisos.
- **RNF-6** Contrato estricto (constitución §3.6): la API rechaza las propiedades desconocidas y se despliega antes que
  el agente que use campos nuevos.
- **RNF-7** Las pantallas cumplen la constitución §6: Angular Material, temas claro y oscuro, español, accesibilidad
  (la casilla lleva una etiqueta con el nombre del elemento y se usa con teclado; el riesgo no depende solo del color)
  y pantallas estrechas. Los estados de error, advertencia y éxito usan los roles de §11.1 y siempre llevan ícono y
  texto.
- **RNF-8** Todo dato procedente de un motor es no confiable: se muestra como texto y nunca se concatena en SQL, ni en
  el servidor ni en el agente.

## 6. Reglas de negocio

| ID | Regla |
|---|---|
| RN-1 | Una instancia pertenece al equipo cuyo agente la informa; su identidad la determina la API key, nunca el contenido de la instantánea. Se identifica por equipo + motor + *host* normalizado + puerto + nombre de instancia (P-23). |
| RN-2 | Una cuenta se identifica por instancia + nivel + base de datos + nombre + *host* (MySQL y MariaDB) + tipo. |
| RN-3 | Un permiso se identifica por cuenta titular + objeto asegurable (clase, base de datos, esquema, objeto, columna) + permiso + estado. Una pertenencia, por cuenta + rol. Al cambiar cualquiera de esos datos se convierte en otro elemento. |
| RN-4 | La validación de una cuenta se refiere a sus atributos tal como estaban al validarla (su huella). Si cambian, pasa a "Cambió desde la validación" (P-9). |
| RN-5 | Solo se pueden validar elementos que existen en el estado actual. |
| RN-6 | Marcar y desmarcar son idempotentes. Validar una cuenta exige que la huella que vio el usuario siga siendo la actual. |
| RN-7 | Un dato no disponible se informa como tal y nunca se rellena con valores supuestos. |
| RN-8 | Un equipo eliminado (SPEC-0003) no envía instantáneas. Sus instancias siguen consultables durante la retención, marcadas como de un equipo eliminado. |
| RN-9 | El riesgo lo asigna el servidor con el catálogo vigente (§8.6). Un elemento que no está en el catálogo es **Alto**, para que nada quede sin revisar (P-22). |

Las violaciones de RN-5 devuelven `422`; las de RN-6 (huella desactualizada), `409`.

## 7. Permisos

Categoría "Auditoría SQL", declarados en un `IPermissionDefinitionProvider` del módulo. Todos otorgables.

| Permiso | Concede | Endpoints / pantallas |
|---|---|---|
| `sql-audit.read` | Consultar instancias, cuentas, roles, permisos, riesgo, registro de cambios e historial de validaciones | Todas las pantallas de §11; `GET /sql-instances…`, `GET /sql-accounts…` |
| `sql-audit.accesses.validate` | Marcar y desmarcar como válidos cuentas, pertenencias a roles y permisos | Casillas "Válido"; `PUT`/`DELETE …/validation` |

Validar exige además `sql-audit.read`, porque se hace desde las pantallas de consulta (como en SPEC-0001 y SPEC-0003).

`sql-audit.submissions.read`, aceptado en la primera ronda, se retira porque no hay cola que consultar. Volverá con la
spec de la cola.

El agente no recibe permisos: usa la política `EnrolledDevice` existente (ADR-0009).

## 8. Comportamiento esperado

### 8.1 Preparación de un motor y configuración en el agente

```text
DBA           ──▶ crea la cuenta de solo lectura del agente (guía por motor en el manual; §10.3)
              ──▶ activa TLS en el motor, preferiblemente con un certificado confiable o provisionado para fijarlo
Administrador ──▶ itmas-agent sql add     (con privilegios de administrador; puede repetirse para N motores)
Agente        ──▶ pide alias, motor, host, puerto/instancia, usuario, contraseña (oculta) y modo de TLS
                  (unverified: aviso del riesgo, propuesta de pinned y confirmación "ACEPTO EL RIESGO")
              ──▶ prueba la conexión, el certificado y la lectura del catálogo
                  ├─ falla ──▶ explica la causa (DNS, conexión, motor sin TLS, certificado, credenciales, permisos)
                  │            y no guarda
                  └─ ok    ──▶ avisa si la cuenta tiene privilegios de más ──▶ guarda la conexión protegida
```

### 8.2 Envío diario

```text
Agente ──▶ por cada conexión: consultas fijas de catálogo, una conexión por base de datos (RNF-1)
       ──▶ POST /api/v1/sql-audit-snapshots   Authorization: Device <apiKey> · X-CSRF: 1
API    ──▶ autentica el equipo ──▶ valida tamaño y contrato
       ──▶ submissionId ya recibido ──▶ 202 sin procesar
       ──▶ transacción: instancia → riesgo → diferencias (nuevos, cambiados, eliminados) → registro de cambios
       ◀── 202 { submissionId }
Errores: 400 contrato · 401 API key · 413 tamaño · 429 límite de frecuencia
```

### 8.3 Revisión y validación

```text
Analista ──▶ Auditoría SQL → instancias → cuentas (por defecto, con pendientes) → detalle de la cuenta
         ──▶ clic en "Válido" de un permiso ──▶ PUT /sql-access-items/{id}/validation
API      ──▶ RN-5 ──▶ guarda [auditar sql-audit.item.validated] ◀── 200 { status, by, at }
SPA      ──▶ casilla marcada; contador de pendientes actualizado; si hay error, vuelve atrás y avisa
         ──▶ otro clic ──▶ DELETE /sql-access-items/{id}/validation [auditar sql-audit.item.unvalidated] ◀── 204
```

### 8.4 Mensajes de la interfaz

| Situación | Comportamiento |
|---|---|
| Sin instancias | "Todavía no hay motores SQL auditados. Configúralos en el agente con `itmas-agent sql add`." con enlace al manual. |
| Sin pendientes | "No hay elementos pendientes de validar." |
| Validación de cuenta rechazada por cambio (`409`) | "Esta cuenta cambió desde que cargaste la página. Revisa los cambios y vuelve a validarla." y la fila se actualiza. |
| Error al guardar | La casilla vuelve a su estado y aparece "No se pudo guardar la validación. Inténtalo de nuevo." |
| Datos no actualizados (RF-13) | "No se pudo leer en la última recolección: <motivo>." en la instancia o la base de datos. |
| Error de carga | Mensaje estándar del interceptor y botón "Reintentar". |

### 8.5 Ejemplo de lectura de una cuenta

```text
app_ventas · Motor · Login SQL · Habilitada · [error Crítico] · 3 pendientes        [ ] Válido
  Roles      sysadmin                                  [error Crítico]  Pendiente   [ ] Válido
  Permisos   Ventas.dbo.Facturas   SELECT  GRANT       [info General]  Válido      [✓] Válido
             Ventas.dbo.Facturas   DELETE  GRANT       [warning Alto]     Pendiente   [ ] Válido
             Ventas.dbo.Clientes   UPDATE  GRANT       [warning Alto]     Pendiente   [ ] Válido
```

`error`, `warning` e `info` son los íconos que acompañan a cada nivel (§11.1).

### 8.6 Catálogo de riesgo (propuesta, P-22)

El catálogo vive en el código del servidor, versionado y probado. No se edita desde la interfaz en la v1.

| Nivel | SQL Server | PostgreSQL | MySQL / MariaDB |
|---|---|---|---|
| **Crítico** | *Login* `sa`; roles `sysadmin`, `securityadmin`, `serveradmin`; `CONTROL SERVER`, `ALTER ANY LOGIN`, `ALTER ANY SERVER ROLE`, `IMPERSONATE ANY LOGIN`; roles `db_owner`, `db_securityadmin`, `db_accessadmin`; `CONTROL`, `ALTER ANY USER`, `ALTER ANY ROLE` o `IMPERSONATE` sobre la base de datos; propietario de una base de datos | `SUPERUSER`, `CREATEROLE`, `BYPASSRLS`, `REPLICATION`; roles `pg_execute_server_program`, `pg_read_server_files`, `pg_write_server_files`; propietario de una base de datos; `ALL` sobre una base de datos | Cuenta `root`; globales (`*.*`): `ALL PRIVILEGES`, `SUPER`, `GRANT OPTION`, `CREATE USER`, `FILE`, `SHUTDOWN`, `SYSTEM_USER`; `ALL PRIVILEGES` sobre una base de datos |
| **Alto** | `INSERT`, `UPDATE`, `DELETE`, `EXECUTE`, `ALTER`, `CREATE …`, `TAKE OWNERSHIP`, `CONTROL` sobre un esquema u objeto, `REFERENCES`; roles `db_datawriter`, `db_ddladmin`, `dbcreator`; propietario de esquema u objeto; cualquier permiso `WITH GRANT OPTION` que no sea crítico | `INSERT`, `UPDATE`, `DELETE`, `TRUNCATE`, `EXECUTE`, `CREATE`, `TRIGGER`, `REFERENCES`, `CREATEDB`; roles `pg_write_all_data` y `pg_read_all_data`; propietario de esquema u objeto; privilegios por defecto de escritura | `INSERT`, `UPDATE`, `DELETE`, `EXECUTE`, `CREATE`, `DROP`, `ALTER`, `INDEX`, `TRIGGER`, `EVENT`, `CREATE ROUTINE`, `ALTER ROUTINE`, `PROCESS`, `RELOAD`; `GRANT OPTION` por base de datos |
| **General** | `SELECT`, `CONNECT`, `VIEW DEFINITION`, `SHOWPLAN`; rol `db_datareader`; pertenencia a `public` | `SELECT`, `CONNECT`, `USAGE`, `TEMPORARY`, `LOGIN` | `SELECT`, `SHOW VIEW`, `USAGE`, `SHOW DATABASES` |

- `DENY` se muestra como "Denegado", sin color de riesgo.
- Lo que no figura en el catálogo es **Alto** (RN-9).
- Una cuenta deshabilitada conserva su riesgo y muestra "Deshabilitada".

## 9. Criterios de aceptación

- [ ] **CA-1** Con varias conexiones configuradas (al menos una local y una remota), el agente envía cada día una
  instantánea por conexión con las cuentas, roles y permisos de todos los niveles de RF-4.
- [ ] **CA-2** Un agente sin conexiones no abre conexiones a ningún motor y su comportamiento no cambia.
- [ ] **CA-3** `sql add` y `sql update` prueban la conexión antes de guardar. La contraseña no se muestra al teclearla,
  no se acepta como argumento, se guarda protegida por el SO, no aparece en logs ni en `sql list` y no se envía a la API.
- [ ] **CA-4** En modo `verify`, un motor con un certificado no confiable se rechaza; en `pinned` se acepta solo el
  certificado fijado; `unverified` solo se activa con la confirmación expresa del administrador, nunca de forma
  automática, y la interfaz señala esas instancias con un aviso. Un motor sin TLS se rechaza en todos los modos.
- [ ] **CA-5** La instantánea no contiene hashes, contraseñas ni datos de tablas de usuario. Omite los permisos
  predeterminados de los objetos del motor e informa cuántos omitió (RF-5).
- [ ] **CA-6** Una conexión inaccesible o una base de datos sin permiso se informan como no disponibles con su motivo;
  el resto se envía, y sus elementos no se marcan como eliminados.
- [ ] **CA-7** Las pruebas del agente con motores en contenedor (SQL Server 2019, 2022 y 2025, PostgreSQL 18, MySQL 9
  LTS y MariaDB 12 LTS) obtienen los datos de RF-4 con la cuenta de mínimo privilegio de §10.3.
- [ ] **CA-8** La API procesa una instantánea válida y responde `202`. Reenviar el mismo `submissionId` no la procesa
  otra vez, y una instantánea más antigua que la última no cambia el estado actual.
- [ ] **CA-9** Tras dos instantáneas consecutivas, los elementos añadidos aparecen como pendientes, los quitados como
  eliminados y el registro de cambios lo refleja con su fecha.
- [ ] **CA-10** Cada elemento muestra el nivel de riesgo del catálogo (§8.6). Una cuenta hereda el riesgo de sus roles,
  y un permiso desconocido es Alto.
- [ ] **CA-11** Un clic marca como válido una cuenta, un rol o un permiso y otro clic lo desmarca, sin diálogos. El
  estado persiste al recargar y cada cambio queda auditado con el usuario y la fecha.
- [ ] **CA-12** Si cambia un atributo de una cuenta validada, pasa a "Cambió desde la validación". Validarla con una
  huella desactualizada devuelve `409`, y validar un elemento eliminado, `422`.
- [ ] **CA-13** Los listados de instancias, cuentas y permisos muestran los contadores de pendientes, y los filtros por
  riesgo, validación, nivel, base de datos y tipo funcionan.
- [ ] **CA-14** Cada endpoint de usuario devuelve `401` sin sesión y `403` sin su permiso. Una API key de equipo no
  accede a los endpoints de usuario, y una sesión de usuario no puede enviar instantáneas.
- [ ] **CA-15** Una instantánea con propiedades desconocidas, fuera de límites, mayor de 16 MB o mal formada se
  rechaza con `400` o `413` y Problem Details, sin cambiar nada.
- [ ] **CA-16** Un equipo eliminado recibe `401` al enviar; sus instancias siguen consultables y marcadas.
- [ ] **CA-17** Un nombre de cuenta, base de datos u objeto con marcado HTML se muestra como texto literal, y un
  nombre con comillas o corchetes no rompe las consultas del agente.
- [ ] **CA-18** Las pantallas funcionan en tema claro y oscuro, con teclado y en pantalla estrecha, y el riesgo se
  entiende sin depender del color: cada estado de error, advertencia o éxito lleva su ícono.
- [ ] **CA-19** Las instalaciones nuevas muestran y exigen los términos v2 (Anexo A); `sql add` no pide términos en
  las instalaciones existentes.
- [ ] **CA-20** El manual de uso describe los procesos de §17.

## 10. Análisis arquitectónico

### 10.1 Módulo del agente existente (decidido)

La auditoría SQL es un módulo del agente de SPEC-0002 (§19.1). Reutiliza:

- el enrolamiento y la API key por equipo;
- el planificador y los reintentos;
- el almacén protegido;
- el empaquetado y la distribución.

Las instancias quedan ligadas al equipo registrado (SPEC-0003).

### 10.2 Fuentes por motor

Solo vistas del catálogo, con texto SQL fijo. ADR-0014 fija las reglas de conexión y consulta.

| Motor | Cuentas y roles | Permisos y propiedad |
|---|---|---|
| SQL Server 2019/2022/2025 | `sys.server_principals`, `sys.sql_logins` (sin `password_hash`), `sys.server_role_members`; por base de datos `sys.database_principals`, `sys.database_role_members` | `sys.server_permissions`; por base de datos `sys.database_permissions` (clase, `major_id`/`minor_id` → esquema, objeto, columna), `sys.objects`, `sys.schemas`, `sys.columns`; propietarios en `sys.databases`, `sys.schemas` y `sys.objects` |
| PostgreSQL 18 | `pg_roles` (atributos), `pg_auth_members` | `pg_database.datacl`; por base de datos: ACL de `pg_namespace`, `pg_class`, `pg_proc`, `pg_type` y `pg_attribute` (columnas), `pg_default_acl`; propietarios en los mismos catálogos |
| MySQL 9 LTS / MariaDB 12 LTS | `mysql.user` (o `mysql.global_priv` en MariaDB) sin columnas de autenticación; `mysql.role_edges` (MySQL) o `mysql.roles_mapping` (MariaDB) | `mysql.user` o `global_priv`, `mysql.global_grants` (privilegios dinámicos de MySQL), `mysql.db`, `mysql.tables_priv`, `mysql.columns_priv`, `mysql.procs_priv` |

En PostgreSQL, MySQL y MariaDB las cuentas son de motor (no hay usuarios por base de datos): los permisos de cada base
de datos cuelgan de la cuenta de motor, con la base de datos en el objeto asegurable.

### 10.3 Cuenta de mínimo privilegio del agente (a confirmar en la implementación)

| Motor | Cuenta prevista |
|---|---|
| SQL Server | *Login* SQL dedicado con `CONNECT ANY DATABASE` y `VIEW ANY DEFINITION` (en 2022 y 2025, `VIEW ANY SECURITY DEFINITION` si basta). Sin roles de servidor. |
| PostgreSQL 18 | Rol con `LOGIN` y `CONNECT` en cada base de datos. Sin superusuario ni roles predefinidos. |
| MySQL 9 LTS | Usuario con `SELECT` solo en las tablas de §10.2 y, en `mysql.user`, solo en las columnas que no son de autenticación. |
| MariaDB 12 LTS | Usuario con `SELECT` sobre las tablas de §10.2. `mysql.global_priv` no admite excluir la autenticación: riesgo documentado en ADR-0014. |

`sql test` compara los privilegios reales de la cuenta con estos y avisa si sobran.

### 10.4 Procesamiento sin cola (decidido) y evolución

- La API procesa la instantánea en la petición, en una transacción, con operaciones por lotes. Es el patrón actual del
  inventario y no introduce servicios ni patrones nuevos (§2.5).
- El contrato ya está preparado para la cola: `202` y `submissionId` idempotente. Una spec posterior podrá añadir una
  tabla de cola en PostgreSQL con un procesador en segundo plano, como se analizó en el primer borrador, sin cambiar el
  agente. RNF-3 fija cuándo hará falta.
- Riesgo aceptado del MVP: si la API no está disponible o rechaza por carga, el agente reintenta con espera
  exponencial (SPEC-0002 RF-8). No se pierden datos, solo se retrasan, porque cada reintento vuelve a recolectar.

### 10.5 Modelo y almacenamiento

- **No se guarda el JSON completo** de cada instantánea. Bastan el estado actual y el registro de cambios para el
  seguimiento, y se evita multiplicar el almacenamiento con instantáneas de hasta 16 MB al día por instancia.
- La diferencia se calcula con la identidad de cada elemento (RN-2, RN-3) como hash SHA-256 de su clave canónica,
  comparando conjuntos.
- La huella de una cuenta es el hash de sus atributos (RN-4).

| Capa | Cambios |
|---|---|
| Domain | `SqlInstance`, `SqlAccount` (atributos, huella, validación), `SqlAccessItem` (pertenencia o permiso, riesgo, validación), `RiskLevel`, `ValidationStatus`; catálogo de riesgo como servicio de dominio puro (`SqlRiskCatalog`), con reglas por motor. Reglas RN-2 a RN-9. |
| Application | `SubmitSqlAuditSnapshot` (procesa: instancia, riesgo, diferencias, registro de cambios), `ValidateSqlAccount`/`UnvalidateSqlAccount`, `ValidateSqlAccessItem`/`UnvalidateSqlAccessItem`; consultas de instancias, cuentas, permisos, cambios; puertos de repositorio; permisos y acciones de auditoría. |
| Infrastructure | Repositorios y consultas EF Core; diferencias con operaciones por lotes (`AddRange`, `ExecuteUpdate`, `ExecuteDelete`), sin SQL explícito; migración. |
| Api | Controlador del agente (`EnrolledDevice`, límite de 16 MB solo en esa acción, límite de frecuencia por equipo), controladores de consulta y validación (`[RequirePermission]`), contratos, purga del registro de cambios. |
| nginx | `location` exacta de `/api/v1/sql-audit-snapshots` con `client_max_body_size 16m`. El resto, 1 MB. |
| Agente | Módulo `SqlAudit` en `Itmas3.Agent.Collectors`: un colector por motor con consultas fijas y *mappers* puros probados con *fixtures*. Comandos `sql *` en `Itmas3.Agent`. Almacén de conexiones. Términos v2. Controladores de ADR-0014. |
| Frontend | Nueva feature `features/sql-audit/` (§11). |

### 10.6 Decisiones que requieren ADR

- **ADR-0014** (aceptado): auditoría de motores desde el agente, enmienda 1.3.0, credenciales, TLS y controladores.
- La cola tendrá su propio ADR en la spec que la introduzca.
- **ADR-0015** (aceptado): conexiones con certificados no válidos (`unverified`), siempre con TLS; sustituye el punto 5
  de ADR-0014; enmienda MAJOR 2.0.0 de §13.6.
- Los colores de estado no requieren ADR: solo se usan roles estándar del tema (§11.1).

### 10.7 Contradicciones con la constitución y su resolución

| # | Regla | Conflicto | Resolución |
|---|---|---|---|
| 1 | §13.4 | Conectarse a un motor no es leer el SO. | **Resuelto**: enmienda MINOR 1.3.0, §13.6 (ADR-0014), aprobada por el solicitante. |
| 2 | §5.1 | "PostgreSQL es la única base de datos; acceso con EF Core". | **Resuelto**: enmienda PATCH 1.3.0, §5 regula la persistencia de ITMas. |
| 3 | §13.2, §4 | Credenciales de los motores en el equipo. | **Resuelto** en §13.6: credenciales propias de cada conexión, introducidas localmente y protegidas por el SO; nunca viajan a la API. |
| 4 | §13.5 | Controladores nuevos en el agente. | **Resuelto**: justificados en ADR-0014, incluidos en el ejecutable (decisión del solicitante). |
| 5 | §5.2 | Recorrer bases de datos invita a concatenar en `USE`. | **Resuelto**: prohibido en §13.6; una conexión por base de datos con el *builder*. Sin la cola, el servidor no necesita SQL explícito. |
| 6 | §3.4 | "Analista" y "administrador" en la definición. | Permisos `sql-audit.*` por políticas; sin roles (confirmado). |
| 7 | §13.3 | Configurar motores desde la interfaz. | Configuración local (confirmado). |
| 8 | §3.9 y límite de 1 MB | Las instantáneas con permisos por objeto superan 1 MB. | Aumento **solo** en la ruta de este endpoint (16 MB), sin compresión, con revisión de `security`. El solicitante lo pidió; el valor se revisa con la primera instancia real (P-2b). No relaja ninguna protección de la lista de §3.9. |
| 9 | §6.2–§6.3 (paletas por defecto; no sobrescribir colores ni tokens) | La primera definición pedía etiquetas rojas, naranjas y azules, y el tema no tiene naranja. | **Resuelto sin enmienda**: el solicitante descartó la paleta y los colores personalizados. Solo se usan roles estándar del tema (`error`, `tertiary`, `primary`, `on-surface-variant`, `outline`) con ícono y texto (§11.1), como las pantallas actuales. |
| 10 | SPEC-0002 Anexo A, RN-3 | Los términos v1 no mencionan los motores SQL. | Términos v2 (Anexo A), con el contexto corporativo y contractual que indicó el solicitante. Solo los aceptan las instalaciones nuevas (P-17b). |
| 11 | SPEC-0002 RNF-2 | "Solo conexiones HTTPS a ITMas y a ipinfo.io". | Esta spec la amplía a las conexiones configuradas en el agente (§13.6). |
| 12 | §11.4 | Quedan puntos sin decidir. | §19.4. |
| 13 | §13.6 (1.3.0: "sin ninguna opción para desactivar la validación") y §3.1 | Hay servidores sin certificado válido que se deben auditar. | **Resuelto**: enmienda MAJOR 2.0.0 (ADR-0015), aprobada por el solicitante. El cifrado sigue siendo obligatorio; solo se puede omitir la validación del certificado, por conexión y de forma expresa; no afecta a la comunicación con la API (§13.3). |

## 11. Cambios de frontend

Feature `features/sql-audit/`, con carga diferida. La sección se llama **"Auditoría SQL"** (P-18): un grupo nuevo de
la navegación con ese título y la entrada "Instancias" (icono `database`), visible con `sql-audit.read` (solo UX),
como el grupo "Inventario" con su entrada "Equipos". Las pantallas llevan el título "Auditoría SQL".

| Ruta | Pantalla | Permiso |
|---|---|---|
| `/sql-audit` | Listado de instancias (RF-20) | `sql-audit.read` |
| `/sql-audit/instances/:id` | Pestañas "Cuentas" (RF-21) y "Permisos" (RF-23) | `sql-audit.read` (+ `sql-audit.accesses.validate` para las casillas) |
| `/sql-audit/accounts/:id` | Detalle de una cuenta (RF-22) | `sql-audit.read` |

Componentes de Angular Material, sin dependencias nuevas:
- `mat-table` con `mat-sort` y `mat-paginator` en el servidor (las tablas de permisos pueden tener decenas de miles de
  filas); `mat-form-field` de búsqueda y `mat-select` para los filtros; `mat-tab-group` en la instancia.
- Casilla "Válido": `mat-checkbox` en cada fila, con `aria-label` "Marcar como válido: <elemento>". Actualización
  inmediata, reversión si la API falla y `mat-snack-bar` para los errores. Desactivada, con explicación en
  `matTooltip`, sin `sql-audit.accesses.validate` o si el elemento está eliminado.
- Etiqueta de riesgo: `mat-chip` con ícono y texto ("Crítico", "Alto", "General", "Denegado"), con los estilos de §11.1.
- Estados de carga (`mat-progress-bar`), vacío y error (§8.4).

### 11.1 Colores e íconos de estado

Solo roles estándar del esquema de color MD3 del tema, sin colores personalizados ni cambios en los tokens de los
componentes (constitución §6.3). El estado se muestra como ícono + texto dentro de la etiqueta o de la celda. El color
del rol se aplica al ícono y al texto con `var(--mat-sys-…)`, como ya hace el inventario con `var(--mat-sys-error)`.
MD3 solo define un rol semántico, `error`. Por eso, para advertencia y éxito se usan los roles de acento del tema, y el
significado lo dan el ícono y el texto.

| Estado | Rol de color MD3 | Ícono |
|---|---|---|
| Riesgo Crítico | `error` | `error` |
| Riesgo Alto | `tertiary` | `warning` |
| Riesgo General | `on-surface-variant` | `info` |
| Denegado (`DENY`) | `on-surface-variant` | `block` |
| Válido | `primary` | `check_circle` |
| Pendiente de validar | `on-surface-variant` | `pending` |
| Cambió desde la validación | `tertiary` | `change_circle` |
| Eliminado del motor | `outline` | `remove_circle_outline` |
| Instancia en modo `unverified` | `tertiary` | `gpp_maybe` |
| Datos no disponibles | `tertiary` | `warning` |
| Error al cargar o guardar | `error` | `error` |

## 12. Cambios de backend

### 12.1 Endpoints

Todos bajo `/api/v1`, con Problem Details en los errores. `pageSize` máximo 100.

| Método | Ruta | Autorización | Request | Response | Errores |
|---|---|---|---|---|---|
| POST | `/sql-audit-snapshots` | `EnrolledDevice` | Instantánea `sql-audit v1` (§12.2) | `202 { submissionId }` | 400, 401, 413, 429 |
| GET | `/sql-instances?search=&engine=&sort=&page=&pageSize=` | `sql-audit.read` | — | `200 { items: [{ id, device: { id, name, status }, alias, engine, host, port, instanceName, version, lastCollectedAt, accountCount, pendingCount, pendingCriticalCount, hasUnavailable }], total }` | 400, 401, 403 |
| GET | `/sql-instances/{id}` | `sql-audit.read` | — | `200` instancia con bases de datos, partes no disponibles y permisos omitidos (RF-5) | 401, 403, 404 |
| GET | `/sql-instances/{id}/accounts?level=&database=&type=&risk=&validation=&search=&sort=&page=&pageSize=` | `sql-audit.read` | — | `200 { items: [{ id, level, database, name, host, type, enabled, risk, roles, pendingCount, validation: { status, by, at }, fingerprint, firstSeenAt }], total }` | 400, 401, 403, 404 |
| GET | `/sql-instances/{id}/permissions?risk=&validation=&database=&account=&objectType=&search=&sort=&page=&pageSize=` | `sql-audit.read` | — | `200 { items: [elemento], total }` | 400, 401, 403, 404 |
| GET | `/sql-accounts/{id}` | `sql-audit.read` | — | `200` cuenta con atributos, riesgo (propio y heredado), roles y validación | 401, 403, 404 |
| GET | `/sql-accounts/{id}/items?kind=&risk=&validation=&database=&objectType=&page=&pageSize=` | `sql-audit.read` | — | `200 { items: [{ id, kind: role-membership\|permission, securable: { class, database, schema, name, objectType, column }, role, permission, state, grantor, risk, validation, firstSeenAt, removedAt }], total }` | 400, 401, 403, 404 |
| GET | `/sql-accounts/{id}/changes?page=&pageSize=` | `sql-audit.read` | — | `200` registro de cambios e historial de validaciones | 401, 403, 404 |
| PUT | `/sql-accounts/{id}/validation` | `sql-audit.accesses.validate` | `{ fingerprint }` | `200 { status, by, at }` | 400, 401, 403, 404, 409, 422 |
| DELETE | `/sql-accounts/{id}/validation` | `sql-audit.accesses.validate` | — | `204` (idempotente) | 401, 403, 404 |
| PUT | `/sql-access-items/{id}/validation` | `sql-audit.accesses.validate` | — | `200 { status, by, at }` | 401, 403, 404, 422 |
| DELETE | `/sql-access-items/{id}/validation` | `sql-audit.accesses.validate` | — | `204` (idempotente) | 401, 403, 404 |

### 12.2 Contrato `sql-audit v1` (estricto, borrador)

Estructura plana con referencias internas (`ref`), para que el tamaño crezca de forma lineal:

```json
{
  "schemaVersion": 1,
  "submissionId": "3f2c…",
  "collectedAt": "2026-10-11T12:00:00Z",
  "agentVersion": "…",
  "connection": { "alias": "ventas-prod", "engine": "sqlserver|postgresql|mysql|mariadb", "host": "…", "port": 1433, "instanceName": "…|null", "tls": "verify|pinned|unverified" },
  "instance": { "version": "…", "edition": "…|null", "authenticationMode": "mixed|windows|password|unknown" },
  "databases": [{ "name": "…", "state": "online|offline|restoring|…", "ownerRef": "a1|null" }],
  "accounts": [{
    "ref": "a1", "level": "server|database", "database": "…|null", "name": "…", "host": "…|null",
    "type": "sql-login|windows-login|windows-group|entra-login|server-role|database-user|database-role|application-role|user-without-login|pg-role|mysql-account|mysql-role|certificate|other",
    "enabled": true, "locked": false, "createdAt": "…|null", "modifiedAt": "…|null",
    "defaultDatabase": "…|null", "defaultSchema": "…|null", "loginRef": "a7|null", "isOrphaned": false,
    "password": { "policyEnforced": true, "expirationEnforced": false, "expired": false, "validUntil": "…|null" }
  }],
  "roleMemberships": [{ "memberRef": "a1", "roleRef": "a2" }],
  "permissions": [{
    "granteeRef": "a1",
    "securable": { "class": "server|database|schema|object|column|default-privileges", "database": "…|null", "schema": "…|null", "name": "…|null", "objectType": "table|view|procedure|function|sequence|type|…|null", "column": "…|null" },
    "permission": "SELECT", "state": "grant|grant-with-grant-option|deny", "grantorRef": "a3|null"
  }],
  "omitted": { "builtinPublicPermissions": 0 },
  "unavailable": [{ "section": "database:Ventas", "reason": "connection-failed|certificate-invalid|authentication-failed|permission-denied|offline|timeout|error" }]
}
```

- Propiedades desconocidas → `400`; enumeraciones cerradas; todos los campos obligatorios salvo los anulables;
  las referencias deben existir.
- La propiedad se envía como el permiso `OWNERSHIP`, y los atributos de rol de PostgreSQL como permisos de clase
  `server`.
- Límites (borrador, los confirma `security`): cadenas ≤ 256 caracteres y sin caracteres de control; bases de datos
  ≤ 1 000; cuentas ≤ 20 000; pertenencias ≤ 100 000; permisos ≤ 200 000; cuerpo ≤ 16 MB; `collectedAt` no posterior
  a ahora + 5 min.
- El riesgo **no** viaja en el contrato: lo calcula el servidor (RF-15).

### 12.3 Auditoría

Acciones nuevas en `audit_events`:

| Tipo de recurso | Acciones |
|---|---|
| `sql-account` | `sql-audit.account.validated`, `sql-audit.account.unvalidated` |
| `sql-access-item` | `sql-audit.item.validated`, `sql-audit.item.unvalidated` |

Cada evento incluye la instancia, la cuenta, el elemento (permiso y objeto asegurable) y la huella validada.

### 12.4 Configuración

| Variable | Contenido |
|---|---|
| `SqlAudit__ChangeLogRetentionDays` | Retención del registro de cambios y de los recibos de envío: 365 días (P-14). |
| `SqlAudit__SnapshotsPermitLimitPerHour` | Límite de instantáneas por equipo y hora (propuesta: 120, por las 50 conexiones y sus reintentos). |
| `Inventory__TermsVersions` (existente) | Pasa a admitir `1` y `2` en el enrolamiento; la vigente, para instalaciones nuevas, es `2`. |

Ninguna es un secreto. Se documentan en `.env.example` y en README (constitución §4.3).

## 13. Cambios de base de datos (preliminar; los concreta `database`)

Esquema `app`, migración `AddSqlAudit`:

| Tabla | Contenido principal |
|---|---|
| `sql_instances` | PK `id`; FK `device_id`; `alias`, `engine`, `host`, `port`, `instance_name` (único por equipo + motor + *host* + puerto + instancia); `version`, `edition`, `authentication_mode`, `tls_mode`, `last_collected_at`, `last_received_at`, `unavailable jsonb`, `omitted_builtin_permissions`. |
| `sql_snapshot_receipts` | PK `id`; FK `device_id`, `instance_id`; `submission_id` (único por equipo); `collected_at`, `received_at`, `connection_ip`, `processing_ms`, `applied` (falso si era antigua, RF-12). Sirve para la idempotencia y la métrica de RNF-3. |
| `sql_accounts` | PK `id`; FK `instance_id`; `identity_hash char(64)` (único por instancia); `level`, `database_name`, `name`, `host`, `account_type`, `enabled`, `attributes jsonb`, `fingerprint char(64)`, `risk_level`, `inherited_risk_level`, `first_seen_at`, `last_seen_at`, `removed_at`, `validation_status`, `validated_fingerprint`, `validated_by`, `validated_at`. |
| `sql_access_items` | PK `id`; FK `account_id`, `instance_id` (desnormalizada para la vista de permisos); `identity_hash char(64)` (único por cuenta); `kind`, `role_account_id`, `securable_class`, `database_name`, `schema_name`, `object_name`, `object_type`, `column_name`, `permission`, `state`, `grantor`, `risk_level`, `first_seen_at`, `last_seen_at`, `removed_at`, `validation_status`, `validated_by`, `validated_at`; índices `(instance_id, risk_level, validation_status)` y `(account_id, validation_status)`. |
| `sql_access_changes` | PK `id`; FK `instance_id`, `account_id`, `item_id` (anulable); `change_type` (`added`, `removed`, `changed`), `detected_at`, `details jsonb` (valores anterior y posterior, sin secretos); índice `(account_id, detected_at desc)`. |

- Sin cambios de roles: la API ya tiene `SELECT`, `INSERT`, `UPDATE` y `DELETE` en `app` (ADR-0004).
- Purga del registro de cambios y de los recibos con `ExecuteDelete`. El estado actual se conserva mientras exista la
  instancia; los elementos eliminados del motor se conservan durante la misma retención (1 año) y después se purgan.
  Las validaciones quedan en `audit_events`, sin límite (P-14).
- Volumen orientativo con RNF-5: hasta 200 000 filas de `sql_access_items` por instancia.

## 14. Consideraciones de seguridad

### 14.1 Amenazas (STRIDE)

| Amenaza | Mitigación |
|---|---|
| **I** Los datos son un **mapa de las cuentas privilegiadas** de los motores | Permiso propio `sql-audit.read`; sin hashes ni contraseñas (RF-6); sin nombres de cuentas, bases de datos ni objetos en logs (solo ids); manual sin datos reales; retención limitada (P-14). |
| **S/T/I** Con `unverified`: suplantación del motor (datos falsos) o captura de la credencial del agente por un atacante en la red | TLS siempre obligatorio (sin conexiones en claro); `verify` por defecto y `pinned` propuesto como alternativa; `unverified` solo con confirmación expresa, por conexión y nunca automática; cuenta de mínimo privilegio y solo lectura; aviso visible en la interfaz. **Riesgo aceptado por el solicitante** para los servidores sin certificado válido (ADR-0015). |
| **E** La cuenta del agente en el motor se usa para algo más que leer el catálogo | Cuenta dedicada de mínimo privilegio (§10.3); `sql test` avisa de privilegios de más. |
| **I** Robo de las credenciales guardadas en el equipo | DPAPI de máquina o `0600`/`root`; nunca en la API, logs ni argumentos. Riesgo residual: el de cualquier credencial local de un equipo comprometido. En MariaDB, la cuenta mínima podría leer hashes (ADR-0014). |
| **E** Inyección SQL en el agente con nombres devueltos por el motor | Consultas fijas, una conexión por base de datos con el *builder*, sin concatenar (§13.6); pruebas con nombres maliciosos (CA-17). |
| **T** Un equipo declara accesos falsos u oculta alguno | Inherente a un agente; las instancias quedan ligadas al equipo autenticado (RN-1). La revisión periódica por personas es el control. |
| **S** Robo de la API key del equipo | Sin cambios respecto a SPEC-0002 y SPEC-0003: eliminar el equipo la revoca. |
| **D** Envíos grandes o en ráfaga que saturan la API (sin cola) | 16 MB solo en esta ruta; límites por colección; límite de frecuencia por equipo; validación antes de procesar; procesamiento por lotes con tiempo máximo; métrica y criterio de la cola (RNF-3). |
| **R** Validaciones sin rastro | Eventos en la tabla de auditoría *append-only* (SPEC-0001). |
| **T/E** Nombres con contenido malicioso (XSS) | Angular escapa la interpolación; prohibido `innerHTML` y `bypassSecurityTrust*` en la feature; CSP existente (CA-17). |

### 14.2 Datos personales y términos

Los nombres de las cuentas pueden identificar a personas (p. ej. `DOMINIO\jperez`). El solicitante indica que todos
los equipos son corporativos y que las auditorías forman parte de la operación de la compañía y de los acuerdos
contractuales. Los términos v2 (Anexo A) lo recogen. Se recomienda revisión legal, que no bloquea.

### 14.3 Revisión de `security`

Obligatoria para: el límite de 16 MB, los límites del contrato, la cuenta mínima por motor (§10.3), el almacén de
conexiones, ADR-0015 (modo `unverified`) y la actualización de SECURITY.md y del modelo de amenazas.

## 15. Pruebas

| Criterio | Tipo | Prueba prevista |
|---|---|---|
| CA-1, CA-5, CA-6, CA-7 | Unit (agente, *fixtures* por motor) + integración del agente con motores en contenedor (Testcontainers: SQL Server 2019/2022/2025, PostgreSQL 18, MySQL 9 LTS, MariaDB 12 LTS) | Cuentas, roles, permisos por nivel, propiedad, atributos de PostgreSQL; sin columnas de autenticación; omisión de los predeterminados; base de datos sin permiso o fuera de línea. |
| CA-2, CA-3 | Unit (agente) | Sin conexiones no hay conexiones; `sql add`/`update` sin guardar si falla; contraseña oculta, de archivo o variable, rechazada como argumento, ausente en logs, `sql list` y contrato. |
| CA-4 | Integración (agente) + frontend | Motor con certificado autofirmado: rechazo en `verify`, aceptación en `pinned` y en `unverified` tras confirmar; sin confirmación no se guarda; nunca se degrada solo; motor sin TLS rechazado; aviso en la interfaz. |
| CA-8, CA-9, CA-12 | Integración (API + PostgreSQL) | `202`; `submissionId` repetido; instantánea antigua; secuencia de instantáneas con altas, bajas y cambios de atributos; registro de cambios; `409` y `422`. |
| CA-10 | Unit (dominio) | Catálogo por motor: cada ejemplo de §8.6, herencia por roles, desconocido → Alto, `DENY`. |
| CA-11, CA-13 | Integración + frontend | Marcar y desmarcar cuenta, rol y permiso; persistencia; auditoría; contadores; filtros; reversión de la casilla ante error. |
| CA-14 | Integración | `401`/`403` por endpoint; API key en endpoints de usuario y sesión en el del agente; convenciones existentes (catálogo y autorización). |
| CA-15 | Integración | Propiedad desconocida, referencia inexistente, límites, cuerpo de 16 MB + 1 byte (con Kestrel real y la `location` de nginx). |
| CA-16 | Integración | Equipo eliminado → `401`; instancias visibles y marcadas. |
| CA-17 | Frontend + unit (agente) | Nombres con `<script>` como texto; nombres con `'`, `"`, `]` y `;` en bases de datos y objetos de los motores de prueba. |
| CA-18 | Verificación documentada + frontend | Claro y oscuro, escritorio y móvil, teclado; cada estado con su ícono y texto (§11.1); sin colores fuera de `--mat-sys-*`. |
| CA-19 | Unit (agente) | Términos v2 en `install`; `sql add` no pide términos. |
| CA-20 | Revisión + prueba de coherencia del manual (SPEC-0004 RF-7) | Procesos de §17 publicados. |
| Rendimiento | Integración | Instantánea sintética con el volumen de RNF-5 procesada dentro de RNF-2. |
| Contrato | Contrato en ambos lados | *Fixture* `sql-audit.v1.json` compartido por el agente y la API. |

## 16. Plan de implementación (preliminar)

| # | Tarea | Agente | Archivos principales |
|---|---|---|---|
| 1 | ~~ADR-0014 y enmienda 1.3.0 (§13.6, §5)~~ Hecho el 2026-10-11. ~~ADR-0015 y enmienda 2.0.0; nota en ADR-0014~~ Hecho el 2026-10-11 | architecture + security | `specs/architecture/`, `constitution.md`, `agents/` |
| 2 | Migración `AddSqlAudit` | database | `Infrastructure/Persistence/**` |
| 3 | Dominio (cuentas, elementos, validación, catálogo de riesgo) y casos de uso; permisos y auditoría | backend | `Itmas3.Domain/SqlAudit/**`, `Itmas3.Application/SqlAudit/**` |
| 4 | Repositorios, consultas y diferencias por lotes | backend + database | `Itmas3.Infrastructure/SqlAudit/**` |
| 5 | Endpoints, contratos, límites; `location` de 16 MB en nginx | backend + frontend + security | `Itmas3.Api/**`, `frontend/nginx/**` |
| — | **Incremento A**: API verificable con instantáneas de prueba de los cuatro motores | testing | `backend/tests/**` |
| 6 | Módulo SQL del agente: comandos `sql *`, almacén de conexiones, TLS, colectores por motor, términos v2, controladores | device-agent + security | `agent/src/**`, `agent/terms/**`, `agent/tests/**`, `agent/Directory.Packages.props` |
| — | **Incremento B**: agente contra motores en contenedor y la pila local | testing | — |
| 7 | Pantallas: instancias, cuentas, permisos, detalle; casillas y etiquetas de riesgo; navegación | frontend | `frontend/src/app/features/sql-audit/**`, `layout/navigation/**`, `app.routes.ts` |
| 8 | Manual de uso (§17) | architecture | `frontend/public/manual/**` |
| — | **Incremento C**: interfaz completa | testing | `*.spec.ts` |
| 9 | Revisión de seguridad; documentación (README, ARCHITECTURE, SECURITY, DEVELOPMENT, `agent/README.md`, modelo de amenazas) | security + architecture | docs |
| — | **Cierre**: validación con un motor real de cada tipo; spec → Implemented | testing + security | — |

## 17. Manual de uso

| Proceso (`id`) | Acción | Permiso | Pantallas (`routes`) |
|---|---|---|---|
| `preparar-motor-sql` | Nuevo: crear la cuenta de solo lectura del agente en cada motor y preparar TLS | DBA (sin permiso de ITMas) | — |
| `configurar-conexiones-sql` | Nuevo: `itmas-agent sql add/update/list/test/remove`, modos de TLS y sus riesgos | Administrador del equipo (sin permiso de ITMas) | — |
| `consultar-accesos-sql` | Nuevo: instancias, cuentas, permisos, niveles de riesgo y registro de cambios | `sql-audit.read` | `/sql-audit` |
| `validar-accesos-sql` | Nuevo: marcar y desmarcar cuentas, roles y permisos con un clic; qué significa cada estado | `sql-audit.accesses.validate` | `/sql-audit` |
| `instalar-agente` | Cambia: términos v2 y mención del módulo de auditoría SQL | Código del equipo | — |

Sin nombres reales de instancias, cuentas ni servidores (constitución §10.4). Los ejemplos de creación de la cuenta del
agente usan marcadores evidentes y nunca contraseñas reales.

## 18. Trazabilidad (al implementar)

- Decisiones y enmiendas: [ADR-0014](../architecture/adr-0014-database-engine-audit-agent.md) (constitución 1.3.0: §5, §13.4, §13.6) y [ADR-0015](../architecture/adr-0015-database-connections-without-verified-tls.md) (2.0.0: §13.6); `agents/device-agent.md`.
- Commits/PRs: …
- Archivos principales: …
- Pruebas: …
- Manual de uso actualizado: …

## 19. Decisiones y preguntas abiertas

### 19.1 Primera ronda (2026-10-11)

| # | Tema | Decisión del solicitante |
|---|---|---|
| P-1 | Agente | Módulo del agente actual, desactivado por defecto. |
| P-2 | Motores | SQL Server 2019, 2022 y 2025; PostgreSQL 18; MySQL 9 LTS; MariaDB 12 LTS. |
| P-3/P-5 | Ubicación y credenciales | Los motores pueden estar en otros servidores y fuera de un directorio. Una cuenta propia en cada motor para el agente, sin autenticación integrada. Un agente admite **varias conexiones**. |
| P-6 | Cola | **No en el MVP**: procesamiento sencillo; la cola llegará cuando el volumen lo haga necesario. |
| P-7 | Granularidad | Lo más detallado posible: hasta objeto y columna. |
| P-8 | Tamaño | Aumentar el límite. |
| P-10 | Riesgo | **Sí**, etiquetas por color: rojo para `sa`, `sysadmin` y privilegios amplios; naranja para `UPDATE`, `DELETE` y ejecución; azul para permisos generales (los colores se sustituyen en la segunda ronda, P-21). **Validación de cada permiso**. |
| P-16 | Validación masiva | No por ahora. |
| P-17 | Términos | Modificarlos, teniendo en cuenta que los equipos son corporativos y que las auditorías son parte de la operación contractual. |
| §10.7 | Constitución | Enmiendas de §13.4 (MINOR) y §5 (PATCH) aprobadas y aplicadas (1.3.0, ADR-0014). Credenciales locales aceptadas. Sin `USE`. Controladores incluidos en el agente. Configuración solo local. Sin roles. |

### 19.2 Segunda ronda (2026-10-11)

| # | Tema | Decisión del solicitante |
|---|---|---|
| P-9 | Revalidar | Sí: si cambia un atributo de una cuenta validada, o se borra y se vuelve a crear con el mismo nombre, vuelve a pendiente ("Cambió desde la validación"). |
| P-20 | TLS | Hay que poder conectarse a servidores sin TLS y aceptar certificados no válidos → ADR-0015. *Revisado en la tercera ronda: sin conexiones en claro (§19.3).* |
| P-21 | Colores | Se descarta la paleta roja, naranja y azul. MD3 estándar con armonización de color; todo estado de error, advertencia o éxito con un ícono representativo. Los niveles de riesgo se mantienen. *Revisado en la tercera ronda: sin colores personalizados (§19.3).* |
| P-2b | Volumen | Propuesta aceptada: diseñar para RNF-5 y medir con la primera instancia real. |
| P-17b | Términos | No se piden los términos v2 a las instalaciones existentes; solo los aceptan las nuevas. |

### 19.3 Tercera ronda (2026-10-11)

| # | Tema | Decisión del solicitante |
|---|---|---|
| P-26 | ADR-0015 | Aprobado con cambios: no habrá conexiones sin TLS; se admiten `verify`, `pinned` y `unverified`. Constitución 2.0.0. |
| P-26 | ADR-0016 | Descartado: no se crean colores personalizados; se usa lo estándar y recomendado de MD3 (roles del tema, §11.1). El ADR propuesto se retiró. |
| P-22 | Lista de riesgo | De acuerdo con la lista de §8.6; lo no clasificado es Alto (RN-9). |

### 19.4 Cuarta ronda (2026-10-11)

| # | Tema | Decisión del solicitante |
|---|---|---|
| ADR-0015 | Constitución 2.0.0 | Confirmada. |
| §11.1 | Colores | Confirmado: solo roles estándar de MD3. |
| P-24 | Herencia | Una cuenta miembro de un rol crítico se muestra como crítica, indicado como riesgo heredado. |
| P-25 | Vista de permisos | Pestaña "Permisos" por instancia. |
| P-23 | Motor duplicado | Aceptable verlo dos veces en la v1. |
| P-14 | Retención | Registro de cambios: 1 año. Validaciones: sin límite. |
| P-11 | Exportación | Fuera de la v1. |
| P-12 | Cadencia | Una vez al día, junto con el inventario. |
| P-15 | Vista inicial | Solo lo pendiente, con un filtro para ver todo. |
| P-18 | Nombre | **"Auditoría SQL"** (en lugar de "Accesos SQL"). |
| P-19 | Doble control | No: basta un permiso. |

### 19.5 Preguntas abiertas

Ninguna.

## Anexo A — Términos y condiciones (versión 2, borrador)

> Sustituye a la versión 1 de SPEC-0002 en las instalaciones nuevas. Las instalaciones existentes no tienen que volver
> a aceptar términos (P-17b).
> Se versiona en `agent/terms/`. Se recomienda revisión legal, que no bloquea.

```text
TÉRMINOS Y CONDICIONES DE USO DEL AGENTE ITMas (versión 2)

Este agente se instala en equipos de propiedad de la organización o administrados por ella, como parte de la
gestión del inventario tecnológico, la auditoría de accesos y la seguridad de la información que la organización
realiza en el marco de sus acuerdos contractuales y de sus políticas internas. Al instalarlo, usted declara estar
autorizado para instalar software en este equipo y reconoce que:

1. Inventario del equipo. El agente recopilará una vez al día información técnica del equipo: nombre del equipo,
   sistema operativo y versión, aplicaciones instaladas, cuentas de usuario locales y los grupos a los que
   pertenecen, direcciones IP locales, características de memoria, procesador y discos, estado de cifrado de los
   discos y estado del firewall.
2. Dirección IP pública. Para conocer la dirección IP pública del equipo y su ubicación aproximada (país, región y
   ciudad), el agente consultará una vez al día un proveedor externo (ipinfo.io), sin enviarle ningún otro dato del
   equipo.
3. Auditoría de motores de bases de datos. Solo si el administrador del equipo la configura, el agente se conectará,
   con cuentas de solo lectura creadas para este fin, a los motores de bases de datos que se le indiquen, y
   recopilará una vez al día las cuentas y roles definidos en ellos y los permisos que tienen asignados. No accede al
   contenido de las bases de datos ni a contraseñas, y no modifica los motores.
4. La información se enviará de forma cifrada a la plataforma ITMas, que registrará además la dirección IP desde la
   que recibe la conexión.
5. La finalidad es exclusivamente la gestión del inventario tecnológico, la auditoría de accesos y la seguridad de la
   información de la organización. La información no se utilizará para otros fines.
6. El agente no accede al contenido de sus archivos, correos, navegación ni actividad personal, y no permite controlar
   el equipo de forma remota.
7. La información se conservará durante el tiempo necesario para la finalidad indicada y será accesible solo por el
   personal autorizado.
8. Puede desinstalar el agente con el comando "itmas-agent uninstall". Para consultar, actualizar o solicitar la
   supresión de sus datos, contacte con el área de TI de su organización.
```
