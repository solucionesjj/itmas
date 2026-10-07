using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Itmas3.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicyBasedAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_events",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    resource_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    before = table.Column<string>(type: "jsonb", nullable: true),
                    after = table.Column<string>(type: "jsonb", nullable: true),
                    trace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "authorization_state",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_authorization_state", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                schema: "app",
                columns: table => new
                {
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_grantable = table.Column<bool>(type: "boolean", nullable: false),
                    is_deprecated = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permissions", x => x.name);
                });

            migrationBuilder.CreateTable(
                name: "policies",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_root = table.Column<bool>(type: "boolean", nullable: false),
                    security_stamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_sign_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "policy_statements",
                schema: "app",
                columns: table => new
                {
                    permission_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    effect = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_policy_statements", x => new { x.policy_id, x.permission_name });
                    table.ForeignKey(
                        name: "fk_policy_statements_permissions_permission_name",
                        column: x => x.permission_name,
                        principalSchema: "app",
                        principalTable: "permissions",
                        principalColumn: "name",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_policy_statements_policies_policy_id",
                        column: x => x.policy_id,
                        principalSchema: "app",
                        principalTable: "policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "policy_assignments",
                schema: "app",
                columns: table => new
                {
                    policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_policy_assignments", x => new { x.user_id, x.policy_id });
                    table.ForeignKey(
                        name: "fk_policy_assignments_policies_policy_id",
                        column: x => x.policy_id,
                        principalSchema: "app",
                        principalTable: "policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_policy_assignments_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_external_identities",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_external_identities", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_external_identities_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "app",
                table: "authorization_state",
                columns: new[] { "id", "version" },
                values: new object[] { (short)1, 1L });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_actor_user_id",
                schema: "app",
                table: "audit_events",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_occurred_at",
                schema: "app",
                table: "audit_events",
                column: "occurred_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_resource_type_resource_id",
                schema: "app",
                table: "audit_events",
                columns: new[] { "resource_type", "resource_id" });

            migrationBuilder.CreateIndex(
                name: "ix_policy_assignments_policy_id",
                schema: "app",
                table: "policy_assignments",
                column: "policy_id");

            migrationBuilder.CreateIndex(
                name: "ix_policy_statements_permission_name",
                schema: "app",
                table: "policy_statements",
                column: "permission_name");

            migrationBuilder.CreateIndex(
                name: "ix_user_external_identities_provider_issuer_subject",
                schema: "app",
                table: "user_external_identities",
                columns: new[] { "provider", "issuer", "subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_external_identities_user_id",
                schema: "app",
                table: "user_external_identities",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_is_root",
                schema: "app",
                table: "users",
                column: "is_root");
            // Policy names are unique regardless of case (SPEC-0001 RN-7).
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_policies_name_lower ON app.policies (lower(name));");

            // Append-only audit trail (SPEC-0001 RNF-3 / CA-23): the table owner (migrator) revokes UPDATE, DELETE and
            // TRUNCATE from every other role that received privileges (e.g. through the schema default privileges).
            migrationBuilder.Sql("""
                DO $$
                DECLARE grantee_role text;
                BEGIN
                    FOR grantee_role IN
                        SELECT DISTINCT grantee FROM information_schema.role_table_grants
                        WHERE table_schema = 'app' AND table_name = 'audit_events' AND grantee <> current_user
                    LOOP
                        EXECUTE format('REVOKE UPDATE, DELETE, TRUNCATE ON app.audit_events FROM %I', grantee_role);
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS app.ux_policies_name_lower;");

            migrationBuilder.DropTable(
                name: "audit_events",
                schema: "app");

            migrationBuilder.DropTable(
                name: "authorization_state",
                schema: "app");

            migrationBuilder.DropTable(
                name: "policy_assignments",
                schema: "app");

            migrationBuilder.DropTable(
                name: "policy_statements",
                schema: "app");

            migrationBuilder.DropTable(
                name: "user_external_identities",
                schema: "app");

            migrationBuilder.DropTable(
                name: "permissions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "policies",
                schema: "app");

            migrationBuilder.DropTable(
                name: "users",
                schema: "app");
        }
    }
}
