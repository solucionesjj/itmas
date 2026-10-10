using Itmas3.Application.Abstractions.Authorization;
using Itmas3.Application.Authorization;
using Itmas3.Application.Authorization.Permissions.SynchronizeCatalog;
using Itmas3.Application.Authorization.Permissions.UpdatePermission;
using Itmas3.Application.Authorization.Policies.CreatePolicy;
using Itmas3.Application.Authorization.Policies.DeletePolicy;
using Itmas3.Application.Authorization.Policies.UpdatePolicy;
using Itmas3.Application.Authorization.Users.AssignPolicy;
using Itmas3.Application.Authorization.Users.ProvisionUser;
using Itmas3.Application.Authorization.Users.RevokePolicy;
using Itmas3.Application.Authorization.Users.SetRootStatus;
using Itmas3.Application.Authorization.Users.SetUserStatus;
using Itmas3.Application.Identity.GetCurrentUserProfile;
using Itmas3.Application.Inventory.AuthenticateDevice;
using Itmas3.Application.Inventory;
using Itmas3.Application.Inventory.EnrollDevice;
using Itmas3.Application.Inventory.ManageDevices;
using Itmas3.Application.Inventory.PurgeHistory;
using Itmas3.Application.Inventory.SubmitInventoryReport;
using Microsoft.Extensions.DependencyInjection;

namespace Itmas3.Application;

public static class DependencyInjection
{
    /// <summary>Registers application use cases. New use cases introduced by specs are registered here.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<GetCurrentUserProfileHandler>();

        // Authorization (SPEC-0001)
        services.AddSingleton<IPermissionDefinitionProvider, AuthorizationPermissionDefinitions>();
        services.AddScoped<IPermissionEvaluator, PolicyPermissionEvaluator>();
        services.AddScoped<AdministrationSafeguards>();
        services.AddScoped<ProvisionUserHandler>();
        services.AddScoped<SetUserStatusHandler>();
        services.AddScoped<AssignPolicyHandler>();
        services.AddScoped<RevokePolicyHandler>();
        services.AddScoped<SetRootStatusHandler>();
        services.AddScoped<CreatePolicyHandler>();
        services.AddScoped<UpdatePolicyHandler>();
        services.AddScoped<DeletePolicyHandler>();
        services.AddScoped<UpdatePermissionHandler>();
        services.AddScoped<SynchronizePermissionCatalogHandler>();

        // Inventory (SPEC-0002, SPEC-0003)
        services.AddSingleton<IPermissionDefinitionProvider, InventoryPermissionDefinitions>();
        services.AddScoped<DeviceAuthenticator>();
        services.AddScoped<EnrollmentCodeAuthenticator>();
        services.AddScoped<EnrollDeviceHandler>();
        services.AddScoped<SubmitInventoryReportHandler>();
        services.AddScoped<PurgeInventoryHistoryHandler>();
        services.AddScoped<RegisterDeviceHandler>();
        services.AddScoped<UpdateDeviceHandler>();
        services.AddScoped<RegenerateEnrollmentCodeHandler>();
        services.AddScoped<DeleteDeviceHandler>();

        return services;
    }
}
