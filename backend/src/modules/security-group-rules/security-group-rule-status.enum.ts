// Spanish values match spec.md §21's literal contract (RF-22) — this is a
// user-facing control field, not an internal code, so the wire values are
// the domain terms IT-MAS's review/authorization workflow actually uses.
export enum SecurityGroupRuleStatus {
  PENDIENTE = 'pendiente',
  REVISADO = 'revisado',
  AUTORIZADO = 'autorizado',
  ELIMINADO = 'eliminado',
}
