# Recursos de AWS de los que dependen los controles (SPEC-0009 §10.6, ADR-0022, constitución §8.7)

Los crea quien administra AWS; este repositorio solo guarda las plantillas, **sin identificadores de cuenta, de instancia ni nombres reales**
(los marcadores van entre `<…>`). El repositorio es **público**: tampoco se escriben aquí nombres de *buckets*, roles, regiones ni cuentas reales. Nada de esto se crea desde el servidor. Se verifica con los criterios CA-1, CA-2, CA-5, CA-19 y CA-27 de SPEC-0009.

**Qué hay en producción** ([ADR-0022](../../specs/architecture/adr-0022-backup-destination-same-account.md), aceptado el 2026-11-05):

- Las copias van a un bucket de S3 de la **misma cuenta** que el servidor, sin versionado, *Object Lock* ni vencimiento.
- El rol de la instancia, compartido con otras instancias, puede subir, listar, leer y borrar, pero **no** cambiar la configuración del bucket.
- Los riesgos aceptados son R-8, R-9 y R-10 de SPEC-0009 §14.4, con revisión el 2027-05-05.

Las plantillas de este directorio describen la **alternativa más segura**, que **no está aplicada**: las alternativas A y F de ADR-0022.

| Recurso | Propiedad exigida | Plantilla |
|---|---|---|
| Instancia EC2 | Ubuntu 26.04 LTS, **`x86_64`** (el CI construye `linux/amd64`); **IMDSv2 obligatorio con *hop limit* 1** | — |
| Rol de la instancia | Sin claves guardadas. **NO DEBE poder cambiar la configuración del bucket de las copias**: entre otras, `PutBucketPolicy`, `DeleteBucketPolicy`, `PutBucketAcl`, `PutBucketOwnershipControls`, `PutBucketPublicAccessBlock`, `PutLifecycleConfiguration`, `PutBucketVersioning`, `PutBucketNotification`, `PutReplicationConfiguration`, `DeleteBucket` y `PutAccountPublicAccessBlock` (constitución §8.7 e). En producción además sube, lista, lee y borra en todo el bucket, lo comparten otras instancias y alcanza un segundo bucket (R-10) | `instance-role-policy.json.template` (alternativa F, no aplicada) |
| Volumen EBS | **Cifrado** (se marca al crear el volumen) | — |
| *Security group* | Entrada **solo** 22/tcp, 80/tcp y 443/tcp (los mismos puertos que `ufw`), también en IPv6 | — |
| Bucket de destino | **Bloqueo de acceso público** (las cuatro opciones). En producción: misma cuenta, sin política propia, sin versionado, *Object Lock* ni vencimiento (R-8) | `bucket-policy.json.template`, `lifecycle.json.template` (alternativa A, no aplicada) |

## Cómo se configura (resumen)

1. **Bucket.** Activa el bloqueo de acceso público.
   - Si algún día se aplica la alternativa A, el bucket va en otra cuenta y se crea con *Object Lock*, que solo se puede activar al crearlo, y con versionado. Lleva además propietario forzado, cifrado SSE-S3, `bucket-policy.json.template` y `lifecycle.json.template`.
2. **Rol de la instancia.** Quita al rol, o deniégale de forma explícita, las acciones de configuración del bucket de la tabla. Exige IMDSv2:

   ```bash
   aws ec2 modify-instance-metadata-options --instance-id <INSTANCE_ID> --http-tokens required --http-put-response-hop-limit 1 --http-endpoint enabled
   ```

   La alternativa F es un rol propio para ITMas, con `AmazonSSMManagedInstanceCore` y `instance-role-policy.json.template` (subir al prefijo de las copias).

3. **Servidor.** `rclone` usa el rol (`RCLONE_S3_ENV_AUTH=true` en `/etc/itmas3/host.conf`): no hay claves guardadas. Con copias de más de 200 MiB, `rclone` sube por partes y
   necesita además `s3:AbortMultipartUpload` y `s3:ListMultipartUploadParts`; hoy las copias son pequeñas.

## Verificación (sin datos ni identificadores en los registros)

Se hace **desde tu equipo**, con credenciales de administración de la cuenta. El rol del servidor no puede leer IAM, EC2 ni CloudTrail, y no debe poder hacerlo.

```bash
# Volumen cifrado
aws ec2 describe-volumes --filters Name=attachment.instance-id,Values=<INSTANCE_ID> --query 'Volumes[].Encrypted'
# IMDSv2 con hop limit 1
aws ec2 describe-instances --instance-ids <INSTANCE_ID> --query 'Reservations[].Instances[].MetadataOptions'
# Reglas de entrada del security group: solo 22, 80 y 443
aws ec2 describe-security-groups --group-ids <SG_ID> --query 'SecurityGroups[].IpPermissions[].[FromPort,ToPort]'
```

