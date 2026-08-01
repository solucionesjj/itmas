// spec.md's UC-03/UC-04 only ever describe login/logout events at the OS and
// database level; extend here (not by hardcoding elsewhere) if a later
// sub-phase needs more actions.
export enum AccessEventAction {
  LOGIN = 'login',
  LOGOUT = 'logout',
}
