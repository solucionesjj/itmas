# Consultar los accesos de los motores SQL

La sección **Auditoría SQL** muestra, para cada motor de base de datos que audita un agente, sus cuentas, los roles de
los que son miembros y sus permisos, con una etiqueta de nivel de riesgo. Los datos llegan una vez al día desde el
agente de cada equipo; ITMas no crea, modifica ni revoca accesos en los motores.

**Quién puede hacerlo:** usuarios con el permiso `sql-audit.read` (*Consultar auditoría SQL*).

## Instancias

En el menú, abre **Auditoría SQL → Instancias**. Cada fila es un motor auditado (una *instancia*) e indica:

- el equipo cuyo agente lo audita, el alias de la conexión, el motor y su versión, y el destino (*host* y puerto);
- la fecha de la última recolección y el número de cuentas;
- los **elementos pendientes** de validar y, de ellos, los **pendientes críticos**.

Puedes buscar por alias, *host*, instancia o equipo, filtrar por motor y ordenar por cualquiera de esos datos.

Algunas instancias llevan un aviso:

| Aviso | Significado |
|---|---|
| **Certificado sin verificar** | El agente se conecta cifrando el canal, pero sin validar el certificado del motor (lo eligió expresamente el administrador del equipo). Conviene pasar esa conexión a un certificado verificado o fijado. |
| **Datos no disponibles** | En la última recolección no se pudo leer la instancia o alguna de sus bases de datos. Sus datos anteriores se conservan, marcados como no actualizados. |
| **Equipo eliminado** | El equipo se retiró del inventario: su agente ya no envía datos, pero lo recolectado se puede seguir consultando. |

## Cuentas y permisos de una instancia

Pulsa el alias de una instancia. Arriba aparecen sus datos, sus bases de datos y los avisos. Debajo hay dos pestañas:

- **Cuentas:** una fila por cuenta, con su nivel ("Motor" o el nombre de la base de datos), nombre, tipo, si está
  habilitada, su riesgo, un resumen de sus roles, los elementos pendientes y la casilla **Válido**. Por defecto se
  muestran solo las cuentas con algo pendiente; cambia el filtro de validación para ver todas, las completamente
  validadas, las que cambiaron desde su validación o las eliminadas del motor. También puedes filtrar por nivel, base
  de datos, tipo y riesgo, y buscar por nombre.
- **Permisos:** todos los permisos de la instancia en una sola tabla, para revisar, por ejemplo, los **críticos
  pendientes** de todas las cuentas a la vez. Se filtra por riesgo, validación, base de datos, cuenta y tipo de objeto.

## Detalle de una cuenta

Pulsa el nombre de una cuenta para ver:

- sus atributos (tipo, habilitada o bloqueada, base de datos y esquema por defecto, *login* asociado o si es
  **huérfana**, política y caducidad de la contraseña, fechas);
- su riesgo propio y el **heredado** de los roles de los que es miembro;
- la tabla de **roles** y la de **permisos**, con el objeto asegurable como ruta `base.esquema.objeto.columna`, el
  permiso, su estado (*Concedido*, *Concedido con opción de conceder* o *Denegado*), quién lo concedió, el riesgo y la
  fecha en que se detectó;
- el **registro de cambios** (cuentas, roles y permisos añadidos, eliminados o modificados entre dos recolecciones) y
  el **historial de validaciones** (quién marcó o desmarcó cada elemento y cuándo).

## Niveles de riesgo

Cada etiqueta lleva siempre un ícono y un texto; el significado no depende del color.

| Nivel | Ícono | Qué incluye (ejemplos) |
|---|---|---|
| **Crítico** | error | Cuentas `sa` y `root`, superusuario, roles `sysadmin` o `db_owner`, `CONTROL SERVER`, `ALL PRIVILEGES` globales, propietario de una base de datos. Privilegios amplios. |
| **Alto** | advertencia | `INSERT`, `UPDATE`, `DELETE`, `EXECUTE`, `ALTER`, `DROP`, `db_datawriter`, permisos con opción de concederlos a otros y **cualquier permiso que no esté en el catálogo**, para que nada quede sin revisar. |
| **General** | información | `SELECT`, `CONNECT`, `USAGE`, `db_datareader`. |
| **Denegado** | bloqueo | Un `DENY`: no concede acceso, por eso no lleva nivel de riesgo. |

El riesgo de una cuenta es el máximo de sus elementos. Si es miembro de un rol con riesgo mayor (directamente o a través
de otros roles), se muestra ese riesgo indicado como **heredado**. Una cuenta deshabilitada conserva su riesgo y se
muestra como deshabilitada.

## Resultado

Tienes a la vista qué cuentas existen en cada motor, qué pueden hacer, qué cambió desde la última revisión y qué falta
por validar (ver [Validar accesos SQL](validar-accesos-sql)).

## Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| "Todavía no hay motores SQL auditados." | Ningún agente envía datos todavía. El administrador del equipo debe configurar las conexiones (ver [Configurar las conexiones SQL del agente](configurar-conexiones-sql)). |
| "No se pudo leer en la última recolección: …" | El motivo indica qué falló (sin conexión, certificado no válido, sin permiso, base de datos fuera de línea, tiempo agotado). Los datos mostrados son los de la recolección anterior. Revisa la conexión con `itmas-agent sql test <alias>` en el equipo. |
| Una instancia aparece dos veces | Dos agentes la auditan, o el mismo agente con dos destinos distintos (por ejemplo, nombre e IP). Deja una sola conexión. |
| No veo la sección Auditoría SQL | Necesitas el permiso `sql-audit.read`. Ver [Qué puedo hacer: permisos y cómo solicitarlos](permisos-y-acceso). |
