---
id: ADR-0022
title: Copias en la cuenta del servidor, sin inmutabilidad (enmienda de §8.7(e), MAJOR 5.0.0)
status: Accepted
date: 2026-11-05
deciders: [architecture, security]
related: [SPEC-0009, ADR-0019, ADR-0018]
---

# ADR-0022 — Copias en la cuenta del servidor, sin inmutabilidad

> **Propuesto el 2026-11-05** a petición del solicitante, tras la primera copia real en producción (SPEC-0009 §18 y §19.10). Ese mismo día:
> - la primera revisión independiente de `architecture` y `security` dio «aceptable con cambios» en las dos;
> - el solicitante rechazó la copia fuera de línea (P-28: «por ahora no vamos a hacer copias fuera de línea, aceptar el riesgo por ahora»), y una segunda revisión de `security` revisó el ajuste;
> - las comprobaciones de AWS mostraron que el rol de la instancia **lo comparten otras instancias** y alcanza todo el bucket y un segundo bucket ajeno a ITMas. El solicitante decidió **aceptarlo como riesgo** (R-10), y una tercera revisión de `architecture` y `security` revisó el ajuste.
>
> Todos los hallazgos están incorporados (§«Revisiones»). **Aceptado por el solicitante el 2026-11-05**, con las tres compuertas de §«Compuertas de aceptación» cumplidas.
> La constitución pasa a la **5.0.0** en el mismo cambio, y SPEC-0009 incorpora lo de §«Cambios al aceptar». La persona que acepta es también quien administra
> el servidor y la infraestructura: no hay separación de funciones (SPEC-0009, R-2).

## Contexto

La constitución §8.7(e), desde la 3.0.0 (ADR-0019), dice:

> Las credenciales de despliegue, de registro y de copias DEBEN ser de mínimo alcance (solo lectura o solo añadir) y NO DEBEN estar en el repositorio ni en
> la línea de comandos (§4). Ninguna credencial de CI PUEDE dar acceso de ejecución al servidor sin un ADR propio con revisión de `security`.

Además, §8.7(c) prohíbe que los componentes del host relajen §3.8, que pide «mínimo privilegio en todo» con una única excepción: el agente de dispositivo.

SPEC-0009 lo concreta así:

- las copias van a un bucket de S3 de **otra cuenta** de AWS (RF-2, Q-1);
- el servidor solo puede **añadir**: no puede leer, listar ni borrar (RN-4, P-1, P-25);
- el destino tiene **versionado y *Object Lock*** y **vence a los 30 días** (RF-3, Q-3, §10.6).

Así, un servidor comprometido no puede destruir su propia recuperación.

**Lo que hay en producción**, comprobado el 2026-11-05 y confirmado por el solicitante:

- El bucket es de uso general y está en la **misma cuenta** que el servidor.
- **No tiene versionado** (`VersionId: null`) y, por tanto, tampoco *Object Lock*.
- **No tiene regla de vencimiento.**
- El rol de la instancia lleva la política de SSM y **lo comparten otras instancias** de la cuenta. Con él se puede:
  - leer, escribir, listar y borrar en **todo el bucket**, no solo en el prefijo de las copias;
  - acceder por completo a **un segundo bucket** ajeno a ITMas.
- El 2026-11-05 la persona administradora restringió su política para que **no pueda cambiar la configuración del bucket ni borrarlo** (simulación con `implicitDeny`, SPEC-0009 §18).
- El servidor se administra por **SSH y por SSM Session Manager**.

El solicitante decidió no usar versiones («solo almacenamiento») y aceptar como riesgo la política y el rol compartido.

**Lo que sí se cumple y no cambia:**

- §8.7(f): cada copia se cifra con `age` antes de salir del servidor, y la clave privada no reside en él.
- La restauración se probó con la primera copia real (SPEC-0009 CA-4, 2026-11-05).
- La credencial es la de un rol de instancia: no hay claves de AWS guardadas.

### Qué expone

Las credenciales del rol las obtiene quien consiga `root` en **el servidor de ITMas o en cualquier otra instancia que comparta el rol**: el IMDS las entrega desde el host, y el *hop limit* 1 solo protege frente a los contenedores. Las vías, en cualquiera de esas instancias, son:

