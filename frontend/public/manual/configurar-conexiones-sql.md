# Configurar las conexiones SQL del agente

El agente de inventario puede auditar, además, los accesos de motores SQL: una vez al día lee las cuentas, sus roles y
sus permisos y los envía a ITMas. Viene desactivado: solo audita los motores que configures en el propio equipo. Un
mismo agente puede auditar hasta 50 motores, en el propio equipo o en otros servidores.

Motores admitidos: Microsoft SQL Server 2019, 2022 y 2025; PostgreSQL 18; MySQL 9 LTS; MariaDB 12 LTS.

**Quién puede hacerlo:** el administrador del equipo donde está instalado el agente (no necesita permisos en ITMas).
Antes, el DBA debe crear en cada motor la cuenta de solo lectura del agente (ver
[Preparar un motor SQL para la auditoría](preparar-motor-sql)).

## Pasos

Ejecuta los comandos como administrador (PowerShell como administrador en Windows; `sudo` en Linux y macOS).

1. **Añadir una conexión:** `itmas-agent sql add`. El agente pide:
   - un **alias** para reconocerla (letras, números, `.`, `_` y `-`);
   - el **motor**, el ***host*** y el **puerto** (o, en SQL Server, el nombre de la instancia);
   - en PostgreSQL, la **base de datos inicial**;
   - el **usuario** y la **contraseña** de la cuenta del agente. La contraseña no se ve al escribirla;
   - el **modo de TLS** (ver más abajo).

   Antes de guardar, el agente prueba la conexión, el certificado y la lectura del catálogo. Si algo falla, explica la
   causa y no guarda nada.
2. **Ver las conexiones:** `itmas-agent sql list` muestra el alias, el motor, el destino, el modo de TLS y el resultado
   de la última recolección. Nunca muestra contraseñas.
3. **Probar una conexión:** `itmas-agent sql test <alias>`. Además avisa si la cuenta del agente tiene más privilegios
   de los necesarios.
4. **Cambiar una conexión** (por ejemplo, para rotar la contraseña): `itmas-agent sql update <alias>`. Se vuelve a
   probar antes de guardar.
5. **Quitar una conexión:** `itmas-agent sql remove <alias>`. Borra la conexión y su contraseña del equipo.

Para automatizar la configuración sin preguntas, la contraseña se puede leer de un archivo o de una variable de
entorno, nunca de un argumento del comando. Consulta `itmas-agent sql add --help`.

## Modos de TLS

La conexión con el motor va **siempre cifrada**, también cuando el motor está en el mismo equipo. Un motor que no
acepte TLS no se puede auditar hasta que el DBA lo active.

| Modo | Qué hace | Cuándo usarlo |
|---|---|---|
| `verify` (por defecto) | Valida el certificado del motor con los certificados de confianza del sistema y el nombre del *host*. | Siempre que el motor tenga un certificado emitido por una autoridad de confianza. |
| `pinned` | Acepta solo el certificado que indiques (archivo o huella SHA-256). | Motores con un certificado autofirmado que el DBA te entrega. |
| `unverified` | Cifra, pero **no valida** el certificado: acepta cualquiera. | Solo si no hay alternativa. |

Con `unverified`, alguien en la red podría hacerse pasar por el motor: enviar datos falsos o capturar la credencial del
agente. Por eso el agente explica el riesgo, propone `pinned` y pide escribir `ACEPTO EL RIESGO`; nunca lo activa por su
cuenta, ni siquiera si `verify` falla. ITMas muestra esas instancias con el aviso **Certificado sin verificar**.

## Resultado

En la siguiente recolección diaria (junto con el inventario) el motor aparece en **Auditoría SQL → Instancias** (ver
[Consultar los accesos de los motores SQL](consultar-accesos-sql)).

## Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| "No se pudo resolver el nombre" o "No se pudo conectar" | Revisa el *host*, el puerto y que el firewall permita la conexión desde este equipo. |
| "El motor no acepta conexiones cifradas" | El DBA debe activar TLS en el motor. |
| "El certificado del servidor no es válido" | Pide al DBA un certificado de confianza, o usa `pinned` con el certificado que te entregue. |
| "Usuario o contraseña incorrectos" o "Sin permiso para leer el catálogo" | Revisa la cuenta del agente con el DBA (ver [Preparar un motor SQL para la auditoría](preparar-motor-sql)). |
| `sql test` avisa de privilegios de más | La cuenta del agente puede hacer más de lo necesario. Pide al DBA que la deje con los privilegios mínimos. |
