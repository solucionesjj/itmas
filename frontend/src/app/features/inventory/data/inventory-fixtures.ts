import { DeviceListItem, DeviceReport, Inventory } from './inventory.models';

/** Test data of the inventory feature (contract v1, SPEC-0002 §12.2). Not used by the application. */
export const INVENTORY: Inventory = {
  schemaVersion: 1,
  collectedAt: '2026-10-09T12:00:00Z',
  agentVersion: '1.1.0',
  system: {
    hostname: 'srv-app-01',
    osFamily: 'linux',
    osName: 'Ubuntu',
    osVersion: '24.04',
    osBuild: '6.8.0-45-generic',
    architecture: 'x64',
  },
  hardware: {
    memoryBytes: 17179869184,
    cpus: [{ model: 'Intel Xeon E-2236', physicalCores: 6, logicalProcessors: 12 }],
    disks: [
      {
        id: 'nvme0n1',
        model: 'Samsung 980',
        sizeBytes: 512110190592,
        mediaType: 'ssd',
        busType: 'nvme',
      },
    ],
    volumes: [
      {
        mountPoint: '/',
        fileSystem: 'ext4',
        sizeBytes: 100,
        freeBytes: 25,
        diskId: 'nvme0n1',
        isSystem: true,
        encryption: { status: 'encrypted', method: 'luks' },
      },
    ],
  },
  network: {
    interfaces: [{ name: 'eth0', ipv4: ['10.0.0.5'], ipv6: [] }],
    publicNetwork: {
      ip: '203.0.113.25',
      country: 'CO',
      region: 'Bogotá D.C.',
      city: 'Bogotá',
      org: 'AS0 Ejemplo',
    },
  },
  software: { applications: ['zsh', 'nginx', 'Git'] },
  accounts: { users: [{ name: 'admin', enabled: true, isAdmin: true, groups: ['sudo', 'adm'] }] },
  security: { firewall: { status: 'active', details: [{ name: 'ufw', enabled: true }] } },
  unavailable: [],
};

export function reportOf(
  overrides: Partial<DeviceReport> = {},
  inventory: Partial<Inventory> = {},
): DeviceReport {
  return {
    id: 'r1',
    device: { id: 'd1', name: 'Servidor web', type: 'server', status: 'active', owner: null },
    collectedAt: '2026-10-09T12:00:00Z',
    receivedAt: '2026-10-09T12:00:05Z',
    connectionIp: '203.0.113.25',
    isLatest: true,
    inventory: { ...INVENTORY, ...inventory },
    ...overrides,
  };
}

export function deviceOf(overrides: Partial<DeviceListItem> = {}): DeviceListItem {
  return {
    id: 'd1',
    name: 'pc-ana',
    type: 'workstation',
    status: 'active',
    hostname: 'pc-ana',
    owner: null,
    lastCollectedAt: null,
    reportCount: 0,
    ...overrides,
  };
}
