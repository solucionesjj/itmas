# Validar accesos SQL

Validar es confirmar que un acceso es correcto: que la cuenta debe existir con esos atributos, que debe ser miembro de
ese rol o que debe tener ese permiso. Cada elemento se valida por separado, con un solo clic en su casilla **Válido**.

**Quién puede hacerlo:** usuarios con los permisos `sql-audit.read` y `sql-audit.accesses.validate` (*Validar accesos
SQL*). Sin el segundo, las casillas se ven desactivadas.

## Qué se valida

| Elemento | Qué confirmas |
|---|---|
| **La cuenta** | Que debe existir y con sus atributos actuales: habilitada o no, tipo, política de contraseñas, *login* asociado… |
| **Cada pertenencia a un rol** | Que la cuenta debe ser miembro de ese rol. |
| **Cada permiso** | Que la cuenta debe tener ese permiso sobre ese objeto, con ese estado. |

Una cuenta está **completamente validada** cuando la cuenta y todos sus roles y permisos actuales son válidos. Las
pantallas muestran por cuenta el número de elementos pendientes.

## Pasos

1. Abre **Auditoría SQL → Instancias**, entra en una instancia y elige la pestaña **Cuentas** (por defecto muestra las
   cuentas con algo pendiente) o **Permisos**.
2. Revisa el elemento. Para ver los roles y permisos de una cuenta, pulsa su nombre.
3. Marca su casilla **Válido**. Se guarda al momento, sin confirmaciones, y el contador de pendientes se actualiza.
4. Para deshacerlo, vuelve a pulsar la casilla: el elemento vuelve a **Pendiente de validar**.

Empieza por lo crítico: en la pestaña **Permisos**, filtra por riesgo *Crítico* y validación *Pendiente*.

## Estados

| Estado | Significado |
|---|---|
| **Pendiente de validar** | Es nuevo o se desmarcó. |
| **Válido** | Alguien lo marcó y no ha cambiado desde entonces. Se indica quién y cuándo. |
| **Cambió desde la validación** | Solo en cuentas: estaba validada, pero cambió alguno de sus atributos o se volvió a crear con el mismo nombre. Revisa el registro de cambios y vuelve a validarla. |
| **Eliminado del motor** | Ya no figura en la última recolección. No se puede marcar. |

Un permiso o una pertenencia no "cambian": si cambia su estado (por ejemplo, de *Concedido* a *Concedido con opción de
conceder*), aparece como un permiso nuevo, pendiente, y el anterior pasa a eliminado.

## Resultado

Cada marca y cada desmarca quedan en la auditoría de ITMas con el usuario y la fecha, y en el historial de
validaciones de la cuenta.

## Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| "Esta cuenta cambió desde que cargaste la página. Revisa los cambios y vuelve a validarla." | Llegó una recolección nueva mientras revisabas. La fila se actualiza: revisa sus atributos y vuelve a marcarla. |
| "No se pudo guardar la validación. Inténtalo de nuevo." | La casilla vuelve a su estado anterior. Comprueba la conexión y vuelve a pulsarla. |
| La casilla está desactivada | Te falta el permiso `sql-audit.accesses.validate`, o el elemento fue eliminado del motor (pasa el puntero sobre la casilla para ver el motivo). |
