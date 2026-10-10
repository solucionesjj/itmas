/** Contracts of the device inventory API (SPEC-0003 §12.1). Device data is untrusted: always render it as text. */

export type DeviceType = 'workstation' | 'server';
export type DeviceStatus = 'pending' | 'active' | 'deleted';
export type DeviceStatusFilter = 'current' | 'pending' | 'active' | 'deleted' | 'all';
export type DeviceSort = 'name' | 'owner' | 'lastCollectedAt';

export interface Paged<T> {
  readonly items: readonly T[];
  readonly page: number;
  readonly pageSize: number;
  readonly total: number;
}

/** Collaborator of a device: `name` is null when no ITMas user has the e-mail (RF-4b). */
export interface DeviceOwner {
  readonly email: string;
  readonly name: string | null;
}

export interface DeviceListItem {
  readonly id: string;
  readonly name: string;
  readonly type: DeviceType;
  readonly status: DeviceStatus;
  readonly hostname: string | null;
  readonly owner: DeviceOwner | null;
  readonly lastCollectedAt: string | null;
  readonly reportCount: number;
}

export interface DeviceDetail {
  readonly id: string;
  readonly name: string;
  readonly type: DeviceType;
  readonly status: DeviceStatus;
  readonly revision: number;
  readonly owner: DeviceOwner | null;
  readonly hostname: string | null;
  readonly osName: string | null;
  readonly osVersion: string | null;
  readonly createdAt: string;
  readonly enrolledAt: string | null;
  readonly enrollmentCodeIssuedAt: string | null;
  readonly hasPendingEnrollmentCode: boolean;
  readonly lastSeenAt: string | null;
  readonly lastCollectedAt: string | null;
  readonly deletedAt: string | null;
  readonly reportCount: number;
}

export interface DeviceInput {
  readonly name: string;
  readonly type: DeviceType;
  readonly ownerEmail: string | null;
}

/** Response of the creation: the only place, with the regeneration, where the code is returned (ADR-0012). */
export interface DeviceCreated {
  readonly id: string;
  readonly name: string;
  readonly type: DeviceType;
  readonly status: DeviceStatus;
  readonly owner: DeviceOwner | null;
  readonly createdAt: string;
  readonly enrollmentCode: string;
  readonly enrollmentCodeIssuedAt: string;
}

export interface EnrollmentCode {
  readonly enrollmentCode: string;
  readonly issuedAt: string;
}

export interface DeviceReportListItem {
  readonly id: string;
  readonly collectedAt: string;
  readonly receivedAt: string;
  readonly connectionIp: string | null;
  readonly publicIp: string | null;
  readonly countryCode: string | null;
  readonly region: string | null;
  readonly city: string | null;
  readonly agentVersion: string;
  readonly unavailableCount: number;
  readonly isLatest: boolean;
}

export interface DeviceHeader {
  readonly id: string;
  readonly name: string;
  readonly type: DeviceType;
  readonly status: DeviceStatus;
  readonly owner: DeviceOwner | null;
}

export interface DeviceReport {
  readonly id: string;
  readonly device: DeviceHeader;
  readonly collectedAt: string;
  readonly receivedAt: string;
  readonly connectionIp: string | null;
  readonly isLatest: boolean;
  readonly inventory: Inventory;
}

/** Inventory contract v1 (SPEC-0002 §12.2). A null section is listed in `unavailable` with its reason. */
export interface Inventory {
  readonly schemaVersion: number;
  readonly collectedAt: string;
  readonly agentVersion: string;
  readonly system: {
    readonly hostname: string;
    readonly osFamily: string;
    readonly osName: string;
    readonly osVersion: string;
    readonly osBuild: string | null;
    readonly architecture: string;
  };
  readonly hardware: {
    readonly memoryBytes: number | null;
    readonly cpus: readonly Cpu[] | null;
    readonly disks: readonly Disk[] | null;
    readonly volumes: readonly Volume[] | null;
  };
  readonly network: {
    readonly interfaces: readonly NetworkInterface[] | null;
    readonly publicNetwork: PublicNetwork | null;
  };
  readonly software: { readonly applications: readonly string[] | null };
  readonly accounts: { readonly users: readonly LocalAccount[] | null };
  readonly security: { readonly firewall: Firewall | null };
  readonly unavailable: readonly UnavailableSection[];
}

export interface Cpu {
  readonly model: string;
  readonly physicalCores: number;
  readonly logicalProcessors: number;
}

export interface Disk {
  readonly id: string;
  readonly model: string | null;
  readonly sizeBytes: number;
  readonly mediaType: string;
  readonly busType: string;
}

export interface Volume {
  readonly mountPoint: string;
  readonly fileSystem: string | null;
  readonly sizeBytes: number;
  readonly freeBytes: number;
  readonly diskId: string | null;
  readonly isSystem: boolean;
  readonly encryption: { readonly status: string; readonly method: string | null };
}

export interface NetworkInterface {
  readonly name: string;
  readonly ipv4: readonly string[];
  readonly ipv6: readonly string[];
}

export interface PublicNetwork {
  readonly ip: string;
  readonly country: string;
  readonly region: string | null;
  readonly city: string | null;
  readonly org: string | null;
}

export interface LocalAccount {
  readonly name: string;
  readonly enabled: boolean;
  readonly isAdmin: boolean;
  readonly groups: readonly string[];
}

export interface Firewall {
  readonly status: string;
  readonly details: readonly { readonly name: string; readonly enabled: boolean }[];
}

export interface UnavailableSection {
  readonly section: string;
  readonly reason: string;
}
