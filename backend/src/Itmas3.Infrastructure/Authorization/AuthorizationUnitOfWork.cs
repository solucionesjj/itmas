using System.Diagnostics;
using System.Text.Json;
using Itmas3.Application.Abstractions.Identity;
using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Domain.Common;
using Itmas3.Infrastructure.Persistence;
using Itmas3.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Itmas3.Infrastructure.Authorization;

/// <summary>
/// Transactional boundary of authorization administration (ADR-0008 §5): row lock on <c>authorization_state</c>
/// (serialises all authorization changes), the use case, audit entries and version bump in one transaction.
/// Rejections are rolled back and audited separately.
/// </summary>
internal sealed partial class AuthorizationUnitOfWork(
    AppDbContext db,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<AuthorizationUnitOfWork> logger) : IAuthorizationUnitOfWork
{
    private const string Success = "success";
    private const string Rejected = "rejected";
    private const string Denied = "denied";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T> ExecuteAsync<T>(
        AuditContext context, Func<AuditTrail, CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        AuditTrail? trail = null;
        try
        {
            var strategy = db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async ct =>
            {
                db.ChangeTracker.Clear();
                trail = new AuditTrail();

                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await db.Database.ExecuteSqlAsync($"SELECT 1 FROM app.authorization_state WHERE id = 1 FOR UPDATE", ct);

                var result = await work(trail, ct);

                if (trail.Entries.Count > 0)
                {
                    var actor = trail.Actor ?? CurrentActor();
                    foreach (var entry in trail.Entries)
                    {
                        db.AuditEvents.Add(Record(entry.Action, entry.ResourceType, entry.ResourceId, actor, Success, entry.Before, entry.After));
                    }

                    await db.AuthorizationState
                        .Where(state => state.Id == AuthorizationState.SingletonId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(state => state.Version, state => state.Version + 1), ct);
                }

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return result;
            }, cancellationToken);
        }
        catch (Exception exception) when (Classify(exception) is { } rejection)
        {
            await RecordRejectionAsync(context, trail?.Actor ?? CurrentActor(), rejection.Result, rejection.Exception.Message);
            if (ReferenceEquals(rejection.Exception, exception))
            {
                throw;
            }

            throw rejection.Exception;
        }
    }

    private static (string Result, Exception Exception)? Classify(Exception exception) => exception switch
    {
        ForbiddenException => (Denied, exception),
        DomainException or ConflictException or PreconditionFailedException => (Rejected, exception),
        DbUpdateConcurrencyException => (Rejected, new ConflictException("La información fue modificada por otra operación. Vuelve a intentarlo.", exception)),
        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
            (Rejected, new ConflictException("Ya existe un registro con esos datos.", exception)),
        _ => null,
    };

    private async Task RecordRejectionAsync(AuditContext context, AuditActor actor, string result, string reason)
    {
        try
        {
            db.ChangeTracker.Clear();
            db.AuditEvents.Add(Record(context.Action, context.ResourceType, context.ResourceId, actor, result, null, new { Reason = reason }));
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception exception) when (exception is DbUpdateException or NpgsqlException or InvalidOperationException)
        {
            LogRejectionAuditFailed(logger, exception, context.Action);
        }
    }

    private AuditEventRecord Record(
        string action, string resourceType, string? resourceId, AuditActor actor, string result, object? before, object? after) =>
        new()
        {
            Id = Guid.CreateVersion7(timeProvider.GetUtcNow()),
            OccurredAt = timeProvider.GetUtcNow(),
            ActorUserId = actor.UserId,
            ActorDisplayName = actor.DisplayName,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Result = result,
            Before = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            After = after is null ? null : JsonSerializer.Serialize(after, JsonOptions),
            TraceId = Activity.Current?.TraceId.ToString(),
        };

    private AuditActor CurrentActor() =>
        new(currentUser.ApplicationUserId, currentUser.DisplayName ?? currentUser.Username ?? "Desconocido");

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not write the audit record of a rejected {Action} operation")]
    private static partial void LogRejectionAuditFailed(ILogger logger, Exception exception, string action);
}
