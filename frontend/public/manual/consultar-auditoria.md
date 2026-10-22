# Consultar la auditoría

La auditoría registra cada cambio de administración: quién lo hizo, cuándo, sobre qué y con qué resultado. No se
puede modificar ni borrar.

**Quién puede hacerlo:** `audit.read`.

## Pasos

1. Abre **Administración → Auditoría**.
2. Filtra por **Acción**, **Tipo de recurso** o **Periodo**.
3. Cada fila muestra la **Fecha**, el **Actor** (la persona, o el agente en el caso de los equipos), la **Acción**,
   el **Recurso** y el **Resultado**. Pulsa una fila para ver los valores anteriores y posteriores del cambio.

## Qué se registra

- Usuarios: alta automática, activación, desactivación, asignación y retiro de políticas, Root.
- Políticas y permisos: creación, cambios, activación, eliminación.
- Equipos: registro, cambios, regeneración del código, eliminación, enrolamiento del agente y enrolamientos rechazados.
- Auditoría SQL: cada vez que alguien marca o desmarca como válida una cuenta, un rol o un permiso de un motor SQL
  (tipos de recurso *Cuenta SQL* y *Rol o permiso SQL*), con la instancia, la cuenta y el elemento.
- Agenda de infraestructura: crear, modificar y eliminar actividades, programarlas, cancelarlas, registrar su ejecución,
  cambiar sus etiquetas, crear y modificar series y tipos de actividad, y el resultado de las publicaciones en Outlook
  (tipos de recurso *Actividad de la agenda*, *Serie de la agenda* y *Tipo de actividad*). Cada evento guarda los campos
  de la actividad antes y después del cambio, y nunca guarda secretos ni las respuestas de Outlook.
- También los intentos rechazados por una regla o una salvaguarda, con su motivo.

La auditoría nunca guarda códigos ni credenciales de los equipos.
