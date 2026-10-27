-- Privilege assertions of scripts/restore-db.sh (SPEC-0009 RF-5, CA-4). Run with psql -At and the variables app_role and migrator_role;
-- it prints "t" when the restored database keeps the least-privilege model of the application, "f" otherwise.
-- The runtime role of the API keeps DML and nothing else: no DDL, no TRUNCATE, no ownership, no access to the migration history,
-- and no UPDATE or DELETE on the audit table. scripts/test-backup.sh breaks each line on purpose and expects "f".
SELECT bool_and(ok) FROM (VALUES
  (NOT has_schema_privilege(:'app_role', 'app', 'CREATE')),
  (has_schema_privilege(:'app_role', 'app', 'USAGE')),
  (NOT has_schema_privilege(:'app_role', 'app_migrations', 'USAGE')),
  (NOT EXISTS (SELECT 1 FROM pg_tables WHERE schemaname IN ('app', 'app_migrations') AND tableowner <> :'migrator_role')),
  (NOT EXISTS (SELECT 1 FROM pg_tables t WHERE t.schemaname = 'app'
     AND NOT has_table_privilege(:'app_role', quote_ident(t.schemaname) || '.' || quote_ident(t.tablename), 'SELECT'))),
  (NOT EXISTS (SELECT 1 FROM pg_tables t WHERE t.schemaname = 'app'
     AND has_table_privilege(:'app_role', quote_ident(t.schemaname) || '.' || quote_ident(t.tablename), 'TRUNCATE'))),
  (NOT EXISTS (SELECT 1 FROM pg_class c JOIN pg_roles r ON r.oid = c.relowner WHERE r.rolname = :'app_role')),
  (NOT EXISTS (SELECT 1 FROM pg_tables t WHERE t.schemaname = 'app' AND t.tablename = 'audit_events'
     AND (has_table_privilege(:'app_role', quote_ident(t.schemaname) || '.' || quote_ident(t.tablename), 'UPDATE')
       OR has_table_privilege(:'app_role', quote_ident(t.schemaname) || '.' || quote_ident(t.tablename), 'DELETE'))))
) AS v(ok);
