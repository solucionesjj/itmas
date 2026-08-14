export type SecurityGroupRuleStatus =
  | 'pendiente'
  | 'revisado'
  | 'autorizado'
  | 'eliminado';

export type SecurityGroupRuleDirection = 'ingress' | 'egress';

export interface AttachedResource {
  resourceType:
    | 'ec2-instance'
    | 'rds-instance'
    | 'load-balancer'
    | 'lambda'
    | 'other';
  resourceId: string;
  resourceName?: string;
}

export interface SecurityGroupRule {
  _id: string;
  awsAccountId: string;
  region: string;
  vpcId: string;
  securityGroupId: string;
  securityGroupName: string;
  attachedResources: AttachedResource[];
  ruleId: string;
  ruleName: string | null;
  direction: SecurityGroupRuleDirection;
  source: string;
  destination: string;
  protocol: string;
  portRange: string | null;
  status: SecurityGroupRuleStatus;
  createdAt: string;
  reviewObservation: string | null;
  reviewedAt: string | null;
  authorizationObservation: string | null;
  authorizedAt: string | null;
}

// Mirrors backend's SecurityGroupRuleSortField allowlist — never a raw
// client-typed string reaches the API.
export type SecurityGroupRuleSortField =
  | 'securityGroupName'
  | 'securityGroupId'
  | 'ruleName'
  | 'ruleId'
  | 'source'
  | 'destination'
  | 'protocol'
  | 'portRange'
  | 'status'
  | 'createdAt'
  | 'reviewedAt'
  | 'authorizedAt'
  | 'region';

export type SortDirection = 'asc' | 'desc';

// Per-feature local copy, matching the established convention (no shared
// core model) — see devices/alert.model.ts.
export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  limit: number;
}

export interface DistinctGroup {
  securityGroupId: string;
  securityGroupName: string;
}

export interface SecurityGroupRulesQuery {
  q?: string;
  securityGroupId?: string;
  status?: SecurityGroupRuleStatus;
  createdFrom?: string;
  createdTo?: string;
  reviewedFrom?: string;
  reviewedTo?: string;
  authorizedFrom?: string;
  authorizedTo?: string;
  sortBy?: SecurityGroupRuleSortField;
  sortDir?: SortDirection;
  page?: number;
  limit?: number;
}

export type ExportFormat = 'csv' | 'pdf';

export type SecurityGroupRulesExportQuery = Omit<
  SecurityGroupRulesQuery,
  'sortBy' | 'sortDir' | 'page' | 'limit'
> & { format: ExportFormat };
