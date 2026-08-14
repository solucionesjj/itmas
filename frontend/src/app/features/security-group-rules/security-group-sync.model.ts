export type AwsSyncTriggerType = 'manual' | 'automated';
export type AwsSyncRunStatus = 'success' | 'partial_failure' | 'failure';

export interface GroupSyncResult {
  region: string;
  vpcId: string;
  groupId: string;
  groupName: string;
  outcome: 'success' | 'error';
  ruleCount: number;
  errorMessage?: string;
}

export interface AwsSyncRunSummary {
  groupsProcessed: number;
  groupsFailed: number;
  rulesProcessed: number;
  rulesCreated: number;
  rulesMarkedDeleted: number;
}

export interface AwsSyncRun {
  _id: string;
  triggerType: AwsSyncTriggerType;
  triggeredBy: string | null;
  startedAt: string;
  finishedAt: string;
  status: AwsSyncRunStatus;
  regionsChecked: string[];
  vpcsChecked: string[];
  groupResults: GroupSyncResult[];
  summary: AwsSyncRunSummary;
}

export interface AwsSyncSummary {
  pendienteCount: number;
  pendienteSinceLastRun: number;
  lastRunAt: string | null;
  lastRunStatus: AwsSyncRunStatus | null;
}
