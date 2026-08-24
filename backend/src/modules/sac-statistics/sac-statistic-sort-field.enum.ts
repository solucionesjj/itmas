/**
 * Sort whitelist for `GET /sac-statistics` (BL-032 CA-2). Same
 * "sort keys are an enum, never a raw client string" posture as
 * SecurityGroupRuleSortField — an arbitrary field name would let a caller sort
 * on an unindexed field and turn the listing into a collection scan.
 */
export enum SacStatisticSortField {
  GENERATED_AT = 'generatedAt',
  DATABASE_NAME = 'databaseName',
  TOTAL_SIZE_GB = 'totalSizeGb',
  ACTIVE_ACCOUNTS = 'activeAccounts',
  DEBTORS = 'debtors',
  ACTIVITIES = 'activities',
  ACTIVE_USERS = 'activeUsers',
  BALANCE_SUM = 'balanceSum',
  OVERDUE_SUM = 'overdueSum',
  PRINCIPAL_SUM = 'principalSum',
}

export enum SacStatisticSortOrder {
  ASC = 'asc',
  DESC = 'desc',
}
