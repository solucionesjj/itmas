using Itmas3.Application.Authorization;

namespace Itmas3.Application.Inventory;

/// <summary>Permissions of the device inventory (SPEC-0003 §7).</summary>
public static class InventoryPermissions
{
    public const string Category = "Inventario";

    public const string DevicesRead = "devices.read";
    public const string DevicesCreate = "devices.create";
    public const string DevicesUpdate = "devices.update";
    public const string DevicesDelete = "devices.delete";
    public const string DevicesReportsRead = "devices.reports.read";
}

internal sealed class InventoryPermissionDefinitions : IPermissionDefinitionProvider
{
    private const string Category = InventoryPermissions.Category;

    public IEnumerable<PermissionDeclaration> GetPermissions() =>
    [
        new(InventoryPermissions.DevicesRead, "Consultar equipos", "Ver el listado de equipos con su tipo, colaborador asociado, estado y cantidad de registros.", Category),
        new(InventoryPermissions.DevicesCreate, "Crear equipos", "Registrar equipos y obtener su código de enrolamiento.", Category),
        new(InventoryPermissions.DevicesUpdate, "Modificar equipos", "Modificar el nombre, el tipo y el correo del propietario, y regenerar el código de enrolamiento.", Category),
        new(InventoryPermissions.DevicesDelete, "Eliminar equipos", "Eliminar equipos: su agente deja de poder enviar inventario.", Category),
        new(InventoryPermissions.DevicesReportsRead, "Consultar inventario recolectado", "Ver el historial y los informes de los equipos (cuentas locales, IPs y ubicación).", Category),
    ];
}
