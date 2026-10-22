using System.Diagnostics;
using System.Text.Json;
using Itmas3.Application.Abstractions.Identity;
using Itmas3.Application.Agenda.Abstractions;
using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Infrastructure.Persistence;
using Itmas3.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Itmas3.Infrastructure.Agenda;

/// <summary>
/// Transactional boundary of the agenda operations: the use case changes and its audit entries are committed together.
/// It neither locks nor bumps the authorization version (no privileges change). Concurrent changes to the same event or type
/// (xmin) and unique-index races (a repeated name) are reported as conflicts (409), except the race of a new tag: two people
/// using the same new tag at once is not an error (SPEC-0006 RN-15), so the operation runs again and reuses it.
/// </summary>
internal sealed class AgendaUnitOfWork(AppDbContext db, ICurrentUser currentUser, TimeProvider timeProvider) : IAgendaUnitOfWork
{
    /// <summary>Unique index on <c>lower(name)</c> of <c>agenda_tags</c> (created by the migration).</summary>
    private const string TagNameIndex = "ux_agenda_tags_name_lower";

    /// <summary>
    /// The first attempt plus the ones lost to other requests creating tags. A lost attempt means another request committed a tag
    /// this one was going to create (tags are never deleted), so a few attempts are plenty; past them the conflict is reported.
    /// </summary>
    private const int MaxAttempts = 4;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T> ExecuteAsync<T>(Func<AuditTrail, CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await RunAsync(work, cancellationToken);
                }
                catch (DbUpdateException exception) when (attempt < MaxAttempts && IsTagNameRace(exception))
                {
                    // Another request committed the same new tag first: run the operation again so that it finds the tag and
                    // reuses it, keeping the spelling of the first one (the transaction of this attempt was rolled back).
                }
            }
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConflictException("La actividad fue modificada por otra operación. Recarga los datos e inténtalo de nuevo.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Ya existe un registro con esos datos. Inténtalo de nuevo.", exception);
        }
    }

    private Task<T> RunAsync<T>(Func<AuditTrail, CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async ct =>
        {
            db.ChangeTracker.Clear();
            var trail = new AuditTrail();

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var result = await work(trail, ct);

            var actor = trail.Actor ?? CurrentActor();
            var now = timeProvider.GetUtcNow();
            foreach (var entry in trail.Entries)
            {
                db.AuditEvents.Add(new AuditEventRecord
                {
                    Id = Guid.CreateVersion7(now),
                    OccurredAt = now,
                    ActorUserId = actor.UserId,
                    ActorDisplayName = actor.DisplayName,
                    Action = entry.Action,
                    ResourceType = entry.ResourceType,
                    ResourceId = entry.ResourceId,
                    Result = "success",
                    Before = entry.Before is null ? null : JsonSerializer.Serialize(entry.Before, JsonOptions),
                    After = entry.After is null ? null : JsonSerializer.Serialize(entry.After, JsonOptions),
                    TraceId = Activity.Current?.TraceId.ToString(),
                });
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }, cancellationToken);
    }

    private static bool IsTagNameRace(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: TagNameIndex };

    /// <summary>The signed-in user for operations made from the UI; the system otherwise.</summary>
    private AuditActor CurrentActor() =>
        currentUser.ApplicationUserId is { } userId
            ? new AuditActor(userId, currentUser.DisplayName ?? currentUser.Username ?? "Desconocido")
            : AuditActor.System;
}
