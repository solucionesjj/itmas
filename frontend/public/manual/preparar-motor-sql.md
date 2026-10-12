# Preparar un motor SQL para la auditoría

Para auditar un motor, el agente necesita una **cuenta propia, de solo lectura**, que le permita leer el catálogo de
seguridad (cuentas, roles y permisos) y nada más. El agente nunca lee datos de las tablas, contraseñas ni hashes, y no
modifica el motor. Además, la conexión va siempre cifrada, así que el motor debe aceptar TLS.

**Quién puede hacerlo:** el DBA o administrador del motor (no necesita permisos en ITMas).

## Antes de empezar

- Elige una contraseña larga y única para la cuenta del agente. Entrégala al administrador del equipo donde está el
  agente por un canal seguro; no la envíes por correo ni la pegues en tickets.
- En los ejemplos, `itmas_agent`, `<contraseña>`, `<base>` y `<equipo-del-agente>` son marcadores: sustitúyelos.
- No uses nunca una cuenta con privilegios de administración (`sa`, `sysadmin`, superusuario, `root`, propietario de
  bases de datos).

## Pasos

### 1. Crear la cuenta del agente

**SQL Server 2019, 2022 y 2025**

```sql
CREATE LOGIN itmas_agent WITH PASSWORD = '<contraseña>', CHECK_POLICY = ON;
GRANT CONNECT ANY DATABASE TO itmas_agent;
GRANT VIEW ANY DEFINITION TO itmas_agent;
```

Sin roles de servidor ni usuarios en las bases de datos.

**PostgreSQL 18**

```sql
CREATE ROLE itmas_agent LOGIN PASSWORD '<contraseña>';
```

Basta el `CONNECT` que `PUBLIC` tiene por defecto en cada base de datos. Si una base lo revoca, concédeselo a la cuenta
(`GRANT CONNECT ON DATABASE <base> TO itmas_agent;`); si no, esa base aparecerá en ITMas como no disponible. Sin
superusuario ni roles predefinidos. En `pg_hba.conf`, permite la cuenta solo con `hostssl` desde el equipo del agente.

**MySQL 9 LTS**

```sql
CREATE USER 'itmas_agent'@'<equipo-del-agente>' IDENTIFIED BY '<contraseña>' REQUIRE SSL;
GRANT SHOW DATABASES ON *.* TO 'itmas_agent'@'<equipo-del-agente>';
GRANT SELECT ON mysql.role_edges TO 'itmas_agent'@'<equipo-del-agente>';
GRANT SELECT ON mysql.global_grants TO 'itmas_agent'@'<equipo-del-agente>';
GRANT SELECT ON mysql.db TO 'itmas_agent'@'<equipo-del-agente>';
GRANT SELECT ON mysql.tables_priv TO 'itmas_agent'@'<equipo-del-agente>';
GRANT SELECT ON mysql.columns_priv TO 'itmas_agent'@'<equipo-del-agente>';
GRANT SELECT ON mysql.procs_priv TO 'itmas_agent'@'<equipo-del-agente>';
-- En mysql.user, solo las columnas que no son de autenticación:
GRANT SELECT (Host, User, Select_priv, Insert_priv, Update_priv, Delete_priv, Create_priv, Drop_priv, Reload_priv,
  Shutdown_priv, Process_priv, File_priv, Grant_priv, References_priv, Index_priv, Alter_priv, Show_db_priv, Super_priv,
  Create_tmp_table_priv, Lock_tables_priv, Execute_priv, Repl_slave_priv, Repl_client_priv, Create_view_priv,
  Show_view_priv, Create_routine_priv, Alter_routine_priv, Create_user_priv, Event_priv, Trigger_priv,
  Create_tablespace_priv, Create_role_priv, Drop_role_priv, account_locked, password_expired, password_last_changed,
  password_lifetime) ON mysql.user TO 'itmas_agent'@'<equipo-del-agente>';
```

`SHOW DATABASES` es necesario para ver todas las bases de datos, no solo aquellas a las que la cuenta ya tiene acceso.
La cuenta no puede leer las columnas de autenticación (`authentication_string`, `User_attributes`).

**MariaDB 12 LTS**

```sql
CREATE USER 'itmas_agent'@'<equipo-del-agente>' IDENTIFIED BY '<contraseña>' REQUIRE SSL;
GRANT SHOW DATABASES ON *.* TO 'itmas_agent'@'<equipo-del-agente>';
GRANT SELECT ON mysql.global_priv TO 'itmas_agent'@'<equipo-del-agente>';
GRANT SELECT ON mysql.roles_mapping TO 'itmas_agent'@'<equipo-del-agente>';
GRANT SELECT ON mysql.db TO 'itmas_agent'@'<equipo-del-agente>';
GRANT SELECT ON mysql.tables_priv TO 'itmas_agent'@'<equipo-del-agente>';
GRANT SELECT ON mysql.columns_priv TO 'itmas_agent'@'<equipo-del-agente>';
GRANT SELECT ON mysql.procs_priv TO 'itmas_agent'@'<equipo-del-agente>';
```

En MariaDB, la tabla `mysql.global_priv` guarda la autenticación junto con los privilegios y no admite permisos por
columna: la cuenta del agente podría leer los hashes de las contraseñas, aunque el agente nunca los consulta. Protege
la contraseña de esta cuenta como la de una cuenta sensible.

El comando `itmas-agent sql test <alias>` indica si falta algún permiso o si la cuenta tiene privilegios de más.

### 2. Activar TLS en el motor

La conexión del agente va siempre cifrada. Por orden de preferencia:

1. Instala en el motor un **certificado emitido por una autoridad de confianza** para su nombre de *host*. El agente lo
   validará sin más (modo `verify`).
2. Si usas un **certificado autofirmado**, entrega su archivo (o su huella SHA-256) al administrador del equipo para
   fijarlo (modo `pinned`).
3. Solo si no es posible ninguna de las anteriores, el administrador del equipo puede aceptar el riesgo de no validar
   el certificado (modo `unverified`, ver [Configurar las conexiones SQL del agente](configurar-conexiones-sql)).

En SQL Server, sin un certificado instalado el motor genera uno autofirmado nuevo en cada reinicio, que no sirve para
fijarlo: instala un certificado.

## Resultado

El administrador del equipo ya puede añadir la conexión en el agente con `itmas-agent sql add`.

## Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| El agente dice que el motor no acepta conexiones cifradas | Activa TLS en el motor (paso 2). |
| El agente avisa de privilegios de más | Retira a la cuenta del agente cualquier rol o permiso que no esté en el paso 1. |
| Una base de datos aparece como "no disponible" en ITMas | La cuenta del agente no puede conectarse a ella (por ejemplo, falta `CONNECT` en PostgreSQL) o está fuera de línea. |
