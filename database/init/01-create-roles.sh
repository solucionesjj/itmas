#!/bin/sh
# -----------------------------------------------------------------------------
# PostgreSQL bootstrap: least-privilege roles and application schema.
#
# Executed ONCE by the official postgres image (docker-entrypoint-initdb.d) when
# the data volume is empty. Re-running requires recreating the volume, or
# applying equivalent changes through a reviewed database spec.
#
#   <migrator role>  owns schemas "app" and "app_migrations"; runs EF Core
#                    migrations (DDL). Used only by the one-shot "migrator" container.
#   <app role>       runtime role of the API: SELECT/INSERT/UPDATE/DELETE on the
#                    tables of schema "app". No DDL, no access to other schemas
#                    (including the migration history in "app_migrations").
#
# Passwords are read from environment variables with psql's \getenv, so they
# never appear in command lines or in this file.
# -----------------------------------------------------------------------------
set -eu

: "${POSTGRES_DB:?POSTGRES_DB is required}"
: "${APP_DB_MIGRATOR_USER:?APP_DB_MIGRATOR_USER is required}"
: "${APP_DB_MIGRATOR_PASSWORD:?APP_DB_MIGRATOR_PASSWORD is required}"
: "${APP_DB_USER:?APP_DB_USER is required}"
: "${APP_DB_PASSWORD:?APP_DB_PASSWORD is required}"

psql -v ON_ERROR_STOP=1 --no-psqlrc --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<'EOSQL'
\getenv db_name POSTGRES_DB
\getenv migrator_user APP_DB_MIGRATOR_USER
\getenv migrator_password APP_DB_MIGRATOR_PASSWORD
\getenv app_user APP_DB_USER
\getenv app_password APP_DB_PASSWORD

SET password_encryption = 'scram-sha-256';

CREATE ROLE :"migrator_user" LOGIN PASSWORD :'migrator_password'
    NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
CREATE ROLE :"app_user" LOGIN PASSWORD :'app_password'
    NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;

-- Nobody connects or creates objects unless explicitly allowed.
REVOKE ALL ON DATABASE :"db_name" FROM PUBLIC;
REVOKE ALL ON SCHEMA public FROM PUBLIC;
GRANT CONNECT ON DATABASE :"db_name" TO :"migrator_user", :"app_user";

-- Application schema, owned by the migrator role.
CREATE SCHEMA app AUTHORIZATION :"migrator_user";
GRANT USAGE ON SCHEMA app TO :"app_user";

-- Migration history: private to the migrator, so the runtime role cannot tamper with it.
CREATE SCHEMA app_migrations AUTHORIZATION :"migrator_user";

-- Objects created by the migrator in the future are usable (DML only) by the runtime role.
ALTER DEFAULT PRIVILEGES FOR ROLE :"migrator_user" IN SCHEMA app
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO :"app_user";
ALTER DEFAULT PRIVILEGES FOR ROLE :"migrator_user" IN SCHEMA app
    GRANT USAGE, SELECT ON SEQUENCES TO :"app_user";

ALTER ROLE :"migrator_user" SET search_path = app;
ALTER ROLE :"app_user" SET search_path = app;
EOSQL

echo "Least-privilege roles and schema 'app' created."