Bucket y rol (CA-1, CA-2):

```bash
aws s3api get-public-access-block --bucket <BUCKET>                  # las cuatro opciones en true
aws s3api get-bucket-policy --bucket <BUCKET> --query Policy --output text   # en producción: NoSuchBucketPolicy
```

Con el ARN del rol en `ROLE_ARN`, las acciones de configuración deben salir `implicitDeny` o `explicitDeny`. Si el bucket tiene política propia, añade a cada simulación
`--resource-policy "$(aws s3api get-bucket-policy --bucket <BUCKET> --query Policy --output text)"`: sin ella solo se evalúan las políticas del rol.

```bash
aws iam simulate-principal-policy --policy-source-arn "$ROLE_ARN" --action-names s3:PutBucketPolicy s3:DeleteBucketPolicy s3:PutBucketAcl s3:PutBucketOwnershipControls s3:PutBucketPublicAccessBlock s3:PutLifecycleConfiguration s3:PutBucketVersioning s3:PutBucketNotification s3:PutReplicationConfiguration s3:DeleteBucket --resource-arns arn:aws:s3:::<BUCKET> --query 'EvaluationResults[].[EvalActionName,EvalDecision]' --output table
aws iam simulate-principal-policy --policy-source-arn "$ROLE_ARN" --action-names s3:PutAccountPublicAccessBlock --resource-arns "*" --query 'EvaluationResults[].[EvalActionName,EvalDecision]' --output table
```

El alcance del rol no debe crecer más allá de lo declarado en R-10. Simula una ruta cada vez: con varias, la salida puede mostrar una plantilla genérica en lugar de las rutas.

```bash
aws iam simulate-principal-policy --policy-source-arn "$ROLE_ARN" --action-names s3:GetObject s3:PutObject s3:DeleteObject --resource-arns "arn:aws:s3:::<BUCKET>/<PREFIX>/prueba" --query 'EvaluationResults[].[EvalActionName,EvalResourceName,EvalDecision]' --output table   # allowed
aws iam simulate-principal-policy --policy-source-arn "$ROLE_ARN" --action-names s3:GetObject s3:PutObject s3:DeleteObject --resource-arns "arn:aws:s3:::otro-bucket-cualquiera/prueba" --query 'EvaluationResults[].[EvalActionName,EvalResourceName,EvalDecision]' --output table   # implicitDeny
```

Accesos de administración por AWS (R-9):

```bash
aws ec2 get-serial-console-access-status                             # en producción: activada, aceptada en R-9
aws cloudtrail lookup-events --lookup-attributes AttributeKey=EventName,AttributeValue=StartSession --max-results 5 --query 'Events[].[EventTime,Username]' --output table
aws cloudtrail lookup-events --lookup-attributes AttributeKey=EventName,AttributeValue=SendCommand --max-results 5 --query 'Events[].[EventTime,Username]' --output table
```

Desde el servidor, con el rol de la instancia: comprueba que `rclone` sube. Los comandos se ejecutan como `root`, que es quien lee `host.conf`; las variables `RCLONE_S3_*` de ese archivo están exportadas. Los indicadores `--no-check-dest`, `--s3-no-check-bucket` y `--s3-no-head` son los que usa `scripts/backup-db.sh`; `install-host-tasks.sh check` comprueba que tu versión de `rclone` los admite.

```bash
sudo systemctl start itmas3-backup.service && sudo journalctl -u itmas3-backup -n 30   # «copied to the external destination»
```

Sin versionado, escribir de nuevo una clave existente la **reemplaza**, y el rol puede borrarla: un compromiso puede destruir las copias (R-8).

## Recuperar una copia (persona administradora, desde su equipo)

La descarga se hace con credenciales de administración, no desde el servidor. El descifrado usa la clave privada de `age`, que no está en el servidor (DEVELOPMENT.md §12.9):

```bash
aws s3 ls s3://<BUCKET>/<PREFIX>/ --recursive | tail                                 # el volcado más reciente y su .counts.age
aws s3 cp s3://<BUCKET>/<PREFIX>/<itmas3-....dump.age> .                              # y el .counts.age que lo acompaña
```

Si se sospecha que el servidor, o una instancia que comparta su rol, estuvo comprometido, la copia no es de confianza aunque la restauración termine bien (ADR-0022, decisión 3).
