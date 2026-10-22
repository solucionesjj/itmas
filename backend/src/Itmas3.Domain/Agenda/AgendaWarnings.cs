namespace Itmas3.Domain.Agenda;

/// <summary>A non-blocking warning about an activity (SPEC-0006 RN-11), shown next to it but never preventing a save.</summary>
public sealed record AgendaWarning(AgendaWarningKind Kind, Guid EventId, string EventTitle);

/// <summary>The data of another activity needed to detect overlaps (including whether its type is a freeze).</summary>
public sealed record OverlapCandidate(
    Guid Id,
    string Title,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    EventStatus Status,
    EventImpact Impact,
    bool IsFreeze);

/// <summary>Detection of the warnings of SPEC-0006 RN-11. Pure: the caller supplies the activities that may overlap.</summary>
public static class AgendaWarnings
{
    /// <summary>
    /// Warnings of an activity in Draft or Scheduled state that spans <paramref name="startUtc"/> to <paramref name="endUtc"/>:
    /// (a) it is not a freeze and overlaps a scheduled freeze; (b) it has an impact and overlaps another Draft or Scheduled
    /// activity that also has one. Touching ends do not overlap.
    /// </summary>
    public static IReadOnlyList<AgendaWarning> Evaluate(
        Guid? subjectId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        bool subjectIsFreeze,
        EventImpact subjectImpact,
        IEnumerable<OverlapCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var warnings = new List<AgendaWarning>();
        foreach (var other in candidates.OrderBy(candidate => candidate.StartUtc).ThenBy(candidate => candidate.Id))
        {
            if (other.Id == subjectId || !(other.StartUtc < endUtc && startUtc < other.EndUtc))
            {
                continue;
            }

            if (!subjectIsFreeze && other is { IsFreeze: true, Status: EventStatus.Scheduled })
            {
                warnings.Add(new AgendaWarning(AgendaWarningKind.FreezeOverlap, other.Id, other.Title));
            }

            if (subjectImpact != EventImpact.None
                && other.Impact != EventImpact.None
                && other.Status is EventStatus.Draft or EventStatus.Scheduled)
            {
                warnings.Add(new AgendaWarning(AgendaWarningKind.ImpactOverlap, other.Id, other.Title));
            }
        }

        return warnings;
    }
}
