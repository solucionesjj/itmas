using System.Text.Json;
using System.Text.Json.Serialization;
using Itmas3.Application.Agenda;
using Itmas3.Domain.Agenda;
using Itmas3.Domain.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Itmas3.Infrastructure.Persistence.Configurations;

/// <summary>
/// Persistence of the infrastructure agenda (SPEC-0006 §13). Enumerations are stored with their wire codes and guarded by
/// <c>CHECK</c> constraints, the small lists of an event as <c>jsonb</c>, local times as <c>timestamp</c> and instants as
/// <c>timestamptz</c>. The <c>lower(name)</c> unique indexes are created by the migration with constant SQL, as for devices.
/// </summary>
internal sealed class AgendaEventTypeConfiguration : IEntityTypeConfiguration<EventType>
{
    public void Configure(EntityTypeBuilder<EventType> builder)
    {
        builder.ToTable("agenda_event_types", table =>
        {
            table.HasCheckConstraint("ck_agenda_event_types_duration", $"default_duration_minutes BETWEEN 1 AND {EventType.MaxDurationMinutes}");
            table.HasCheckConstraint("ck_agenda_event_types_reminder", $"default_reminder_minutes BETWEEN 0 AND {AgendaEvent.MaxReminderMinutes}");
        });
        builder.HasKey(type => type.Id);
        builder.Property(type => type.Id).ValueGeneratedNever();
        builder.Property(type => type.Name).HasMaxLength(EventType.MaxNameLength).IsRequired();
        builder.Property(type => type.Description).HasMaxLength(EventType.MaxDescriptionLength);
        // Revision is the user-facing ETag; xmin protects every write.
        builder.Property<uint>("RowVersion").IsRowVersion();
        // Also created in the migration: unique lower(name) (ux_agenda_event_types_name_lower).
    }
}