- una clave SSH robada;
- una ejecución remota que escale al host;
- una identidad de IAM con `ssm:StartSession` o `ssm:SendCommand` sobre la instancia, o con EC2 Instance Connect o la consola serie.

Con esas credenciales se puede:

1. **Destruir las copias.**
   - Borrar las del bucket, o sobrescribirlas: los nombres son predecibles (`itmas3-<UTC>.dump.age`) y, sin versionado, un `PutObject` sobre una clave existente la reemplaza.
   - Desde el servidor de ITMas, borrar también las locales de `/var/backups/itmas3`, de 7 días.
   - Es el escenario de extorsión que RN-4 quería impedir: se pierden a la vez el servicio y su recuperación.
2. **Manipularlas sin que se note.**
   - La clave pública de `age` está en `host.conf`, y `age` no autentica al remitente.
   - Un atacante puede sustituir cualquier copia por un volcado válido y alterado, que la restauración aceptaría como bueno (se restaura como superusuario).
3. **Usar las credenciales fuera de la instancia** hasta que caduquen.
4. **Llegar a datos de otros sistemas:** el resto del bucket, que es de uso general y puede guardar datos sin cifrar, y el segundo bucket.

La configuración del bucket ya no se puede cambiar con el rol (decisión 2).

Además:

- **Quien administre la cuenta** del servidor administra también sus copias: se pierde la separación de cuentas.
- **CloudTrail** registra por defecto `StartSession` y `SendCommand` (historial de 90 días), pero no `PutObject` ni `DeleteObject`: son *data events*, con coste aparte.
- **Leer y listar las copias expone poco:** están cifradas y solo se ven nombres, fechas y tamaños.

## Decisión

1. **Destino.** Las copias van a un bucket de S3 de la **misma cuenta** que el servidor, bajo un prefijo propio, **sin versionado ni *Object Lock***.
   - El bucket las conserva **sin vencimiento** (P-30 rechazada).
   - Se mantienen el cifrado con `age` antes de salir, la clave privada fuera del servidor (§8.7 f), el **bloqueo de acceso público** del bucket y un rol de instancia sin claves guardadas.
   - El servidor sigue borrando solo sus copias locales de más de 7 días (RF-3).
   - `backup-db.sh` no cambia.
2. **Credencial.** El rol de la instancia, con la política de SSM y compartido con otras instancias, puede subir, listar, leer y borrar objetos **en todo el bucket**, y tiene acceso completo a un segundo bucket.
   - **NO DEBE poder cambiar la configuración del bucket de las copias.** Entre otras acciones: `PutBucketPolicy`, `DeleteBucketPolicy`, `PutBucketAcl`, `PutBucketOwnershipControls`, `PutBucketPublicAccessBlock`, `PutLifecycleConfiguration`, `PutBucketVersioning`, `PutBucketNotification`, `PutReplicationConfiguration` y `DeleteBucket`. Tampoco `PutAccountPublicAccessBlock`.
     Así se impide que haga público el bucket, lo vacíe con una regla de ciclo de vida o lo borre entero.
     **Cumplido el 2026-11-05:** todas salen `implicitDeny` en `aws iam simulate-principal-policy`, y el bucket no tiene política propia que pueda conceder más.
   - **Riesgo aceptado (R-10), decidido el 2026-11-05:** el rol no se limita al prefijo de las copias ni a ITMas.
     - Un compromiso de ITMas alcanza datos de otros sistemas.
     - Un compromiso de cualquier otra instancia que use el rol alcanza las copias de ITMas.
     - El solicitante rechazó la alternativa F, un rol propio para la instancia de ITMas.
