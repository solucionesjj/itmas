// Runtime-checkable mirror of InventoryDiffService's InventoryResourceKey
// union ('cpu'|'ram'|'disks') — class-validator needs an actual enum object,
// not a type alias, to validate `config.resources` entries. Keep in sync.
export enum AlertableResource {
  CPU = 'cpu',
  RAM = 'ram',
  DISKS = 'disks',
}
