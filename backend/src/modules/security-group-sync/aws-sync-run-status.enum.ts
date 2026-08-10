// No 'running' state: a run is built in memory and persisted once, atomically,
// at completion (ADR-0013/0015's synchronous, in-process execution model —
// no queue/worker, so there is no meaningful "in progress" document to read).
export enum AwsSyncRunStatus {
  SUCCESS = 'success',
  PARTIAL_FAILURE = 'partial_failure',
  FAILURE = 'failure',
}