3. **Sin control compensatorio por ahora.** El solicitante rechazó P-28 el 2026-11-05: era una copia mensual fuera de línea, de 35 días como máximo, separada de la clave y con su SHA-256.
   - **Qué se acepta (R-8):**
     - Si se compromete el servidor de ITMas, cualquier instancia que comparta el rol, el acceso de SSM o la cuenta, se pueden perder o alterar **todas** las copias: las del bucket y las locales. No queda ninguna fuera de su alcance.
     - En ese escenario no hay recuperación: se pierden todos los datos desde el inicio. El RPO de 24 horas solo vale para fallos que no sean un ataque: un disco, un borrado accidental de la base, una migración rota.
     - Sin P-29 (rechazada) ni los *data events* de CloudTrail, un borrado o una alteración de las copias puede pasar inadvertido hasta que haga falta restaurar.
   - **Revisión:** la aceptación de R-8 y R-10 es temporal y se revisa como muy tarde el **2027-05-05**, seis meses después. El solicitante confirmó esa fecha para R-8 y para R-10 el 2026-11-05.
     - Se revisa antes si cambia lo que enumera §«Seguimiento».
     - Si la fecha vence sin revisión, la aceptación caduca. Renovarla exige una nueva aceptación expresa, con su fecha.
     - La fecha consta en los riesgos residuales de SECURITY.md (`security` la mantiene), y la prueba trimestral de restauración comprueba que no ha vencido.
   - **Restauración:** no hay ancla de integridad, así que quien restaure trata cada copia del bucket como no confiable. Si sospecha que el servidor de ITMas, o una instancia que comparta el rol, estuvo comprometido, no da la copia por buena solo porque `restore-db.sh` termine en 0.
4. **Controles recomendados (P-29): rechazados por el solicitante el 2026-11-05.** No se aplican; quedan como opción para la revisión de R-8. Eran:
   - una denegación explícita de `s3:DeleteObject` para el rol en la política del bucket (una `Deny` prevalece sobre la política de identidad), que frena `aws s3 rm` y el borrado masivo, aunque no la sobrescritura;
   - una notificación de S3 `s3:ObjectRemoved:*` a un tema de SNS con suscripción de correo a la lista de avisos, para detectar un borrado el mismo día.
5. **Retención (P-30): rechazada por el solicitante el 2026-11-05** («sin vencimiento»). Era una regla de ciclo de vida que venciera las copias del prefijo a los 365 días.
   - Los volcados conservan sin límite los datos de personas y equipos ya eliminados. Esa retención indefinida se acepta en R-8 y se revisa con él (2027-05-05).
   - Las copias del bucket solo las borra una persona, a mano. La retención indefinida no aporta seguridad, porque el rol también puede borrarlas.
6. **SSM como segundo acceso de administración (R-9).** Session Manager se enumera en SPEC-0009 §10.6 como recurso del que dependen los controles (§8.7, primer párrafo).
   - No abre puertos de entrada.
   - Toda identidad con `ssm:StartSession` o `ssm:SendCommand` sobre la instancia, con permiso de crear o cambiar asociaciones de SSM State Manager, o con EC2 Instance Connect o la consola serie, equivale a `root` en el servidor.
   - **Observado el 2026-11-05:**
     - CloudTrail registra `StartSession` y `SendCommand`.
     - Abren sesiones de SSM en la cuenta una persona distinta del solicitante y una identidad de automatización (Terraform).
     - SSM State Manager ejecuta comandos en instancias de la cuenta.
     - La consola serie está activada a nivel de cuenta.
   - **La consola serie se mantiene activada y se acepta en R-9** (decisión del solicitante, 2026-11-05). Se revisa con R-8 y R-10 (2027-05-05).
   - **R-9 se acepta con lo que muestra CloudTrail** (decisión del solicitante, 2026-11-05). La lista completa de identidades con esos permisos queda para la revisión del 2027-05-05.
7. **Enmienda de §8.7(e)** (texto en §«Texto de la constitución»).
   - Hoy las credenciales de copias «DEBEN ser de mínimo alcance (solo lectura o solo añadir)». Pasan a «DEBERÍAN ser de mínimo alcance, limitadas a su destino, exclusivas del servidor y de solo añadir».
   - Una credencial que no cumpla alguna de esas condiciones se admite como **única excepción a §3.8 y a §8.7(c)** para las credenciales de copias, si el ADR o la spec declara:
     - su alcance real y los sistemas con los que se comparte;
     - quién puede borrar o sobrescribir las copias;
     - el control compensatorio, o la pérdida que se acepta sin él;
     - la aceptación expresa y fechada, con caducidad.
   - En ningún caso puede cambiar la configuración del destino de las copias.

## Alternativas consideradas

