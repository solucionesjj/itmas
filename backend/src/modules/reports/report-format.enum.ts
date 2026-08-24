export enum ReportFormat {
  CSV = 'csv',
  PDF = 'pdf',
  /**
   * Added for the SAC statistics report (BL-032), but implemented as a generic
   * serialiser alongside CSV and PDF — so it is available to every reportType
   * without any of them changing how they fetch data.
   */
  XLSX = 'xlsx',
}

export const REPORT_CONTENT_TYPES: Record<ReportFormat, string> = {
  [ReportFormat.CSV]: 'text/csv; charset=utf-8',
  [ReportFormat.PDF]: 'application/pdf',
  [ReportFormat.XLSX]:
    'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
};
