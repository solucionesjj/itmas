using Itmas3.Application.Agenda.Abstractions;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Domain.Agenda;
using Itmas3.Domain.Common;

namespace Itmas3.Application.Agenda.Types;

/// <summary>The data of an activity type (SPEC-0006 RF-1).</summary>
public sealed record EventTypeInput(string? Name, string? Description, int DefaultDurationMinutes, int DefaultReminderMinutes, bool IsFreeze);

public sealed record UpdateEventTypeCommand(
    Guid TypeId, int ExpectedRevision, EventTypeInput Input, bool? IsActive);

/// <summary>Use case: create an activity type (RF-2). The name is unique ignoring case; a repeated one is a business error (422, CA-1).</summary>
public sealed class CreateEventTypeHandler(IEventTypeRepository types, IAgendaUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public Task<EventType> HandleAsync(EventTypeInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        return unitOfWork.ExecuteAsync(async (audit, ct) =>
        {
            var type = EventType.Create(
                input.Name, input.Description, input.DefaultDurationMinutes, input.DefaultReminderMinutes, input.IsFreeze, timeProvider.GetUtcNow());
            await EnsureNameAvailableAsync(types, type.Name, null, ct);

            types.Add(type);
            audit.Record(AgendaAuditActions.EventTypeCreated, AgendaAuditActions.EventTypeResourceType, type.Id.ToString(), null, AgendaAuditViews.OfType(type));
            return type;
        }, cancellationToken);
    }

    internal static async Task EnsureNameAvailableAsync(IEventTypeRepository types, string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (await types.NameExistsAsync(name, excludeId, cancellationToken))
        {
            throw new DomainException("Ya existe un tipo de actividad con este nombre.");
        }
    }
}

/// <summary>
/// Use case: modify an activity type and activate or deactivate it, with optimistic concurrency (RF-2, RNF-3). A type is never
/// deleted; events that already use an inactive type keep it.
/// </summary>
public sealed class UpdateEventTypeHandler(IEventTypeRepository types, IAgendaUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public Task<EventType> HandleAsync(UpdateEventTypeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Input);
        return unitOfWork.ExecuteAsync(async (audit, ct) =>
        {
            var type = await types.GetAsync(command.TypeId, ct) ?? throw new NotFoundException("Event type not found.");
            if (type.Revision != command.ExpectedRevision)
            {
                throw new PreconditionFailedException("Otro usuario modificó este tipo de actividad. Recarga los datos e inténtalo de nuevo.");
            }

            var before = AgendaAuditViews.OfType(type);
            var now = timeProvider.GetUtcNow();
            var input = command.Input;
            type.Update(input.Name, input.Description, input.DefaultDurationMinutes, input.DefaultReminderMinutes, input.IsFreeze, command.IsActive ?? type.IsActive, now);
            await CreateEventTypeHandler.EnsureNameAvailableAsync(types, type.Name, type.Id, ct);

            audit.Record(AgendaAuditActions.EventTypeUpdated, AgendaAuditActions.EventTypeResourceType, type.Id.ToString(), before, AgendaAuditViews.OfType(type));
            return type;
        }, cancellationToken);
    }
}
