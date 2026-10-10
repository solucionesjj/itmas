using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Itmas3.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_devices_hardware_id",
                schema: "app",
                table: "devices");

            migrationBuilder.AddColumn<string>(
                name: "agent_version",
                schema: "app",
                table: "inventory_reports",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "unavailable_count",
                schema: "app",
                table: "inventory_reports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "terms_version",
                schema: "app",
                table: "devices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "terms_accepted_at",
                schema: "app",
                table: "devices",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "os_family",
                schema: "app",
                table: "devices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "hostname",
                schema: "app",
                table: "devices",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "hardware_id",
                schema: "app",
                table: "devices",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "enrolled_at",
                schema: "app",
                table: "devices",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "api_key_hash",
                schema: "app",
                table: "devices",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character(64)",
                oldFixedLength: true,
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "agent_version",
                schema: "app",
                table: "devices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                schema: "app",
                table: "devices",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                schema: "app",
                table: "devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "device_type",
                schema: "app",
                table: "devices",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "enrollment_code_hash",
                schema: "app",
                table: "devices",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "enrollment_code_issued_at",
                schema: "app",
                table: "devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name",
                schema: "app",
                table: "devices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "owner_email",
                schema: "app",
                table: "devices",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "revision",
                schema: "app",
                table: "devices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "app",
                table: "devices",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            // xmin is a PostgreSQL system column used as concurrency token: Npgsql emits no SQL for this operation.
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "app",
                table: "devices",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            // SPEC-0003 P-6 / P-15: devices enrolled with the former deployment-wide code become active registry entries.
            // Name = hostname (truncated to fit, with " (n)" for repeated hostnames in enrollment order); type inferred
            // from the reported OS: Linux and Windows Server → server, the rest (Windows 11, macOS) → workstation.
            migrationBuilder.Sql("""
                UPDATE app.devices AS d
                SET status = 'active',
                    created_at = d.enrolled_at,
                    revision = 1,
                    device_type = CASE
                        WHEN d.os_family = 'linux' OR d.os_name ILIKE '%server%' THEN 'server'
                        ELSE 'workstation'
                    END,
                    name = CASE WHEN n.position = 1 THEN n.base ELSE n.base || ' (' || n.position || ')' END
                FROM (
                    SELECT id,
                           left(btrim(hostname), 90) AS base,
                           row_number() OVER (PARTITION BY lower(left(btrim(hostname), 90)) ORDER BY enrolled_at, id) AS position
                    FROM app.devices
                ) AS n
                WHERE d.id = n.id;
                """);

            // RF-15: version and unavailable sections of the reports already stored, taken from their payload.
            migrationBuilder.Sql("""
                UPDATE app.inventory_reports
                SET agent_version = left(coalesce(payload ->> 'agentVersion', ''), 50),
                    unavailable_count = coalesce(jsonb_array_length(payload -> 'unavailable'), 0);
                """);

            // The defaults only served to add the NOT NULL columns to existing rows: the application always sets them.
            migrationBuilder.Sql("""
                ALTER TABLE app.devices
                    ALTER COLUMN created_at DROP DEFAULT,
                    ALTER COLUMN device_type DROP DEFAULT,
                    ALTER COLUMN name DROP DEFAULT,
                    ALTER COLUMN revision DROP DEFAULT,
                    ALTER COLUMN status DROP DEFAULT;
                ALTER TABLE app.inventory_reports
                    ALTER COLUMN agent_version DROP DEFAULT,
                    ALTER COLUMN unavailable_count DROP DEFAULT;
                """);

            // RN-1: device names are unique regardless of case among devices that are not deleted.
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_devices_name_lower ON app.devices (lower(name)) WHERE status <> 'deleted';");

            // RF-4b: the collaborator is the user whose e-mail matches the owner e-mail, ignoring case.
            migrationBuilder.Sql("CREATE INDEX ix_devices_owner_email ON app.devices (owner_email);");
            migrationBuilder.Sql("CREATE INDEX ix_users_email_lower ON app.users (lower(email));");

            migrationBuilder.CreateIndex(
                name: "ix_devices_enrollment_code_hash",
                schema: "app",
                table: "devices",
                column: "enrollment_code_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_devices_hardware_id",
                schema: "app",
                table: "devices",
                column: "hardware_id",
                unique: true,
                filter: "status <> 'deleted'");

            migrationBuilder.CreateIndex(
                name: "ix_devices_status",
                schema: "app",
                table: "devices",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversible only while every device has hardware and enrollment data: pending and deleted devices
            // (SPEC-0003) cannot go back to the SPEC-0002 schema. In a shared environment, fix forward instead.
            migrationBuilder.Sql("DROP INDEX IF EXISTS app.ux_devices_name_lower;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS app.ix_devices_owner_email;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS app.ix_users_email_lower;");

            migrationBuilder.DropIndex(
                name: "ix_devices_enrollment_code_hash",
                schema: "app",
                table: "devices");

            migrationBuilder.DropIndex(
                name: "ix_devices_hardware_id",
                schema: "app",
                table: "devices");

            migrationBuilder.DropIndex(
                name: "ix_devices_status",
                schema: "app",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "agent_version",
                schema: "app",
                table: "inventory_reports");

            migrationBuilder.DropColumn(
                name: "unavailable_count",
                schema: "app",
                table: "inventory_reports");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "app",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "app",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "device_type",
                schema: "app",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "enrollment_code_hash",
                schema: "app",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "enrollment_code_issued_at",
                schema: "app",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "name",
                schema: "app",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "owner_email",
                schema: "app",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "revision",
                schema: "app",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "app",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "app",
                table: "devices");

            migrationBuilder.AlterColumn<string>(
                name: "terms_version",
                schema: "app",
                table: "devices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "terms_accepted_at",
                schema: "app",
                table: "devices",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "os_family",
                schema: "app",
                table: "devices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "hostname",
                schema: "app",
                table: "devices",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "hardware_id",
                schema: "app",
                table: "devices",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "enrolled_at",
                schema: "app",
                table: "devices",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "api_key_hash",
                schema: "app",
                table: "devices",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character(64)",
                oldFixedLength: true,
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "agent_version",
                schema: "app",
                table: "devices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_devices_hardware_id",
                schema: "app",
                table: "devices",
                column: "hardware_id",
                unique: true);
        }
    }
}
