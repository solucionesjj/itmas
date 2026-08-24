import { Types } from 'mongoose';
import { SacStatisticDocument } from './sac-statistic.schema';

/**
 * The wire shape of one `sac_statistics` record. The three financial sums are
 * **strings**, not numbers (BL-031 CA-4): they are stored as Decimal128 because
 * a numeric(18,2) exceeds a double's exact range, and emitting them as JSON
 * numbers would hand the cents back to the same rounding the storage choice
 * exists to avoid. A consumer that needs arithmetic on them should use a decimal
 * library, not `parseFloat`.
 */
export interface SacStatisticResponse {
  _id: string;
  deviceId: string;
  databaseName: string;
  generatedAt: Date;
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

/**
 * Mongoose hands back a Decimal128 instance, which `JSON.stringify` renders as
 * `{"$numberDecimal":"1.00"}` — a BSON internal, not a contract. Every path that
 * returns one of these records must go through this function so the API always
 * emits the plain decimal string.
 */
export function decimalToString(
  value: Types.Decimal128 | string | null | undefined,
): string | null {
  if (value === null || value === undefined) {
    return null;
  }
  return typeof value === 'string' ? value : value.toString();
}

export function toSacStatisticResponse(
  doc: SacStatisticDocument,
): SacStatisticResponse {
  return {
    _id: String(doc._id),
    deviceId: doc.deviceId,
    databaseName: doc.databaseName,
    generatedAt: doc.generatedAt,
    totalSizeGb: doc.totalSizeGb ?? null,
    debtors: doc.debtors ?? null,
    debtorActiveAccounts: doc.debtorActiveAccounts ?? null,
    activeAccounts: doc.activeAccounts ?? null,
    avgActiveUsersLast3Months: doc.avgActiveUsersLast3Months ?? null,
    activities: doc.activities ?? null,
    activitiesLast30Days: doc.activitiesLast30Days ?? null,
    activeUsers: doc.activeUsers ?? null,
    logSizeGb: doc.logSizeGb ?? null,
    dataSizeGb: doc.dataSizeGb ?? null,
    balanceSum: decimalToString(doc.balanceSum),
    overdueSum: decimalToString(doc.overdueSum),
    principalSum: decimalToString(doc.principalSum),
  };
}
