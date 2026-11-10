using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Itmas3.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgendaOutlookSentRecipients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "outlook_sent_recipients",
                schema: "app",
                table: "agenda_events",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_agenda_events_outlook_sent_recipients",
                schema: "app",
                table: "agenda_events",
                sql: "outlook_sent_recipients IS NULL OR (outlook_event_id IS NOT NULL AND CASE WHEN jsonb_typeof(outlook_sent_recipients) = 'array' THEN jsonb_array_length(outlook_sent_recipients) > 0 ELSE false END)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_agenda_events_outlook_sent_recipients",
                schema: "app",
                table: "agenda_events");

            migrationBuilder.DropColumn(
                name: "outlook_sent_recipients",
                schema: "app",
                table: "agenda_events");
        }
    }
}
