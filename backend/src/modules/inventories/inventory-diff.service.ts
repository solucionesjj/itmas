import { Injectable } from '@nestjs/common';

export type InventoryResourceKey = 'cpu' | 'ram' | 'disks';

export interface InventorySnapshot {
  cpu: { model: string; cores: number };
  ram: { totalGB: number };
  disks: Array<{ name: string; sizeGB: number }>;
}

export interface InventoryDiffResult {
  changed: boolean;
  changes: InventoryResourceKey[];
}

const NO_CHANGE: InventoryDiffResult = { changed: false, changes: [] };

/**
 * RF-03: compares two consecutive inventories for a device and reports which
 * of cpu/ram/disks changed. Pure and side-effect free — sub-phase 1.3's alert
 * engine will reuse this to decide whether to raise a `resource_change`
 * alert against `alert_rules`; this sub-phase only logs the detection.
 */
@Injectable()
export class InventoryDiffService {
  compare(
    previous: InventorySnapshot | null,
    current: InventorySnapshot,
  ): InventoryDiffResult {
    if (!previous) {
      return NO_CHANGE;
    }

    const changes: InventoryResourceKey[] = [];

    if (
      previous.cpu.model !== current.cpu.model ||
      previous.cpu.cores !== current.cpu.cores
    ) {
      changes.push('cpu');
    }

    if (previous.ram.totalGB !== current.ram.totalGB) {
      changes.push('ram');
    }

    if (!this.disksEqual(previous.disks, current.disks)) {
      changes.push('disks');
    }

    return { changed: changes.length > 0, changes };
  }

  private disksEqual(
    a: InventorySnapshot['disks'],
    b: InventorySnapshot['disks'],
  ): boolean {
    if (a.length !== b.length) {
      return false;
    }

    const sortByName = (
      disks: InventorySnapshot['disks'],
    ): InventorySnapshot['disks'] =>
      [...disks].sort((x, y) => x.name.localeCompare(y.name));

    const sortedA = sortByName(a);
    const sortedB = sortByName(b);

    return sortedA.every(
      (disk, index) =>
        disk.name === sortedB[index].name &&
        disk.sizeGB === sortedB[index].sizeGB,
    );
  }
}