| Alternativa | A favor | En contra |
|---|---|---|
| **A. Lo que pide SPEC-0009:** bucket en otra cuenta con *Object Lock*, versionado, vencimiento a 30 días y rol de solo añadir | Un servidor comprometido no destruye ni altera su recuperación (RN-4). Plantillas y verificación ya en `deploy/aws/` | Exige otra cuenta y un bucket nuevo, porque *Object Lock* solo se activa al crearlo. El solicitante no quiere versiones |
| **B. Este bucket con un rol de solo añadir:** una `Deny` en la política del bucket para `GetObject`, `ListBucket` y `DeleteObject` del rol, sin tocar la política de SSM, y escrituras condicionales (`If-None-Match`, condición `s3:if-none-match`) contra la sobrescritura | Bajo coste, sin versiones y en la misma cuenta: encaja con «solo almacenamiento». Una `Deny` prevalece sobre la política de identidad | No está comprobado que `rclone` envíe `If-None-Match`; sin eso, la sobrescritura sigue abierta. Quien administra la cuenta puede quitar la `Deny`. Con el rol compartido, la `Deny` afectaría a las otras instancias. Se propuso en parte como P-29, que el solicitante rechazó el 2026-11-05 |
| **C. Este bucket con versionado y una `Deny` de `s3:DeleteObjectVersion` para el rol** | Una sobrescritura o un borrado dejan la versión anterior | El solicitante no quiere manejar versiones. Sin *Object Lock*, quien administre la cuenta puede borrarlas |
| **D. Este bucket tal como está, con copia mensual fuera de línea (P-28)** | Sin cambios en AWS. La copia fuera de línea, con su suma, limita la pérdida a unos 35 días y da un ancla de integridad | Una tarea manual mensual. **Rechazada por el solicitante el 2026-11-05** |
| **E (elegida: destino y compensación). Este bucket tal como está, sin control compensatorio, con aceptación temporal del riesgo** | Sin cambios en AWS ni trabajo manual | Un compromiso del servidor, de una instancia que comparta el rol (R-10) o de la cuenta destruye o altera todas las copias, y en ese escenario no hay recuperación. Exige enmendar la constitución más que D |
| **F. Rol propio para la instancia de ITMas:** solo `AmazonSSMManagedInstanceCore` y subir, leer y borrar en el prefijo de las copias, sin configuración del bucket ni el segundo bucket | Corta R-10 en los dos sentidos: ni ITMas alcanza otros datos ni otras instancias alcanzan sus copias. No toca a las demás instancias | Hay que crear un rol y un perfil de instancia y cambiarlo en la instancia. **Rechazada por el solicitante el 2026-11-05** («aceptar como riesgo») |
| **G (elegida: credencial). El rol compartido tal como está, sin permisos de configuración del bucket** | Sin cambios para las otras instancias | R-10 en los dos sentidos. Exige relajar también el mínimo alcance (§3.8, §8.7 c y e) |

## Consecuencias

- **Positivas:**
  - La spec y la constitución describen lo que existe.
  - SPEC-0009 puede cerrarse con CA-1 y CA-2 reescritos.
  - No hay que crear otra cuenta ni otro rol.
- **Negativas / riesgos:**
  - **R-8:** pérdida o alteración de todas las copias si se compromete el servidor de ITMas, una instancia que comparta el rol, el acceso de SSM o la cuenta. Sin control compensatorio, ese escenario no tiene recuperación.
  - **R-9:** SSM, State Manager y la consola serie de EC2 como accesos de `root` fuera de `fail2ban` y de `ufw`.
  - **R-10:** rol compartido con otras instancias, con alcance sobre todo el bucket y sobre un segundo bucket. Amplía R-8, porque las copias quedan al alcance de todas esas instancias, y expone datos de otros sistemas si se compromete ITMas.
  - Retención indefinida de datos personales (P-30 rechazada).
