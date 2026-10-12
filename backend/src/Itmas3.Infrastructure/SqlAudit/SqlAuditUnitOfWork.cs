using System.Diagnostics;
using System.Text.Json;
using Itmas3.Application.Abstractions.Identity;
using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Application.SqlAudit.Abstractions;
using Itmas3.Infrastructure.Persistence;
using Itmas3.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Itmas3.Infrastructure.SqlAudit;

/// <summary>
/// Transactional boundary of the SQL access audit: tracked changes, the set-based updates issued by the stores and the
/// audit entries are committed together. Concurrent snapshots of the same instance or validations of the same element
/// (xmin) and unique-index races are reported as conflicts (409); the agent retries.
/// </summary>
internal sealed class SqlAuditUnitOfWork(AppDbContext db, ICurrentUser currentUser, TimeProvider timeProvider) : ISqlAuditUnitOfWork
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
                // Snapshots add up to hundreds of thousands of entities: detect changes once, on save (RNF-2).
                db.ChangeTracker.AutoDetectChangesEnabled = false;
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

                db.ChangeTracker.DetectChanges();
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                db.ChangeTracker.Clear();
                return result;
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConflictException("Los datos de auditoría SQL fueron modificados por otra operación. Vuelve a intentarlo.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Otra operación registró los mismos datos de auditoría SQL. Vuelve a intentarlo.", exception);
        }
    }

    private AuditActor CurrentActor() =>
        currentUser.ApplicationUserId is { } userId
            ? new AuditActor(userId, currentUser.DisplayName ?? currentUser.Username ?? "Desconocido")
            : AuditActor.System;
}
