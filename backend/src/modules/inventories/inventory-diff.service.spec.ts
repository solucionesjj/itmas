import {
  InventoryDiffService,
  InventorySnapshot,
} from './inventory-diff.service';

function snapshot(
  overrides: Partial<InventorySnapshot> = {},
): InventorySnapshot {
  return {
    cpu: { model: 'Intel i7', cores: 8 },
    ram: { totalGB: 16 },
    disks: [{ name: 'C', sizeGB: 512 }],
    ...overrides,
  };
}

describe('InventoryDiffService', () => {
  let service: InventoryDiffService;

  beforeEach(() => {
    service = new InventoryDiffService();
  });

  it('reports no change when there is no previous inventory', () => {
    const result = service.compare(null, snapshot());
    expect(result).toEqual({ changed: false, changes: [] });
  });

  it('reports no change when nothing differs', () => {
    const result = service.compare(snapshot(), snapshot());
    expect(result).toEqual({ changed: false, changes: [] });
  });

  it('detects a cpu change', () => {
    const result = service.compare(
      snapshot(),
      snapshot({ cpu: { model: 'Intel i9', cores: 8 } }),
    );
    expect(result).toEqual({ changed: true, changes: ['cpu'] });
  });

  it('detects a cpu core-count change', () => {
    const result = service.compare(
      snapshot(),
      snapshot({ cpu: { model: 'Intel i7', cores: 16 } }),
    );
    expect(result).toEqual({ changed: true, changes: ['cpu'] });
  });

  it('detects a ram change', () => {
    const result = service.compare(
      snapshot(),
      snapshot({ ram: { totalGB: 32 } }),
    );
    expect(result).toEqual({ changed: true, changes: ['ram'] });
  });

  it('detects a disks change (size)', () => {
    const result = service.compare(
      snapshot(),
      snapshot({ disks: [{ name: 'C', sizeGB: 1024 }] }),
    );
    expect(result).toEqual({ changed: true, changes: ['disks'] });
  });

  it('detects a disks change (count)', () => {
    const result = service.compare(
      snapshot(),
      snapshot({
        disks: [
          { name: 'C', sizeGB: 512 },
          { name: 'D', sizeGB: 256 },
        ],
      }),
    );
    expect(result).toEqual({ changed: true, changes: ['disks'] });
  });

  it('does not treat reordered-but-identical disks as changed', () => {
    const previous = snapshot({
      disks: [
        { name: 'C', sizeGB: 512 },
        { name: 'D', sizeGB: 256 },
      ],
    });
    const current = snapshot({
      disks: [
        { name: 'D', sizeGB: 256 },
        { name: 'C', sizeGB: 512 },
      ],
    });
    const result = service.compare(previous, current);
    expect(result).toEqual({ changed: false, changes: [] });
  });

  it('detects multiple simultaneous changes', () => {
    const result = service.compare(
      snapshot(),
      snapshot({
        cpu: { model: 'Intel i9', cores: 16 },
        ram: { totalGB: 64 },
      }),
    );
    expect(result.changed).toBe(true);
    expect(result.changes.sort()).toEqual(['cpu', 'ram']);
  });
});
