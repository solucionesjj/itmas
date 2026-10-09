using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Itmas3.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "devices",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hardware_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    api_key_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    enrolled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    enrolled_from_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    terms_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    terms_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_connection_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    last_report_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_collected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    hostname = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    os_family = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    os_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    os_version = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    agent_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    public_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    country_code = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true),
                    region = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    city = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    system_volume_encryption = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    firewall_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    has_local_admins = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_devices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_reports",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    collected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    connection_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    public_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    country_code = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true),
                    region = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    city = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventory_reports_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "app",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_devices_api_key_hash",
                schema: "app",
                table: "devices",
                column: "api_key_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_devices_hardware_id",
                schema: "app",
                table: "devices",
                column: "hardware_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_devices_hostname",
                schema: "app",
                table: "devices",
                column: "hostname");

            migrationBuilder.CreateIndex(
                name: "ix_devices_last_report_id",
                schema: "app",
                table: "devices",
                column: "last_report_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_reports_collected_at",
                schema: "app",
                table: "inventory_reports",
                column: "collected_at");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_reports_device_id_collected_at",
                schema: "app",
                table: "inventory_reports",
                columns: new[] { "device_id", "collected_at" },
                descending: new[] { false, true });

            migrationBuilder.AddForeignKey(
                name: "fk_devices_inventory_reports_last_report_id",
                schema: "app",
                table: "devices",
                column: "last_report_id",
                principalSchema: "app",
                principalTable: "inventory_reports",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_devices_inventory_reports_last_report_id",
                schema: "app",
                table: "devices");

            migrationBuilder.DropTable(
                name: "inventory_reports",
                schema: "app");

            migrationBuilder.DropTable(
                name: "devices",
                schema: "app");
        }
    }
}
