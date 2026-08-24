/**
 * One `sac_statistics` record as the API returns it.
 *
 * The three financial sums are **strings**, not numbers, and must stay that
 * way: the backend stores them as Decimal128 because a numeric(18,2) exceeds a
 * double's exact range, so parsing them into a JS number here would undo that
 * on the client. They are formatted for display with `formatDecimal`, which
 * groups digits without ever converting to a number.
 */
export interface SacStatistic {
  _id: string;
  deviceId: string;
  databaseName: string;
  generatedAt: string;
  totalSizeGb: number | null;
  debtors: number | null;
  debtorActiveAccounts: number | null;
  activeAccounts: number | null;
  avgActiveUsersLast3Months: number | null;
  activities: number | null;
  activitiesLast30Days: number | null;
  activeUsers: number | null;
  logSizeGb: number | null;
  dataSizeGb: number | null;
  balanceSum: string | null;
  overdueSum: string | null;
  principalSum: string | null;
}

export interface SacStatisticsPage {
  items: SacStatistic[];
  total: number;
  page: number;
  limit: number;
}

/** The server-side sort whitelist, mirrored so a typo is a compile error. */
export type SacStatisticSortField =
  | 'generatedAt'
  | 'databaseName'
  | 'totalSizeGb'
  | 'activeAccounts'
  | 'debtors'
  | 'activities'
  | 'activeUsers'
  | 'balanceSum'
  | 'overdueSum'
  | 'principalSum';

export type SortOrder = 'asc' | 'desc';

/** The formats this report supports — `pdf` is refused server-side for 15 columns. */
export type SacExportFormat = 'csv' | 'xlsx';

export interface SacStatisticsQuery {
  databaseName?: string;
  from?: string;
  to?: string;
  sort?: SacStatisticSortField;
  order?: SortOrder;
  page?: number;
  limit?: number;
}
