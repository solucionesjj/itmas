using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Itmas3.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSqlAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sql_instances",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    engine = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    host = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    port = table.Column<int>(type: "integer", nullable: true),
                    instance_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    alias = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tls_mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    version = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    edition = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    authentication_mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_collected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    databases = table.Column<string>(type: "jsonb", nullable: false),
                    unavailable = table.Column<string>(type: "jsonb", nullable: false),
                    omitted_builtin_permissions = table.Column<int>(type: "integer", nullable: false),
                    risk_catalog_version = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sql_instances", x => x.id);
                    table.ForeignKey(
                        name: "fk_sql_instances_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "app",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sql_accounts",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    identity_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    database_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    host = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    account_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    locked = table.Column<bool>(type: "boolean", nullable: false),
                    engine_created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    engine_modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    default_database = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    default_schema = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    login_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    is_orphaned = table.Column<bool>(type: "boolean", nullable: false),
                    has_password = table.Column<bool>(type: "boolean", nullable: false),
                    password_policy_enforced = table.Column<bool>(type: "boolean", nullable: true),
                    password_expiration_enforced = table.Column<bool>(type: "boolean", nullable: true),
                    password_expired = table.Column<bool>(type: "boolean", nullable: true),
                    password_valid_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    risk_level = table.Column<short>(type: "smallint", nullable: false),
                    inherited_risk_level = table.Column<short>(type: "smallint", nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    validation_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    validated_fingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    validated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    validated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sql_accounts", x => x.id);
                    table.ForeignKey(
                        name: "fk_sql_accounts_sql_instances_instance_id",
                        column: x => x.instance_id,
                        principalSchema: "app",
                        principalTable: "sql_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sql_snapshot_receipts",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    collected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    connection_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    processing_ms = table.Column<int>(type: "integer", nullable: false),
                    applied = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sql_snapshot_receipts", x => x.id);
                    table.ForeignKey(
                        name: "fk_sql_snapshot_receipts_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "app",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sql_snapshot_receipts_sql_instances_instance_id",
                        column: x => x.instance_id,
                        principalSchema: "app",
                        principalTable: "sql_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sql_access_items",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    identity_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    role_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    securable_class = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    database_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    schema_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    object_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    object_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    column_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    permission = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    state = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    grantor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    risk_level = table.Column<short>(type: "smallint", nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    validation_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    validated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    validated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sql_access_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_sql_access_items_sql_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "app",
                        principalTable: "sql_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sql_access_items_sql_accounts_role_account_id",
                        column: x => x.role_account_id,
                        principalSchema: "app",
                        principalTable: "sql_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sql_access_items_sql_instances_instance_id",
                        column: x => x.instance_id,
                        principalSchema: "app",
                        principalTable: "sql_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sql_access_changes",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    change_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    target = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    detected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    details = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sql_access_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_sql_access_changes_sql_access_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "app",
                        principalTable: "sql_access_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_sql_access_changes_sql_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "app",
                        principalTable: "sql_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sql_access_changes_sql_instances_instance_id",
                        column: x => x.instance_id,
                        principalSchema: "app",
                        principalTable: "sql_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sql_access_changes_account_id_detected_at",
                schema: "app",
                table: "sql_access_changes",
                columns: new[] { "account_id", "detected_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_sql_access_changes_detected_at",
                schema: "app",
                table: "sql_access_changes",
                column: "detected_at");

            migrationBuilder.CreateIndex(
                name: "ix_sql_access_changes_instance_id",
                schema: "app",
                table: "sql_access_changes",
                column: "instance_id");

            migrationBuilder.CreateIndex(
                name: "ix_sql_access_changes_item_id",
                schema: "app",
                table: "sql_access_changes",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_sql_access_items_account_id_identity_hash",
                schema: "app",
                table: "sql_access_items",
                columns: new[] { "account_id", "identity_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sql_access_items_account_id_validation_status",
                schema: "app",
                table: "sql_access_items",
                columns: new[] { "account_id", "validation_status" },
                filter: "removed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sql_access_items_instance_id_risk_level_validation_status",
                schema: "app",
                table: "sql_access_items",
                columns: new[] { "instance_id", "risk_level", "validation_status" },
                filter: "removed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sql_access_items_removed_at",
                schema: "app",
                table: "sql_access_items",
                column: "removed_at");

            migrationBuilder.CreateIndex(
                name: "ix_sql_access_items_role_account_id",
                schema: "app",
                table: "sql_access_items",
                column: "role_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_sql_accounts_instance_id_identity_hash",
                schema: "app",
                table: "sql_accounts",
                columns: new[] { "instance_id", "identity_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sql_accounts_instance_id_name",
                schema: "app",
                table: "sql_accounts",
                columns: new[] { "instance_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_sql_accounts_removed_at",
                schema: "app",
                table: "sql_accounts",
                column: "removed_at");

            migrationBuilder.CreateIndex(
                name: "ix_sql_instances_alias",
                schema: "app",
                table: "sql_instances",
                column: "alias");

            migrationBuilder.CreateIndex(
                name: "ix_sql_instances_device_id_engine_host_port_instance_name",
                schema: "app",
                table: "sql_instances",
                columns: new[] { "device_id", "engine", "host", "port", "instance_name" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_sql_snapshot_receipts_device_id_submission_id",
                schema: "app",
                table: "sql_snapshot_receipts",
                columns: new[] { "device_id", "submission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sql_snapshot_receipts_instance_id",
                schema: "app",
                table: "sql_snapshot_receipts",
                column: "instance_id");

            migrationBuilder.CreateIndex(
                name: "ix_sql_snapshot_receipts_received_at",
                schema: "app",
                table: "sql_snapshot_receipts",
                column: "received_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sql_access_changes",
                schema: "app");

            migrationBuilder.DropTable(
                name: "sql_snapshot_receipts",
                schema: "app");

            migrationBuilder.DropTable(
                name: "sql_access_items",
                schema: "app");

            migrationBuilder.DropTable(
                name: "sql_accounts",
                schema: "app");

            migrationBuilder.DropTable(
                name: "sql_instances",
                schema: "app");
        }
    }
}
