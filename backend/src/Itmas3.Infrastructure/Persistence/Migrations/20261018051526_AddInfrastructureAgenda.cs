using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Itmas3.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInfrastructureAgenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agenda_event_types",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    default_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    default_reminder_minutes = table.Column<int>(type: "integer", nullable: false),
                    is_freeze = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agenda_event_types", x => x.id);
                    table.CheckConstraint("ck_agenda_event_types_duration", "default_duration_minutes BETWEEN 1 AND 86400");
                    table.CheckConstraint("ck_agenda_event_types_reminder", "default_reminder_minutes BETWEEN 0 AND 40320");
                });

            migrationBuilder.CreateTable(
                name: "agenda_tags",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agenda_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agenda_series",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pattern = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agenda_series", x => x.id);
                    table.ForeignKey(
                        name: "fk_agenda_series_agenda_event_types_type_id",
                        column: x => x.type_id,
                        principalSchema: "app",
                        principalTable: "agenda_event_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agenda_events",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    series_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_all_day = table.Column<bool>(type: "boolean", nullable: false),
                    starts_at_local = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ends_at_local = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    starts_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    responsible_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participants = table.Column<string>(type: "jsonb", nullable: false),
                    notify_team = table.Column<bool>(type: "boolean", nullable: false),
                    impact = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    scope = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    reminder_minutes = table.Column<int>(type: "integer", nullable: false),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    completion_result = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    executed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    completion_notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    evidence_links = table.Column<string>(type: "jsonb", nullable: false),
                    outlook_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    outlook_event_id = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    outlook_web_link = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    outlook_synced_revision = table.Column<int>(type: "integer", nullable: true),
                    outlook_error_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    outlook_status_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agenda_events", x => x.id);
                    table.CheckConstraint("ck_agenda_events_cancel_reason", "status <> 'cancelled' OR cancel_reason IS NOT NULL");
                    table.CheckConstraint("ck_agenda_events_completion", "(status = 'completed') = (completion_result IS NOT NULL)");
                    table.CheckConstraint("ck_agenda_events_completion_result", "completion_result IS NULL OR completion_result IN ('succeeded', 'partial', 'failed', 'not-performed')");
                    table.CheckConstraint("ck_agenda_events_end_after_start", "ends_at_utc > starts_at_utc AND ends_at_local > starts_at_local");
                    table.CheckConstraint("ck_agenda_events_impact", "impact IN ('none', 'degradation', 'outage')");
                    table.CheckConstraint("ck_agenda_events_outlook_status", "outlook_status IN ('not-published', 'pending', 'published', 'error')");
                    table.CheckConstraint("ck_agenda_events_reminder", "reminder_minutes BETWEEN 0 AND 40320");
                    table.CheckConstraint("ck_agenda_events_status", "status IN ('draft', 'scheduled', 'completed', 'cancelled')");
                    table.ForeignKey(
                        name: "fk_agenda_events_agenda_event_types_type_id",
                        column: x => x.type_id,
                        principalSchema: "app",
                        principalTable: "agenda_event_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_agenda_events_agenda_series_series_id",
                        column: x => x.series_id,
                        principalSchema: "app",
                        principalTable: "agenda_series",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_agenda_events_users_responsible_user_id",
                        column: x => x.responsible_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agenda_event_tags",
                schema: "app",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agenda_event_tags", x => new { x.event_id, x.tag_id });
                    table.ForeignKey(
                        name: "fk_agenda_event_tags_agenda_events_event_id",
                        column: x => x.event_id,
                        principalSchema: "app",
                        principalTable: "agenda_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_agenda_event_tags_agenda_tags_tag_id",
                        column: x => x.tag_id,
                        principalSchema: "app",
                        principalTable: "agenda_tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agenda_event_tags_tag_id_event_id",
                schema: "app",
                table: "agenda_event_tags",
                columns: new[] { "tag_id", "event_id" });

            migrationBuilder.CreateIndex(
                name: "ix_agenda_events_outlook_event_id",
                schema: "app",
                table: "agenda_events",
                column: "outlook_event_id",
                unique: true,
                filter: "outlook_event_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_agenda_events_responsible_user_id_starts_at_utc",
                schema: "app",
                table: "agenda_events",
                columns: new[] { "responsible_user_id", "starts_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_agenda_events_series_id",
                schema: "app",
                table: "agenda_events",
                column: "series_id");

            migrationBuilder.CreateIndex(
                name: "ix_agenda_events_starts_at_utc",
                schema: "app",
                table: "agenda_events",
                column: "starts_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_agenda_events_status_ends_at_utc",
                schema: "app",
                table: "agenda_events",
                columns: new[] { "status", "ends_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_agenda_events_type_id",
                schema: "app",
                table: "agenda_events",
                column: "type_id");

            migrationBuilder.CreateIndex(
                name: "ix_agenda_series_type_id",
                schema: "app",
                table: "agenda_series",
                column: "type_id");

            // Names are unique ignoring case (SPEC-0006 RF-1, RN-15). Constant DDL, no data: the model cannot express lower(name).
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_agenda_event_types_name_lower ON app.agenda_event_types (lower(name));");
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_agenda_tags_name_lower ON app.agenda_tags (lower(name));");

            // The activity types of SPEC-0006 Annex A.2, with fixed ids. 60 minutes and a reminder of 1 day before (P-16);
            // the freeze lasts 1 day. They are editable in the catalog. Ids and dates are fixed on purpose: never now().
            var seededAt = new DateTimeOffset(2026, 10, 17, 0, 0, 0, TimeSpan.Zero);
            migrationBuilder.InsertData(
                schema: "app",
                table: "agenda_event_types",
                columns: new[] { "id", "name", "description", "default_duration_minutes", "default_reminder_minutes", "is_freeze", "is_active", "created_at", "updated_at", "revision" },
                values: new object[,]
                {
                    { new Guid("019ba5c0-0000-7000-8000-000000000001"), "Mantenimiento y parches de sistema operativo", "Aplicación de parches y actualizaciones del sistema operativo en estaciones y servidores, en ventanas acordadas.", 60, 1440, false, true, seededAt, seededAt, 1 },
                    { new Guid("019ba5c0-0000-7000-8000-000000000002"), "Pruebas de vulnerabilidad", "Escaneo de vulnerabilidades de los activos internos y expuestos, y seguimiento de su remediación.", 60, 1440, false, true, seededAt, seededAt, 1 },
                    { new Guid("019ba5c0-0000-7000-8000-000000000003"), "Prueba de penetración", "Prueba de penetración anual y la que sigue a un cambio significativo.", 60, 1440, false, true, seededAt, seededAt, 1 },
                    { new Guid("019ba5c0-0000-7000-8000-000000000004"), "Auditoría de seguridad", "Auditoría interna del programa anual de seguridad y revisión por la dirección.", 60, 1440, false, true, seededAt, seededAt, 1 },
                    { new Guid("019ba5c0-0000-7000-8000-000000000005"), "Hardening", "Revisión de las líneas base de configuración segura de sistemas y plataformas.", 60, 1440, false, true, seededAt, seededAt, 1 },
                    { new Guid("019ba5c0-0000-7000-8000-000000000006"), "Rotación de claves y secretos", "Cambio de claves, secretos de aplicaciones y cuentas de servicio según la política. Nunca se registra su valor.", 60, 1440, false, true, seededAt, seededAt, 1 },
                    { new Guid("019ba5c0-0000-7000-8000-000000000007"), "Renovación de certificados TLS", "Renovación de los certificados TLS que no se renuevan de forma automática.", 60, 1440, false, true, seededAt, seededAt, 1 },
                    { new Guid("019ba5c0-0000-7000-8000-000000000008"), "Pruebas de respaldo y restauración", "Pruebas de restauración de las copias de respaldo.", 60, 1440, false, true, seededAt, seededAt, 1 },
                    { new Guid("019ba5c0-0000-7000-8000-000000000009"), "Revisión de accesos", "Revisión de las cuentas privilegiadas y de los accesos a los motores SQL.", 60, 1440, false, true, seededAt, seededAt, 1 },
                    { new Guid("019ba5c0-0000-7000-8000-00000000000a"), "Simulacro de continuidad y recuperación", "Simulacro anual de continuidad del negocio y de recuperación ante desastres.", 60, 1440, false, true, seededAt, seededAt, 1 },
                    { new Guid("019ba5c0-0000-7000-8000-00000000000b"), "Congelamiento de cambios", "Periodo en el que no deben programarse cambios: cierres contables o fiscales, fin de año y campañas del negocio.", 1440, 1440, true, true, seededAt, seededAt, 1 },
                    { new Guid("019ba5c0-0000-7000-8000-00000000000c"), "Otro", "Actividad que no encaja en los demás tipos.", 60, 1440, false, true, seededAt, seededAt, 1 },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agenda_event_tags",
                schema: "app");

            migrationBuilder.DropTable(
                name: "agenda_events",
                schema: "app");

            migrationBuilder.DropTable(
                name: "agenda_tags",
                schema: "app");

            migrationBuilder.DropTable(
                name: "agenda_series",
                schema: "app");

            migrationBuilder.DropTable(
                name: "agenda_event_types",
                schema: "app");
        }
    }
}
