using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Domain.Authorization;

namespace Itmas3.Application.Authorization.Users.ProvisionUser;

/// <summary>Identity data received from the identity provider after a successful authentication.</summary>
public sealed record SignInIdentity(string Provider, string Issuer, string Subject, string? DisplayName, string? Email, bool IsGuest);

public enum SignInRejection
{
    UserDisabled,
    GuestNotAllowed,
}

public sealed record ProvisioningResult(Guid? UserId, string? SecurityStamp, SignInRejection? Rejection)
{
    public bool Succeeded => Rejection is null;
}

/// <summary>
/// Use case: register the user on first sign-in (no policies, RF-1), refresh profile data on later sign-ins,
/// refuse disabled users and guests (RF-2, P2) and apply the Root bootstrap (RF-17).
/// </summary>
public sealed class ProvisionUserHandler(
    IUserRepository users,
    IAuthorizationUnitOfWork unitOfWork,
    AuthorizationSettings settings,
    TimeProvider timeProvider)
{
    public async Task<ProvisioningResult> HandleAsync(SignInIdentity identity, CancellationToken cancellationToken)
    {
        if (identity.IsGuest && !settings.AllowGuestUsers)
        {
            return new ProvisioningResult(null, null, SignInRejection.GuestNotAllowed);
        }

        var context = new AuditContext(AuditActions.UserProvisioned, AuditResourceTypes.User, null);

        return await unitOfWork.ExecuteAsync(context, async (audit, ct) =>
        {
            var now = timeProvider.GetUtcNow();
            var user = await users.FindByExternalIdentityAsync(identity.Provider, identity.Issuer, identity.Subject, ct);

            if (user is null)
            {
                user = User.Provision(identity.Provider, identity.Issuer, identity.Subject, identity.DisplayName, identity.Email, now);
                users.Add(user);
                audit.Actor = new AuditActor(user.Id, user.DisplayName);
                audit.Record(AuditActions.UserProvisioned, AuditResourceTypes.User, user.Id.ToString(), null, new
                {
                    user.DisplayName,
                    user.Email,
                    identity.Provider,
                    identity.Issuer,
                    identity.Subject,
                });
            }
            else
            {
                if (!user.IsActive)
                {
                    return new ProvisioningResult(null, null, SignInRejection.UserDisabled);
                }

                user.RecordSignIn(identity.DisplayName, identity.Email, now);
                audit.Actor = new AuditActor(user.Id, user.DisplayName);
            }

            if (settings.BootstrapRootObjectIds.Contains(identity.Subject)
                && !user.IsRoot
                && await users.CountActiveRootsAsync(ct) == 0)
            {
                user.SetRoot(true);
                audit.Record(AuditActions.RootBootstrapped, AuditResourceTypes.User, user.Id.ToString(), new { IsRoot = false }, new { IsRoot = true });
            }

            return new ProvisioningResult(user.Id, user.SecurityStamp, null);
        }, cancellationToken);
    }
}
