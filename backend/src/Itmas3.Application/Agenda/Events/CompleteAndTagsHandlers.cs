using Itmas3.Application.Abstractions.Identity;
using Itmas3.Application.Agenda.Abstractions;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Domain.Agenda;
using Itmas3.Domain.Common;

namespace Itmas3.Application.Agenda.Events;

/// <summary>An evidence link as informed by the caller (SPEC-0006 RN-10); it is validated by the domain.</summary>
public sealed record EvidenceLinkInput(string? Label, string? Url);

public sealed record CompleteEventCommand(
    Guid EventId,
    CompletionResult Result,
    DateTimeOffset? ExecutedAt,
    string? Notes,
    IReadOnlyList<EvidenceLinkInput>? EvidenceLinks);

/// <summary>
/// Use case: record the execution of a Scheduled event (SPEC-0006 RF-11) and, afterwards, change only its notes and evidence
/// links (RN-9). It does not touch Outlook (RF-17). Once recorded, the result and the execution date cannot change.
/// </summary>
public sealed class CompleteEventHandler(
    IAgendaEventRepository events,
    IAgendaUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public Task<AgendaCommandResult> HandleAsync(CompleteEventCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return unitOfWork.ExecuteAsync(async (audit, ct) =>
        {
            var now = timeProvider.GetUtcNow();
            var actor = AgendaActor.Of(currentUser);
            var agendaEvent = await events.GetAsync(command.EventId, ct) ?? throw new NotFoundException("Agenda event not found.");
            var links = (command.EvidenceLinks ?? []).Select(link => (link.Label, link.Url)).ToList();

            if (agendaEvent.Status == EventStatus.Completed)
            {
                if (command.Result != agendaEvent.CompletionResult || (command.ExecutedAt is { } executedAt && executedAt != agendaEvent.ExecutedAt))
                {
                    throw new DomainException("El resultado y la fecha de ejecución no se pueden cambiar una vez registrados; solo las notas y los enlaces.");
                }

                var before = AgendaAuditViews.OfCompletion(agendaEvent);
                agendaEvent.UpdateCompletion(command.Notes, links, now, actor);
                audit.Record(
                    AgendaAuditActions.EventCompletionUpdated, AgendaAuditActions.EventResourceType, agendaEvent.Id.ToString(), before, AgendaAuditViews.OfCompletion(agendaEvent));
            }
            else
            {
                agendaEvent.Complete(command.Result, command.ExecutedAt, command.Notes, links, now, actor);
                audit.Record(
                    AgendaAuditActions.EventCompleted, AgendaAuditActions.EventResourceType, agendaEvent.Id.ToString(), null, AgendaAuditViews.OfCompletion(agendaEvent));
            }

            return new AgendaCommandResult(agendaEvent.Id, agendaEvent.Revision, agendaEvent.Status, [], OutlookState.Of(agendaEvent));
        }, cancellationToken);
    }
}

public sealed record SetEventTagsCommand(Guid EventId, IReadOnlyList<string>? Tags);

/// <summary>
/// Use case: replace the tags of an event, in any status (SPEC-0006 RF-24). Missing tags are created. It changes neither the
/// revision (ETag) nor Outlook and sends no notice; the last change wins. Audited.
/// </summary>
public sealed class SetEventTagsHandler(
    IAgendaEventRepository events,
    IAgendaUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    AgendaTagResolver resolver)
{
    public Task<IReadOnlyList<string>> HandleAsync(SetEventTagsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return unitOfWork.ExecuteAsync<IReadOnlyList<string>>(async (audit, ct) =>
        {
            var agendaEvent = await events.GetAsync(command.EventId, ct) ?? throw new NotFoundException("Agenda event not found.");
            var before = AgendaAuditViews.TagNames(agendaEvent);

            var tags = await resolver.ResolveAsync(command.Tags, AgendaActor.Of(currentUser), timeProvider.GetUtcNow(), ct);
            agendaEvent.ReplaceTags([.. tags]);

            var after = AgendaAuditViews.TagNames(agendaEvent);
            audit.Record(
                AgendaAuditActions.EventTagsUpdated, AgendaAuditActions.EventResourceType, agendaEvent.Id.ToString(),
                AgendaAuditViews.OfTags(before), AgendaAuditViews.OfTags(after));
            return after;
        }, cancellationToken);
    }
}
