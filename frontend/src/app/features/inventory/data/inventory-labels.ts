import { DeviceOwner, DeviceStatus, DeviceType } from './inventory.models';

/** Spanish labels of the normalized values of the inventory (SPEC-0003 RF-18). Unknown values are shown as received. */
const LABELS = {
  type: { workstation: 'Estación de trabajo', server: 'Servidor' },
  status: { pending: 'Pendiente', active: 'Activo', deleted: 'Eliminado' },
  encryption: {
    encrypted: 'Cifrado',
    encrypting: 'Cifrando',
    suspended: 'Suspendido',
    'not-encrypted': 'Sin cifrar',
    unknown: 'Desconocido',
  },
  encryptionMethod: { bitlocker: 'BitLocker', filevault: 'FileVault', apfs: 'APFS', luks: 'LUKS' },
  firewall: { active: 'Activo', inactive: 'Inactivo', unknown: 'Desconocido' },
  mediaType: { ssd: 'SSD', hdd: 'HDD', unknown: 'Desconocido' },
  busType: {
    nvme: 'NVMe',
    sata: 'SATA',
    sas: 'SAS',
    scsi: 'SCSI',
    usb: 'USB',
    virtual: 'Virtual',
    unknown: 'Desconocido',
  },
  osFamily: { windows: 'Windows', linux: 'Linux', macos: 'macOS' },
  reason: {
    'permission-denied': 'Permiso denegado',
    'not-supported': 'No compatible',
    timeout: 'Tiempo agotado',
    error: 'Error',
  },
  section: {
    'hardware.memory': 'Memoria RAM',
    'hardware.cpus': 'Procesadores',
    'hardware.disks': 'Discos',
    'hardware.volumes': 'Volúmenes',
    'network.interfaces': 'Interfaces de red',
    'network.publicNetwork': 'IP pública',
    'software.applications': 'Software instalado',
    'accounts.users': 'Usuarios locales',
    'security.firewall': 'Firewall',
  },
} satisfies Record<string, Record<string, string>>;

export type LabelKind = keyof typeof LABELS;

export function label(kind: LabelKind, value: string | null | undefined): string {
  if (value === null || value === undefined || value === '') {
    return 'No disponible';
  }
  return (LABELS[kind] as Record<string, string>)[value] ?? value;
}

export const typeLabel = (type: DeviceType): string => label('type', type);

export const statusLabel = (status: DeviceStatus): string => label('status', status);

/** Text of the "Usuario asociado" column (RF-12): collaborator name, else e-mail, else the definition's wording. */
export function ownerLabel(owner: DeviceOwner | null): string {
  return owner?.name ?? owner?.email ?? 'Sin usuario asociado';
}

/** Country name in Spanish from its ISO 3166-1 alpha-2 code; the code itself if the browser does not know it. */
export function countryName(code: string | null | undefined): string | null {
  if (!code) {
    return null;
  }
  try {
    return new Intl.DisplayNames(['es'], { type: 'region' }).of(code) ?? code;
  } catch {
    return code;
  }
}
