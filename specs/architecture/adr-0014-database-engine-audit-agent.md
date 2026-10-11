---
id: ADR-0014
title: Auditoría de motores de bases de datos desde el agente (enmienda 1.3.0)
status: Accepted
date: 2026-10-11
deciders: [architecture, security]
related: [SPEC-0005, SPEC-0002, ADR-0009, ADR-0010]
---

# ADR-0014 — Auditoría de motores de bases de datos desde el agente

## Contexto
SPEC-0005 pide que el agente de dispositivo (SPEC-0002, ADR-0010) recolecte cada día los usuarios y permisos de los
motores SQL de la organización:

- Microsoft SQL Server 2019, 2022 y 2025.
- PostgreSQL 18.
- MySQL 9 LTS.
- MariaDB 12 LTS.

El solicitante decidió (SPEC-0005 §19.1):

- Un mismo agente audita **varios motores**, que pueden estar **en otros servidores**, y esos servidores pueden no
  pertenecer a un directorio.
- Cada motor tiene una **cuenta propia para el agente**; no se usa autenticación integrada.
- El agente lleva consigo todo lo que necesita para conectarse: no hay que instalar clientes en el equipo.
- La lista de motores se configura en el propio equipo, porque el agente no acepta órdenes del servidor (§13.3).

La constitución 1.2.0 no lo permite:

- §13.4 limita al agente privilegiado a "leer el sistema y ejecutar los comandos del SO". Conectarse a un motor
  remoto con un controlador no es ninguna de las dos cosas.
- §5.1 dice que PostgreSQL es la única base de datos y que el acceso desde .NET se hace con EF Core. Se escribió para
  la persistencia de ITMas, pero su texto también abarcaría las consultas del agente a SQL Server o MySQL.
- §13.5 exige justificar en un ADR cada dependencia externa del agente en tiempo de ejecución.

## Decisión
1. **Enmienda MINOR (1.3.0), nueva §13.6.** El agente puede conectarse, solo para leer, a motores de bases de datos
   locales o remotos que configure el administrador del equipo. Es una ampliación acotada a la lectura del catálogo de
   seguridad, con los controles de los puntos 3 a 6. No relaja ninguna otra regla de §13.
2. **Enmienda PATCH, alcance de §5.** §5 regula la persistencia de la aplicación. Los motores que audita el agente no
   son bases de datos de ITMas y se rigen por §13.6.
3. **Conexiones configuradas en el equipo.**
   - El administrador del equipo crea una o varias conexiones con comandos locales que exigen privilegios de
     administrador (`itmas-agent sql add|list|test|remove`).
   - Cada conexión tiene un alias, el motor, el *host*, el puerto o la instancia, el usuario, la contraseña y las
     opciones de TLS del punto 5.
   - El agente compone la cadena de conexión con el *connection string builder* de cada controlador. No admite cadenas
     de conexión libres, porque podrían desactivar el cifrado o la validación del certificado.
   - Límite: 50 conexiones por agente.
4. **Credenciales.**
   - Cada conexión usa una cuenta del motor dedicada al agente, con el mínimo privilegio que permita leer el catálogo
     de seguridad (SPEC-0005 §10.3). Nunca `sysadmin`, superusuario, `root` ni propietario de bases de datos.
   - Es un secreto propio de esa conexión: lo introduce el administrador en el equipo y no es un secreto compartido
     distribuido a los equipos (§13.2).
   - La contraseña se pide oculta, o se lee de un archivo o de una variable de entorno, nunca como argumento (§4.5).
   - Se guarda como la API key del dispositivo: cifrada con DPAPI de ámbito máquina en Windows, y en `0600` con
     propietario `root` en Linux y macOS.
   - Nunca se envía a la API ni se escribe en logs. `sql list` no la muestra.
   - `sql test` comprueba la conexión y la lectura del catálogo, y avisa si la cuenta tiene privilegios de más.
5. **Protección del canal.** *Sustituido por [ADR-0015](adr-0015-database-connections-without-verified-tls.md) (constitución 2.0.0): siempre TLS, con los modos `verify`, `pinned` y `unverified`; sin conexiones en claro, tampoco al propio equipo.* Decisión original:
   - **Conexiones remotas:** TLS obligatorio, con validación del certificado del servidor contra el almacén de
     confianza del sistema y del nombre de *host*. Como alternativa, el certificado del servidor se puede **fijar**:
     se configura su archivo o su huella SHA-256 en la conexión y se acepta solo ese certificado, lo que admite
     certificados autofirmados provisionados por el DBA.
   - No existe ninguna opción para desactivar la validación (equivalente a `TrustServerCertificate=true`,
     `sslmode=require` sin verificación o `SslMode=Required` sin validación).
   - **Conexiones al propio equipo** (*loopback*): pueden ir sin TLS, porque el tráfico no sale del equipo.
