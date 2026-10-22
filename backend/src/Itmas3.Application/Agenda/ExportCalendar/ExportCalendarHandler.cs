using System.Globalization;
using Itmas3.Application.Agenda.Queries;

namespace Itmas3.Application.Agenda.ExportCalendar;

/// <summary>The range (<see cref="From"/> inclusive, <see cref="ToExclusive"/> exclusive, local dates) and the filters of an export.</summary>
public sealed record ExportCalendarQuery(DateOnly From, DateOnly ToExclusive, AgendaFilter Filter);

/// <summary>
/// The exported file: its name, its text (CRLF, to be sent as UTF-8) and how many events it has. When <see cref="TooMany"/> is true more than
/// <see cref="AgendaLimits.MaxResults"/> events matched and nothing was written: a partial file would look complete.
/// </summary>
public sealed record ExportCalendarResult(string FileName, string Content, int Count, bool TooMany);

/// <summary>
/// Use case: export the <b>Scheduled</b> events of a range and filters as an iCalendar file (SPEC-0006 RF-21, CA-19). The status is
/// not a filter of the export: it is always Scheduled, whatever the filter says. It is a query, so it writes no audit entry and calls
/// nothing external (it works with Outlook off, which is what it is for).
/// </summary>
public sealed class ExportCalendarHandler(IAgendaQueries queries, AgendaSettings settings, TimeProvider timeProvider)
{
    public async Task<ExportCalendarResult> HandleAsync(ExportCalendarQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filter = query.Filter with { Status = AgendaStatusFilter.Scheduled };
        var export = await queries.ListExportEventsAsync(query.From, query.ToExclusive, filter, cancellationToken);
        var fileName = FileNameOf(query.From, query.ToExclusive);
        if (export.Truncated)
        {
            return new ExportCalendarResult(fileName, string.Empty, 0, true);
        }

        var content = IcalendarWriter.Write(export.Items, timeProvider.GetUtcNow(), settings.PublicUrl);
        return new ExportCalendarResult(fileName, content, export.Items.Count, false);
    }

    /// <summary>The name of the file, from the dates only (never from user text): <c>agenda-2026-01-01_2026-12-31.ics</c>, the last day inclusive.</summary>
    internal static string FileNameOf(DateOnly from, DateOnly toExclusive) =>
        string.Create(CultureInfo.InvariantCulture, $"agenda-{from:yyyy-MM-dd}_{toExclusive.AddDays(-1):yyyy-MM-dd}.ics");
}