- **Seguimiento:**
  - Revisar R-8, R-9 y R-10 como muy tarde en la fecha de la decisión 3, o antes si cambia alguna de estas cosas:
    - el número de personas administradoras;
    - quién tiene acceso por SSM;
    - las instancias que comparten el rol;
    - el valor de los datos.
  - En esa revisión, retomar P-28, la alternativa B y la alternativa F, las opciones de menor coste que reducen estos riesgos:
    - P-28 deja una copia fuera del alcance del rol;
    - B le quita leer, listar y borrar;
    - F corta R-10.
  - Comprobar si `rclone` admite `If-None-Match`; si lo admite, la alternativa B pasa a ser viable.
  - Valorar GuardDuty (uso de credenciales de instancia fuera de AWS) o una condición `aws:ec2InstanceSourceVPC` en la política del rol.
  - Valorar los *data events* de CloudTrail del prefijo de las copias.
  - Obtener la lista completa de identidades con acceso de `root` por AWS (decisión 6) con `aws iam get-account-authorization-details`.
  - Valorar desactivar la consola serie de EC2 (`aws ec2 disable-serial-console-access`), que afecta a todas las instancias de la cuenta.
  - Valorar el bloqueo de acceso público a nivel de cuenta, que el solicitante decidió no activar el 2026-11-05; antes, comprobar que ningún bucket de la cuenta debe ser público.

## Compuertas de aceptación

El solicitante acepta este ADR solo cuando se cumplan las tres compuertas.

1. **Aceptación de los riesgos:**
   - El solicitante acepta expresamente R-8 y R-10, sin control compensatorio y con la fecha de revisión de la decisión 3 (2027-05-05).
     La fecha está confirmada para los dos desde el 2026-11-05.
   - Como R-10 expone datos de otros sistemas, consta además que **lo acepta, o lo conoce, quien responde de esos datos** (se anota el rol, no el nombre).
     **Cumplido el 2026-11-05:** el solicitante, que es también quien administra la infraestructura y responde de los datos de esos otros sistemas, acepta R-10 por ellos. No hay separación de funciones (R-2).
   - P-29, P-30 y la alternativa F se rechazaron el 2026-11-05.
2. **Comprobaciones anotadas en SPEC-0009 §18, sin identificadores:**
   - el bloqueo de acceso público del bucket: **hecho el 2026-11-05**. La cuenta **no tiene bloqueo** (`NoSuchPublicAccessBlockConfiguration`), y el solicitante decidió no activarlo (2026-11-05). La protección del bucket de las copias depende de su propio bloqueo, que el rol no puede cambiar;
   - si el bucket tiene política propia: **no la tiene** (`NoSuchBucketPolicy`, 2026-11-05), así que la simulación sin `--resource-policy` es completa;
   - que el rol no puede cambiar la configuración del bucket (decisión 2):
     - hecho el 2026-11-05 para `PutBucketPolicy`, `PutLifecycleConfiguration`, `PutBucketVersioning`, `PutBucketNotification`, `PutBucketPublicAccessBlock` y `DeleteBucket`;
     - y para `DeleteBucketPolicy`, `PutBucketAcl`, `PutBucketOwnershipControls`, `PutReplicationConfiguration` y `PutAccountPublicAccessBlock`, también con `implicitDeny` (**hecho el 2026-11-05**);
   - su alcance real en S3: **anotado el 2026-11-05** (todo el bucket y un segundo bucket; denegado en un bucket cualquiera; R-10);
   - quién tiene los permisos de la decisión 6: CloudTrail muestra a una persona distinta del solicitante, una identidad de automatización y State Manager. El solicitante acepta R-9 con eso (2026-11-05); la lista completa queda para la revisión;
   - la decisión sobre la consola serie: **se acepta en R-9** (2026-11-05);
   - que CloudTrail registra `StartSession` y `SendCommand`: **hecho el 2026-11-05**.
3. **La aceptación va en un único cambio** con todo lo de §«Cambios al aceptar». La constitución, el estado de `specs/README.md` y la documentación operativa cambian a la vez, como en ADR-0019 y ADR-0021 (constitución §10.1).
   - Hasta la aceptación, DEVELOPMENT.md §12.9 y SECURITY.md llevaron solo una nota de la desviación pendiente y la advertencia de la decisión 3 sobre restaurar.
   - **Cumplido el 2026-11-05:** la aceptación va en el mismo cambio que la constitución 5.0.0 y la documentación.

## Cambios al aceptar

