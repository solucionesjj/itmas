import { SacStatisticDocument } from '../sac-statistics/sac-statistic.schema';
import { decimalToString } from '../sac-statistics/sac-statistic-response.mapper';
import { CsvCell } from './csv.util';
import { XlsxCell, XlsxColumn } from './xlsx.util';

/**
 * The fifteen contract fields of `sac_statistics`, in the order of BL-031's
 * mapping table. `deviceId` and `_id` are deliberately NOT here: they are
 * IT-MAS's own provenance bookkeeping, not part of what the source produced,
 * and the report is the source's data.
 *
 * Fifteen columns is also why `format=pdf` is refused for this report — they do
 * not fit the PDF generator's fixed layout (BL-032 CA-4).
 */
export const SAC_STATISTICS_COLUMNS: XlsxColumn[] = [
  { header: 'databaseName', type: 'text', width: 28 },
  { header: 'generatedAt', type: 'date', width: 20 },
  { header: 'totalSizeGb', type: 'integer' },
  { header: 'dataSizeGb', type: 'integer' },
  { header: 'logSizeGb', type: 'integer' },
  { header: 'debtors', type: 'integer' },
  { header: 'debtorActiveAccounts', type: 'integer', width: 22 },
  { header: 'activeAccounts', type: 'integer' },
  { header: 'balanceSum', type: 'decimal2', width: 20 },
  { header: 'overdueSum', type: 'decimal2', width: 20 },
  { header: 'principalSum', type: 'decimal2', width: 20 },
  { header: 'activities', type: 'integer' },
  { header: 'activitiesLast30Days', type: 'integer', width: 22 },
  { header: 'activeUsers', type: 'integer' },
  { header: 'avgActiveUsersLast3Months', type: 'integer', width: 26 },
];

export const SAC_STATISTICS_HEADERS = SAC_STATISTICS_COLUMNS.map(
  (column) => column.header,
);

/**
 * CSV keeps the financial sums as their EXACT decimal strings — the whole point
 * of storing them as Decimal128. Dates go out as ISO 8601, matching the other
 * CSV reports.
 */
export function toSacCsvRow(doc: SacStatisticDocument): CsvCell[] {
  return [
    doc.databaseName,
    doc.generatedAt?.toISOString() ?? '',
    doc.totalSizeGb,
    doc.dataSizeGb,
    doc.logSizeGb,
    doc.debtors,
    doc.debtorActiveAccounts,
    doc.activeAccounts,
    decimalToString(doc.balanceSum),
    decimalToString(doc.overdueSum),
    decimalToString(doc.principalSum),
    doc.activities,
    doc.activitiesLast30Days,
    doc.activeUsers,
    doc.avgActiveUsersLast3Months,
  ];
}

/**
 * Native cell types (BL-032 CA-5): a real Date for `generatedAt`, real numbers
 * for the counters and the sums, so the spreadsheet sorts and charts them
 * instead of treating them as text.
 *
 * The sums are converted to `Number` here, which is lossy past ~15 significant
 * digits — unavoidable, since a numeric cell in a spreadsheet IS a double. Use
 * the CSV export when exact cents matter at that magnitude.
 */
export function toSacXlsxRow(doc: SacStatisticDocument): XlsxCell[] {
  return [
    doc.databaseName,
    doc.generatedAt ?? null,
    doc.totalSizeGb,
    doc.dataSizeGb,
    doc.logSizeGb,
    doc.debtors,
    doc.debtorActiveAccounts,
    doc.activeAccounts,
    toNumber(decimalToString(doc.balanceSum)),
    toNumber(decimalToString(doc.overdueSum)),
    toNumber(decimalToString(doc.principalSum)),
    doc.activities,
    doc.activitiesLast30Days,
    doc.activeUsers,
    doc.avgActiveUsersLast3Months,
  ];
}

function toNumber(value: string | null): number | null {
  return value === null ? null : Number(value);
}
