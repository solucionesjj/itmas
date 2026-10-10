# Registrar un equipo y obtener su código

Antes de instalar el agente de inventario en un equipo, el equipo se registra en ITMas. Al registrarlo, ITMas genera
el **código del equipo**, que el agente usa una sola vez para enrolarse.

**Quién puede hacerlo:** `devices.create`.

## Pasos

1. Abre **Inventario → Equipos** y pulsa **Nuevo equipo**.
2. Escribe el **Nombre del equipo** (único).
3. Elige el **Tipo de equipo**: **Estación de trabajo** o **Servidor**.
4. Si quieres, escribe el **Correo del propietario**. Si coincide con un usuario de ITMas, se mostrará su nombre.
5. Pulsa **Crear**.
6. Aparece el **Código del equipo** (`XXXXX-XXXXX-XXXXX-XXXXX`). Pulsa **Copiar código** y después **Listo**.
7. Entrega el código de forma segura a quien instalará el agente (ver [Instalar el agente y enrolar el equipo](instalar-agente)).

## Resultado

El equipo aparece en el listado como **Pendiente** hasta que su agente se enrola; entonces pasa a **Activo**.

## Importante

- El código **solo se muestra una vez** y sirve **una sola vez**. ITMas no puede volver a mostrarlo.
- No lo envíes por correo, chat público ni tickets. Entrégalo solo a quien instalará el agente.
- Si se pierde o pudo filtrarse antes de usarse, genera uno nuevo (ver [Editar un equipo, regenerar su código o eliminarlo](gestionar-equipo)).

## Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| "Ya existe un equipo con este nombre." | Usa otro nombre (no distingue mayúsculas). |
| Cerré el diálogo sin copiar el código | Regenera el código del equipo. |