- **Constitución:** §8.7(e) con el texto de abajo, la cabecera en 5.0.0 y la fila del historial.
- **SPEC-0009:**
  - **RF-2:** misma cuenta y credencial de la decisión 2, en lugar de «otra cuenta» y «solo añadir».
  - **RF-3:** el destino conserva las copias sin vencimiento (P-30 rechazada), en lugar de 30 días.
  - **RF-5:** una copia del bucket no es de confianza si el servidor, o una instancia que comparta el rol, pudo estar comprometido (decisión 3).
  - **RN-4:** el servidor no descifra sus copias. El rol, también desde otras instancias, puede borrarlas o sobrescribirlas; se acepta en R-8 y R-10, sin compensación y con fecha de revisión.
  - **CA-1:** «en el bucket de destino», sin «de la cuenta de destino».
  - **CA-2:** comprueba, con `simulate-principal-policy` y con la política del bucket si la hay:
    - la retención local de 7 días y la del destino;
    - el bloqueo de acceso público del bucket (el de la cuenta no se exige: decisión del 2026-11-05);
    - un rol sin claves guardadas y sin permisos de configuración del bucket;
    - `implicitDeny` en un bucket cualquiera, para que el alcance no crezca más allá de lo declarado en R-10;
    - la clave de `age` fuera del servidor.
  - **§3 (cuenta y bucket de destino), §4 y §10.3 (decisión de Q-1):** sin «otra cuenta».
  - **§10.2:** la frase de que las copias no requieren ADR propio remite a este ADR.
  - **§10.4:** las filas de §8.7(c), que pasa a admitir la excepción, y de §8.7(e), que hoy dice «rol de instancia que solo añade objetos».
  - **§10.6:** las filas de la instancia y del bucket con las decisiones 1 y 2 (rol compartido y su alcance), y una fila nueva para SSM Session Manager.
  - **§14.1 y §14.4:**
    - las filas de extorsión, de «destino en otra cuenta con acceso restringido» y de la retención de datos eliminados;
    - filas nuevas para SSM y para el rol compartido;
    - R-8, R-9 y R-10, con su fecha.
  - **§18 y §19.8:** las menciones a `rclone` contra un bucket con *Object Lock*.
  - **Q-1, Q-3, P-1 y P-25 (§19.1 y §19.2):** se conservan como historia, con la nota «sustituida por ADR-0022».
- **SECURITY.md:** el punto de PostgreSQL y el de recursos de AWS, y en los riesgos residuales:
  - R-8 y R-10, con su fecha de revisión; R-10 con sus dos sentidos;
  - R-9.
- **DEVELOPMENT.md:**
  - §12.1 (AWS);
  - §12.9: qué hace la copia, traer una copia y la advertencia de la decisión 3 sobre restaurar tras un compromiso;
  - §12.13: la fila de CA-1 y CA-2, y que la prueba trimestral de restauración comprueba que la fecha de revisión de R-8 y R-10 no ha vencido.
- **`deploy/aws/README.md`:**
  - tabla y verificación, incluidos `get-bucket-policy` y, si el bucket tiene política, `simulate-principal-policy` con `--resource-policy`;
  - las plantillas `bucket-policy`, `lifecycle` e `instance-role-policy` quedan como referencia de las alternativas A y F, marcadas como no aplicadas.
- **Comentarios de los scripts** (el comportamiento no cambia):
  - `deploy/host.conf.example` («the bucket of the other AWS account»);
  - `scripts/backup-db.sh` («append-only credentials cannot read or list the destination»).
- **ARCHITECTURE.md:** el diagrama de las copias, sin «S3 de otra cuenta (solo añadir)».
- **`specs/README.md`:** el estado de este ADR.

## Texto de la constitución (versión 5.0.0)

La cabecera pasa a `**Versión:** 5.0.0 · **Vigente desde:** 2026-11-05` y §8.7(e) queda así:

> **(e)** Las credenciales de despliegue y de registro DEBEN ser de mínimo alcance (solo lectura o solo añadir).
>
> Las de copias DEBERÍAN ser de mínimo alcance, limitadas a su destino, exclusivas del servidor y de solo añadir sobre un destino que impida borrarlas o
> sobrescribirlas dentro de la retención. Una credencial de copias que no cumpla alguna de esas condiciones es admisible, como única excepción a §3.8 y a
> §8.7(c) para estas credenciales, si el ADR o la spec que define las copias declara:
>
> 1. su alcance real y los sistemas con los que se comparte;
> 2. quién puede borrar o sobrescribir las copias;
> 3. el control compensatorio que mantiene al menos una copia fuera de su alcance, con su antigüedad máxima, o, si no lo hay, la pérdida que se acepta; y
> 4. la aceptación expresa del riesgo por una persona responsable, con su fecha y una fecha de revisión no posterior a seis meses desde la aceptación.
>    Vencida esa fecha sin revisión, la aceptación caduca, y renovarla exige una nueva aceptación expresa.
>
> Una credencial de copias NO DEBE poder cambiar, en ningún caso, la configuración del destino de las copias: entre otras cosas, su acceso público, su
> política, su ciclo de vida, su versionado, sus notificaciones y su borrado.
>
> Las credenciales de despliegue, de registro y de copias NO DEBEN estar en el repositorio ni en la línea de comandos (§4). Ninguna credencial de CI PUEDE
> dar acceso de ejecución al servidor sin un ADR propio con revisión de `security`.

