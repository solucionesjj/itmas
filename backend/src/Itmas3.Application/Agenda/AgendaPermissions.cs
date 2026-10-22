using Itmas3.Application.Authorization;

namespace Itmas3.Application.Agenda;

/// <summary>Permissions of the infrastructure agenda (SPEC-0006 §7). Every action also requires <see cref="Read"/>.</summary>
public static class AgendaPermissions
{
    public const string Category = "Agenda";

    public const string Read = "agenda.read";
    public const string Create = "agenda.create";
    public const string Update = "agenda.update";
    public const string Publish = "agenda.publish";
    public const string Complete = "agenda.complete";
    public const string TypesManage = "agenda.types.manage";
}

internal sealed class AgendaPermissionDefinitions : IPermissionDefinitionProvider
{
    private const string Category = AgendaPermissions.Category;

    public IEnumerable<PermissionDeclaration> GetPermissions() =>
    [
        new(AgendaPermissions.Read, "Consultar la agenda", "Ver las actividades de la agenda de infraestructura, su resumen, los tipos y las etiquetas, y exportar el calendario a un archivo .ics.", Category),
        new(AgendaPermissions.Create, "Crear actividades", "Crear actividades y series en la agenda; siempre nacen en Borrador.", Category),
        new(AgendaPermissions.Update, "Modificar actividades", "Modificar borradores y los campos comunes de una serie, editar las etiquetas y eliminar borradores. Modificar una actividad ya programada exige también «Programar y cancelar actividades».", Category),
        new(AgendaPermissions.Publish, "Programar y cancelar actividades", "Programar actividades, cancelarlas y, junto con «Modificar actividades», modificar las ya programadas: esto envía invitaciones, actualizaciones y cancelaciones al equipo por Outlook. También permite sincronizar de nuevo con Outlook.", Category),
        new(AgendaPermissions.Complete, "Registrar la ejecución", "Registrar el resultado, las notas y los enlaces de evidencia de las actividades ejecutadas.", Category),
        new(AgendaPermissions.TypesManage, "Gestionar tipos de actividad", "Crear, modificar y desactivar los tipos de actividad de la agenda.", Category),
    ];
}
