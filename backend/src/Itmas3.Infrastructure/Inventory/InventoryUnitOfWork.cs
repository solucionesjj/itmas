using System.Diagnostics;
using System.Text.Json;
using Itmas3.Application.Abstractions.Identity;
using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Application.Inventory.Abstractions;
using Itmas3.Infrastructure.Persistence;
using Itmas3.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Itmas3.Infrastructure.Inventory;

/// <summary>
/// Transactional boundary of inventory operations: the use case changes and its audit entries are committed together.
/// Unlike authorization administration it neither locks nor bumps the authorization version (no privileges change).
/// Concurrent changes to the same device (xmin) and unique-index races are reported as conflicts (409).
/// </summary>
internal sealed class InventoryUnitOfWork(AppDbContext db, ICurrentUser currentUser, TimeProvider timeProvider) : IInventoryUnitOfWork
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T> ExecuteAsync<T>(Func<AuditTrail, CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        try
        {
            var strategy = db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async ct =>
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
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConflictException("El equipo fue modificado por otra operación. Vuelve a intentarlo.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Ya existe un equipo con esos datos.", exception);
        }
    }

    /// <summary>The signed-in user for operations made from the UI; the system otherwise (agents set their own actor).</summary>
    private AuditActor CurrentActor() =>
        currentUser.ApplicationUserId is { } userId
            ? new AuditActor(userId, currentUser.DisplayName ?? currentUser.Username ?? "Desconocido")
            : AuditActor.System;
}