6. **Consultas.**
   - Solo se consultan las vistas del catálogo de seguridad, con texto SQL fijo y de solo lectura. Los nombres de bases
     de datos, esquemas u objetos que devuelve el motor nunca se concatenan en SQL.
   - Para recorrer las bases de datos se abre una conexión por base de datos, indicando su nombre en el *builder*. No
     se usa `USE`.
   - Nunca se leen datos de tablas de usuario, texto de procedimientos, hashes ni contraseñas.
   - Cada conexión se identifica como `ITMas Agent` y declara intención de solo lectura donde exista. Cada consulta
     tiene un tiempo máximo de 30 s, y cada conexión de 5 minutos.
7. **Controladores incluidos en el agente** (§13.5). Van dentro del ejecutable *self-contained*: no hay que instalar
   clientes en el equipo.

   | Motor | Paquete | Licencia |
   |---|---|---|
   | SQL Server | `Microsoft.Data.SqlClient` | MIT |
   | PostgreSQL | `Npgsql` | PostgreSQL License |
   | MySQL y MariaDB | `MySqlConnector` | MIT |

   En la implementación se verifica:
   - el árbol de dependencias transitivas (sin paquetes de autenticación en la nube que no se usen, si el controlador
     lo permite) y NuGet Audit;
   - que las bibliotecas nativas de `Microsoft.Data.SqlClient` funcionan en el ejecutable de un solo archivo de los
     cinco RID;
   - el aumento de tamaño de los paquetes.

## Alternativas consideradas
| Alternativa | A favor | En contra |
|---|---|---|
| Autenticación integrada (Windows, Kerberos, `peer`) | Sin secretos en el equipo | No funciona con motores remotos fuera de un directorio (decisión del solicitante). |
| Cadenas de conexión libres | Flexibilidad total | Permitirían desactivar TLS o la validación del certificado y mezclar opciones inseguras. |
| Permitir certificados no confiables en conexiones remotas | Funciona con cualquier motor sin preparación | Expone la contraseña del agente a un MITM en la red interna. Fijar el certificado cubre el caso autofirmado sin ese riesgo. |
| Usar los clientes del SO (`sqlcmd`, `psql`, `mysql`) como comandos | Encaja en el texto actual de §13.4 | Obliga a instalarlos en cada equipo (contra la decisión del solicitante), la contraseña pasaría por argumentos o variables del proceso y habría que interpretar salidas de texto. |
| Un agente aparte solo para SQL | Aísla los controladores del agente de inventario | Duplica el enrolamiento, el servicio y el empaquetado (SPEC-0005 §10.1, descartado por el solicitante). |

## Consecuencias
- **Positivas:**
  - Un solo agente audita todos los motores que alcance, sin instalar clientes.
  - El canal y la credencial quedan protegidos con las mismas reglas que la API key.
- **Negativas / riesgos:**
  - El agente guarda credenciales de motores. Un equipo comprometido las expone. Se mitiga con cuentas de mínimo
    privilegio, solo lectura de metadatos y protección del SO.
  - En MariaDB, la cuenta mínima puede llegar a leer los hashes de contraseñas, porque `mysql.global_priv` guarda la
    autenticación dentro de un JSON que no admite permisos por columna. El agente nunca los consulta. Esta
    limitación se documenta en la guía del DBA.
  - Los motores remotos sin TLS no se pueden auditar hasta que el DBA lo active. SQL Server sin certificado
    provisionado genera uno autofirmado nuevo en cada reinicio, que no sirve para fijarlo: hace falta provisionar un
    certificado.
  - El paquete del agente crece por los controladores.
- **Seguimiento:**
  - La política de TLS se revisó con el solicitante: ADR-0015.
  - Revisar los privilegios mínimos al añadir versiones nuevas de los motores.

## Impacto en la constitución
- **MINOR (1.3.0):** nueva §13.6, auditoría de motores de bases de datos; §13.4 remite a ella.
- **PATCH:** §5 indica que regula la persistencia de la aplicación.
