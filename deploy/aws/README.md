# Recursos de AWS de los que dependen los controles (SPEC-0009 §10.6, constitución §8.7)

Los crea quien administra AWS; este repositorio solo guarda las plantillas, **sin identificadores de cuenta, de instancia ni nombres reales**
(los marcadores van entre `<…>`). El repositorio es **público**: tampoco se escriben aquí nombres de *buckets*, regiones ni cuentas reales. Nada de esto se crea desde el servidor; desde él solo se hace la comprobación final. Se verifica con los criterios CA-1, CA-2, CA-5, CA-19 y CA-27 de SPEC-0009.

| Recurso | Propiedad exigida | Plantilla |
|---|---|---|
| Instancia EC2 | Ubuntu 24.04 LTS, **`x86_64`** (el CI construye `linux/amd64`); **IMDSv2 obligatorio con *hop limit* 1**; el rol de instancia solo puede **añadir** objetos al prefijo de las copias | `instance-role-policy.json.template` |
| Volumen EBS | **Cifrado** (se marca al crear el volumen) | — |
| *Security group* | Entrada **solo** 22/tcp, 80/tcp y 443/tcp (los mismos puertos que `ufw`), también en IPv6 | — |
| Bucket de destino, **en otra cuenta** | Versionado y *Object Lock* con retención por defecto de 30 días; **propietario del bucket forzado** (`BucketOwnerEnforced`, sin ACL); cifrado por defecto **SSE-S3** (no SSE-KMS: exigiría dar permisos de la clave al rol de otra cuenta); *Block Public Access*; política que permite `PutObject` solo al rol de la instancia y le niega leer, listar y borrar; vencimiento a los 30 días | `bucket-policy.json.template`, `lifecycle.json.template` |

## Cómo se crean (resumen)

1. **Cuenta de destino.** Crea el bucket con *Object Lock* activado (solo se puede activar al crearlo; activa también el versionado), con *Block Public Access*, **propietario del bucket forzado** y cifrado por defecto SSE-S3.
   Fija la retención por defecto en 30 días: `GOVERNANCE` permite que un administrador con un permiso especial la acorte; `COMPLIANCE` no lo permite a nadie, ni a la cuenta raíz. Aplica `bucket-policy.json.template` y `lifecycle.json.template`
   con los marcadores sustituidos.
2. **Cuenta del servidor.** Crea un rol de instancia con `instance-role-policy.json.template`, asócialo a la instancia y exige IMDSv2:

   ```bash
   aws ec2 modify-instance-metadata-options --instance-id <INSTANCE_ID> --http-tokens required --http-put-response-hop-limit 1 --http-endpoint enabled
   ```

3. **Servidor.** `rclone` usa el rol (`RCLONE_S3_ENV_AUTH=true` en `/etc/itmas3/host.conf`): no hay claves guardadas. Con copias de más de 200 MiB, `rclone` sube por partes y
   necesita además `s3:AbortMultipartUpload` y `s3:ListMultipartUploadParts` en las dos políticas; hoy las copias son pequeñas.

## Verificación (sin datos ni identificadores en los registros)

```bash
# Volumen cifrado
aws ec2 describe-volumes --filters Name=attachment.instance-id,Values=<INSTANCE_ID> --query 'Volumes[].Encrypted'
# IMDSv2 con hop limit 1
aws ec2 describe-instances --instance-ids <INSTANCE_ID> --query 'Reservations[].Instances[].MetadataOptions'
# Reglas de entrada del security group: solo 22, 80 y 443
aws ec2 describe-security-groups --group-ids <SG_ID> --query 'SecurityGroups[].IpPermissions[].[FromPort,ToPort]'
```

En la cuenta de destino, con credenciales de su administración (CA-1, CA-2: versionado, bloqueo de objetos, ciclo de vida, propietario forzado, política y cifrado):

```bash
aws s3api get-bucket-versioning --bucket <BUCKET>                    # Status: Enabled
aws s3api get-object-lock-configuration --bucket <BUCKET>            # ObjectLockEnabled y retención por defecto de 30 días
aws s3api get-bucket-ownership-controls --bucket <BUCKET>            # ObjectOwnership: BucketOwnerEnforced
aws s3api get-bucket-lifecycle-configuration --bucket <BUCKET>       # expiración a los 30 días del prefijo de las copias
aws s3api get-bucket-encryption --bucket <BUCKET>                    # AES256 (SSE-S3)
aws s3api get-public-access-block --bucket <BUCKET>                  # las cuatro opciones en true
aws s3api get-bucket-policy --bucket <BUCKET> --query Policy --output text   # PutObject solo al rol; denegaciones del resto
```

Desde el servidor, con el rol de la instancia (el servidor puede añadir, pero no leer, listar ni borrar). Los comandos se ejecutan como `root`, que es quien lee `host.conf`; las variables `RCLONE_S3_*` de ese archivo están exportadas:

```bash
echo prueba | sudo sh -c '. /etc/itmas3/host.conf && RCLONE_CONFIG=/dev/null rclone rcat --no-check-dest --s3-no-check-bucket --s3-no-head "${BACKUP_REMOTE%/}/prueba-conexion.txt"'
sudo sh -c '. /etc/itmas3/host.conf && RCLONE_CONFIG=/dev/null rclone lsf "$BACKUP_REMOTE"'                              # debe fallar (acceso denegado)
sudo sh -c '. /etc/itmas3/host.conf && RCLONE_CONFIG=/dev/null rclone deletefile "${BACKUP_REMOTE%/}/prueba-conexion.txt"'  # debe fallar (acceso denegado)
```

El objeto de prueba queda retenido 30 días por el bloqueo y vence por el ciclo de vida; no se puede borrar antes, ni desde el servidor. Los indicadores `--no-check-dest`, `--s3-no-check-bucket` y `--s3-no-head` son los que usa `scripts/backup-db.sh`; `install-host-tasks.sh check` comprueba que tu versión de `rclone` los admite.
Escribir de nuevo una clave existente solo añade una versión: el servidor no puede sobrescribir ni destruir una copia.

## Recuperar una copia (persona administradora de la cuenta de destino)

El servidor no puede leer las copias, así que la descarga se hace desde la cuenta de destino y el descifrado, con la clave privada de `age` que no está en el servidor (DEVELOPMENT.md §12.9):

```bash
aws s3 ls s3://<BUCKET>/<PREFIX>/ --recursive | tail                                 # el volcado más reciente y su .counts.age
aws s3 cp s3://<BUCKET>/<PREFIX>/<itmas3-....dump.age> .                              # y el .counts.age que lo acompaña
aws s3api list-object-versions --bucket <BUCKET> --prefix <PREFIX>/<archivo>           # si hubiera varias versiones de una clave
aws s3api get-object --bucket <BUCKET> --key <PREFIX>/<archivo> --version-id <ID> salida   # una versión concreta
```