Fila del historial:

> | 5.0.0 | 2026-11-05 | MAJOR: §8.7(e) admite, como única excepción a §3.8 y §8.7(c) para las credenciales de copias, una credencial que no sea de mínimo alcance, limitada a su destino, exclusiva o de solo añadir sobre un destino que impida borrar. Lo exige declarado en el ADR o la spec: su alcance y con qué sistemas se comparte, quién puede borrar o sobrescribir las copias, el control compensatorio o la pérdida aceptada, y la aceptación expresa de una persona responsable con revisión a seis meses como máximo, que caduca si vence. Ninguna credencial de copias puede cambiar la configuración de su destino (ADR-0022). |

## Versión: MAJOR 5.0.0

Este cambio relaja dos reglas, y §1.2(c) exige MAJOR:

- las credenciales de copias pasan de «DEBEN ser de mínimo alcance (solo lectura o solo añadir)» a «DEBERÍAN»;
- se añade una excepción acotada a §3.8 y a §8.7(c) para esas credenciales.

Las credenciales de despliegue y de registro no cambian, y el resto de §8.7 tampoco.

## Impacto en la constitución

Requiere la enmienda de **§8.7(e)** descrita arriba. Esa enmienda crea una excepción a §3.8 y a §8.7(c), sin cambiar su texto. Se cumple §1.2:

- **(a)** este ADR, con motivación, alternativas e impacto;
- **(b)** revisión de `architecture` y `security`, en tres pasadas el 2026-11-05 (§«Revisiones»);
- **(c)** versión 5.0.0 con su fila en el historial.

La aceptación es humana, del solicitante, como exigen `agents/README.md` y `agents/architecture.md`. §8.7(f) no cambia.

## Revisiones (2026-11-05, independientes y de solo lectura, un agente por rol)

- **Primera revisión de `security`: aceptable con cambios.** Hallazgos y su tratamiento:
  - **S-1 (alto):** la copia fuera de línea era trimestral, estaba sin especificar e iba junto a la clave. Se rehízo como P-28 (mensual, de 35 días como máximo, 3 copias, separada de la clave, con SHA-256), y **el solicitante la rechazó**: el riesgo se acepta sin compensación y con fecha de revisión (decisión 3).
  - **S-2 (alto):** faltaban vectores en el análisis. Se añadieron la integridad con la clave pública de `age`, la configuración del bucket, el uso de las credenciales fuera de la instancia, `SendCommand`, Instance Connect, la consola serie, los *data events* de CloudTrail y las copias locales.
  - **S-3 (medio):** §8.7(e) era demasiado abierto. Se acotó, y la tercera revisión lo sustituyó al introducir R-10.
  - **S-4 (medio):** quedaban referencias sueltas en SPEC-0009 y dos P-1. La lista completa está en §«Cambios al aceptar», y las propuestas nuevas son P-28 a P-30.
  - **S-5 (medio):** CA-2 ya no protegía casi nada. Se reescribe; CA-30 se retiró con P-28.
  - **S-6 (medio):** SECURITY.md describía un control que no existe, y faltaban sitios en la lista. Ahora lleva una nota hasta la aceptación, y se añadieron `backup-db.sh`, `host.conf.example` y DEVELOPMENT §12.9.
  - **S-7 (bajo):** retención indefinida. Se propuso P-30, que el solicitante rechazó; se acepta en R-8.
  - **S-8 (bajo):** la alternativa B estaba infravalorada. Se corrigió y se propuso P-29, que el solicitante rechazó.
  - **S-9 (informativo):** sin datos confidenciales.
