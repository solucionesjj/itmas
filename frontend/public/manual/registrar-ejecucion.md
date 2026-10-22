# Registrar la ejecución de una actividad

Cuando una actividad se hace, se registra su ejecución: el resultado, notas y enlaces a la evidencia. Es lo que sirve
después para demostrar, por ejemplo en una auditoría, que el parche, la prueba o el cambio de claves se realizó.

**Quién puede hacerlo:** `agenda.complete` (*Registrar la ejecución*).

## Pasos

1. Abre el detalle de una actividad **Programada** y pulsa **Registrar ejecución** (o elige esa opción en el menú de su
   fila en **Agenda → Actividades**). Solo está disponible desde la hora de inicio programada.
2. Elige el **Resultado**: **Realizada con éxito**, **Realizada parcialmente**, **Fallida** o **No realizada**.
3. Si lo sabes, indica la **Fecha y hora reales de la ejecución** (en la zona horaria de la organización). Si la dejas
   vacía, se usa el momento del registro.
4. Escribe las **Notas** si hace falta (hasta 4000 caracteres).
5. Añade los **Enlaces de evidencia** con **Añadir enlace**: una **Etiqueta** (hasta 100 caracteres) y la **URL (http o
   https)** del informe, ticket o documento. Puedes añadir hasta 10.
6. Pulsa **Registrar**.

## Reglas de los enlaces

- Son solo enlaces: ITMas no guarda archivos.
- La dirección debe empezar por `http://` o `https://` y no puede llevar usuario ni contraseña.
- No incluyas contraseñas, claves ni hallazgos de seguridad, ni en las notas ni en las etiquetas de los enlaces: el
  registro lo ve todo el equipo. Registra *cuándo* y *quién*, no el valor.
- No uses enlaces que dan acceso por sí mismos, como los de "cualquier persona con el enlace" o los que llevan una clave
  de acceso en la dirección: enlaza el documento en su ubicación normal, que pide iniciar sesión. Los enlaces quedan
  también en la auditoría.

## Después de registrarla

La actividad pasa a **Completada** y su detalle muestra la tarjeta **Ejecución** con el resultado, la fecha real, quién la
registró y cuándo, las notas y los enlaces. Los enlaces se abren en otra pestaña.

Después solo se pueden cambiar las notas y los enlaces: abre el detalle y pulsa **Editar notas y evidencia**. El resultado
y la fecha real ya no se pueden cambiar.

## Resultado

La actividad queda cerrada, con su evidencia, y cuenta como **Completada** en el resumen del año (ver [Consultar la agenda
del año](consultar-agenda)).

## Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| El botón **Registrar ejecución** está desactivado, o aparece "No se puede registrar la ejecución antes del inicio programado." | La actividad todavía no empezó: solo se puede registrar desde su inicio programado. Espera a que empiece o reprograma la actividad (ver [Planificar actividades y series](planificar-actividad)). |
| No veo **Registrar ejecución** | La actividad debe estar **Programada** (los borradores se programan primero, ver [Programar y cancelar actividades](programar-y-cancelar)) y necesitas el permiso `agenda.complete`. |
| "Debe ser una URL http o https sin usuario ni contraseña." | Corrige el enlace: usa una dirección completa que empiece por `http://` o `https://` y sin credenciales incrustadas. |
| Quiero cambiar el resultado de una actividad ya completada | No se puede: solo se editan las notas y los enlaces. |
| Una actividad quedó **Vencida** | Terminó sin cerrarse. Si se ejecutó, registra su ejecución; si no se hará, cancélala (si estaba programada) o elimínala (si era un borrador). |