internal sealed class AgendaSeriesConfiguration : IEntityTypeConfiguration<EventSeries>
{
    public void Configure(EntityTypeBuilder<EventSeries> builder)
    {
        builder.ToTable("agenda_series");
        builder.HasKey(series => series.Id);
        builder.Property(series => series.Id).ValueGeneratedNever();
        builder.Property(series => series.Pattern)
            .HasConversion(AgendaConverters.Json<RecurrencePattern>(), AgendaConverters.JsonComparer<RecurrencePattern>())
            .HasColumnType("jsonb")
            .IsRequired();
        builder.HasOne<EventType>().WithMany().HasForeignKey(series => series.TypeId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AgendaTagConfiguration : IEntityTypeConfiguration<AgendaTag>
{
    public void Configure(EntityTypeBuilder<AgendaTag> builder)
    {
        builder.ToTable("agenda_tags");
        builder.HasKey(tag => tag.Id);
        builder.Property(tag => tag.Id).ValueGeneratedNever();
        builder.Property(tag => tag.Name).HasMaxLength(TagName.MaxLength).IsRequired();
        // Also created in the migration: unique lower(name) (ux_agenda_tags_name_lower).
    }
}

internal sealed class AgendaEventConfiguration : IEntityTypeConfiguration<AgendaEvent>
{
    public void Configure(EntityTypeBuilder<AgendaEvent> builder)
    {
        builder.ToTable("agenda_events", table =>
        {
            table.HasCheckConstraint("ck_agenda_events_status", "status IN ('draft', 'scheduled', 'completed', 'cancelled')");
            table.HasCheckConstraint("ck_agenda_events_impact", "impact IN ('none', 'degradation', 'outage')");
            table.HasCheckConstraint("ck_agenda_events_outlook_status", "outlook_status IN ('not-published', 'pending', 'published', 'error')");
            table.HasCheckConstraint(
                "ck_agenda_events_completion_result",
                "completion_result IS NULL OR completion_result IN ('succeeded', 'partial', 'failed', 'not-performed')");
            table.HasCheckConstraint("ck_agenda_events_end_after_start", "ends_at_utc > starts_at_utc AND ends_at_local > starts_at_local");
            table.HasCheckConstraint("ck_agenda_events_reminder", $"reminder_minutes BETWEEN 0 AND {AgendaEvent.MaxReminderMinutes}");
            table.HasCheckConstraint("ck_agenda_events_cancel_reason", "status <> 'cancelled' OR cancel_reason IS NOT NULL");
            table.HasCheckConstraint("ck_agenda_events_completion", "(status = 'completed') = (completion_result IS NOT NULL)");
        });
        builder.HasKey(agendaEvent => agendaEvent.Id);
        builder.Property(agendaEvent => agendaEvent.Id).ValueGeneratedNever();

        builder.Property(agendaEvent => agendaEvent.Title).HasMaxLength(AgendaEvent.MaxTitleLength).IsRequired();
        builder.Property(agendaEvent => agendaEvent.Description).HasMaxLength(AgendaEvent.MaxDescriptionLength);
        builder.Property(agendaEvent => agendaEvent.Status).HasConversion(AgendaConverters.Status).HasMaxLength(16).IsRequired();

        // Local wall-clock time of the organization zone, the zone itself and the UTC instants (RNF-4).
        builder.Property(agendaEvent => agendaEvent.StartsAtLocal).HasColumnType("timestamp without time zone");
        builder.Property(agendaEvent => agendaEvent.EndsAtLocal).HasColumnType("timestamp without time zone");
        builder.Property(agendaEvent => agendaEvent.TimeZoneId).HasColumnName("time_zone").HasMaxLength(64).IsRequired();

        builder.Property(agendaEvent => agendaEvent.Participants)
            .HasConversion(AgendaConverters.Json<IReadOnlyList<string>>(), AgendaConverters.JsonComparer<IReadOnlyList<string>>())
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(agendaEvent => agendaEvent.Impact).HasConversion(AgendaConverters.Impact).HasMaxLength(16).IsRequired();
        builder.Property(agendaEvent => agendaEvent.Scope).HasMaxLength(AgendaEvent.MaxScopeLength);
        builder.Property(agendaEvent => agendaEvent.Location).HasMaxLength(AgendaEvent.MaxLocationLength);

        builder.Property(agendaEvent => agendaEvent.CancelReason).HasMaxLength(AgendaEvent.MaxCancelReasonLength);
        builder.Property(agendaEvent => agendaEvent.CompletionResult).HasConversion(AgendaConverters.Result).HasMaxLength(16);
        builder.Property(agendaEvent => agendaEvent.CompletionNotes).HasMaxLength(AgendaEvent.MaxNotesLength);
        builder.Property(agendaEvent => agendaEvent.EvidenceLinks)
            .HasConversion(AgendaConverters.Json<IReadOnlyList<EvidenceLink>>(), AgendaConverters.JsonComparer<IReadOnlyList<EvidenceLink>>())
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(agendaEvent => agendaEvent.OutlookStatus).HasConversion(AgendaConverters.Outlook).HasMaxLength(16).IsRequired();
        builder.Property(agendaEvent => agendaEvent.OutlookEventId).HasMaxLength(300);
        builder.Property(agendaEvent => agendaEvent.OutlookWebLink).HasMaxLength(1000);
        builder.Property(agendaEvent => agendaEvent.OutlookErrorCode).HasMaxLength(30);

        // Revision is the user-facing ETag; xmin protects every write.
        builder.Property<uint>("RowVersion").IsRowVersion();

        builder.Ignore(agendaEvent => agendaEvent.Timing);
        builder.Ignore(agendaEvent => agendaEvent.NeedsOutlookSync);
        builder.Ignore(agendaEvent => agendaEvent.OutlookIsCurrent);
        builder.Ignore(agendaEvent => agendaEvent.NeedsOutlookCancellation);

        // Queries of §13.2: ranges, overdue and overlaps, "my activities", and the single Outlook id.
        builder.HasIndex(agendaEvent => agendaEvent.StartsAtUtc);
        builder.HasIndex(agendaEvent => new { agendaEvent.Status, agendaEvent.EndsAtUtc });
        builder.HasIndex(agendaEvent => new { agendaEvent.ResponsibleUserId, agendaEvent.StartsAtUtc });
        builder.HasIndex(agendaEvent => agendaEvent.OutlookEventId).IsUnique().HasFilter("outlook_event_id IS NOT NULL");

        builder.HasOne<EventType>().WithMany().HasForeignKey(agendaEvent => agendaEvent.TypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EventSeries>().WithMany().HasForeignKey(agendaEvent => agendaEvent.SeriesId).OnDelete(DeleteBehavior.Restrict);
        // The agenda domain only keeps the user id; the people are never deleted, so no cascade (§13).
        builder.HasOne<User>().WithMany().HasForeignKey(agendaEvent => agendaEvent.ResponsibleUserId).OnDelete(DeleteBehavior.Restrict);

        // Tags live apart from the event row: editing them changes neither xmin nor the revision (RF-24). Only drafts are
        // deleted (RN-13), so their links cascade; a tag in use cannot be deleted.
        builder.HasMany(agendaEvent => agendaEvent.Tags)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "agenda_event_tags",
                right => right.HasOne<AgendaTag>().WithMany().HasForeignKey("tag_id").OnDelete(DeleteBehavior.Restrict),
                left => left.HasOne<AgendaEvent>().WithMany().HasForeignKey("event_id").OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.ToTable("agenda_event_tags");
                    join.HasKey("event_id", "tag_id");
                    join.HasIndex("tag_id", "event_id");
                });
        builder.Navigation(agendaEvent => agendaEvent.Tags).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Value converters of the agenda: the wire codes of its enumerations and the JSON of its small lists.</summary>
internal static class AgendaConverters
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };

    public static readonly ValueConverter<EventStatus, string> Status =
        new(value => AgendaCodes.ToCode(value), code => AgendaCodes.ParseStatus(code) ?? EventStatus.Draft);

    public static readonly ValueConverter<EventImpact, string> Impact =
        new(value => AgendaCodes.ToCode(value), code => AgendaCodes.ParseImpact(code) ?? EventImpact.None);

    public static readonly ValueConverter<CompletionResult, string> Result =
        new(value => AgendaCodes.ToCode(value), code => AgendaCodes.ParseResult(code) ?? CompletionResult.Succeeded);

    public static readonly ValueConverter<OutlookStatus, string> Outlook =
        new(value => AgendaCodes.ToCode(value), code => AgendaCodes.ParseOutlookStatus(code) ?? OutlookStatus.NotPublished);

    public static ValueConverter<T, string> Json<T>() =>
        new(value => JsonSerializer.Serialize(value, JsonOptions), json => JsonSerializer.Deserialize<T>(json, JsonOptions)!);

    public static ValueComparer<T> JsonComparer<T>() => new(
        (left, right) => JsonSerializer.Serialize(left, JsonOptions) == JsonSerializer.Serialize(right, JsonOptions),
        value => JsonSerializer.Serialize(value, JsonOptions).GetHashCode(StringComparison.Ordinal),
        value => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!);
}