- **Primera revisión de `architecture`: aceptable con cambios.** Hallazgos y su tratamiento:
  - **A-1:** se conservaba el mínimo alcance con «NO DEBEN». La tercera revisión lo sustituyó: el mínimo alcance pasa a DEBERÍA (R-10).
  - **A-2:** P-1 pasó a P-28.
  - **A-3:** el control compensatorio era compuerta. Tras el rechazo de P-28, la compuerta es la aceptación expresa y fechada.
  - **A-4:** se añadieron el texto de la constitución, la versión y el cumplimiento de §1.2.
  - **A-5:** se corrigió la alternativa B.
  - **A-6 y A-7:** el texto aprobado de SPEC-0009 no se toca hasta la aceptación, y aquí está la lista completa.
  - **A-8:** las comprobaciones de SSM y CloudTrail son compuerta.
  - **A-9:** un único cambio al aceptar, con una nota provisional en DEVELOPMENT §12.9.
  - **A-10:** CA-2 comprueba lo que RN-4 dirá.
  - **A-11:** los riesgos llevan su fecha.
  - **A-12:** redacción y `specs/README.md`, y la Elastic IP llevada a §19.8.
- **Segunda revisión de `security`** (tras el rechazo de P-28): **aceptable con cambios**. Hallazgos y su tratamiento:
  - **R-1:** la decisión 7 se alineó con la decisión 3.
  - **R-2:** el tope de seis meses cuenta desde la aceptación, caduca si vence, consta en SECURITY.md y lo comprueba la prueba trimestral.
  - **R-3:** el alcance se comprueba con `simulate-principal-policy`.
  - **R-4:** se añadió quién acepta.
  - **R-5:** la revisión quedó registrada.
  - **R-6:** nota provisional en DEVELOPMENT §12.9.
  - **R-7:** se añadió que un borrado puede pasar inadvertido.
- **Tercera revisión de `security`** (tras R-10): **aceptable con cambios**. Hallazgos y su tratamiento:
  - **T-1:** «NO PUEDEN» y «Ninguna… PUEDE estar» no son términos de RFC 2119. Ahora dice «NO DEBE poder cambiar, en ningún caso» y «NO DEBEN estar», con el mínimo en frase aparte.
  - **T-2:** la lista del mínimo era cerrada. Ahora es abierta («entre otras cosas»), y la decisión 2 y la compuerta 2 añaden las acciones que faltaban y el bloqueo de la cuenta.
  - **T-3:** la simulación sin `--resource-policy` no ve la política del bucket. Comprobarla es compuerta 2.
  - **T-4:** R-8, la advertencia sobre restaurar, RF-5, RN-4, DEVELOPMENT y SECURITY incluyen las instancias que comparten el rol.
  - **T-5:** la aceptación de R-10 exige que lo acepte, o lo conozca, quien responde de los datos de los otros sistemas, y que se confirme su fecha (compuerta 1).
  - **T-6 y T-7:** SPEC-0009 §18 y §19.10 se ajustaron.
  - **T-8:** las alternativas elegidas dicen en qué eje («destino y compensación», «credencial»).
  - **T-9:** esta entrada, y las notas en S-3 y A-1.
  - **T-10:** CA-2 comprueba `implicitDeny` en un bucket cualquiera.
- **Tercera revisión de `architecture`** (tras R-10): **aceptable con cambios**. Hallazgos y su tratamiento:
  - **B-1 (alto):** el rol compartido chocaba también con §3.8 y §8.7(c). El texto lo declara como única excepción para las credenciales de copias, y la fila del historial y §«Versión» lo recogen.
  - **B-2:** términos de RFC 2119, como en T-1.
  - **B-3:** lista abierta del mínimo, como en T-2.
  - **B-4:** la tercera revisión ya está registrada.
  - **B-5 y B-6:** SPEC-0009 §18 y §19.10 se ajustaron.
  - **B-7:** se añadieron §3, §10.2 y §10.4 de SPEC-0009 a §«Cambios al aceptar».
  - **B-8:** la cita del texto vigente y la fila del historial se corrigieron.
  - **B-9:** título más corto.
  - **B-10:** §8.7(e) se presenta como lista, y la nota de DEVELOPMENT incluye las otras instancias.
