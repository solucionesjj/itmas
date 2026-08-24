import { Writable } from 'stream';
import ExcelJS from 'exceljs';

/** How one column of a spreadsheet report is typed and formatted. */
export interface XlsxColumn {
  /** The contract field name, used verbatim as the header cell (BL-032 CA-5). */
  header: string;
  /**
   * `date` and the two numeric kinds produce NATIVE cell types, so Excel sorts,
   * filters and charts them as dates and numbers rather than as text.
   * `decimal2` additionally carries a two-decimal format, matching the
   * numeric(18,2) source of the financial sums.
   */
  type: 'text' | 'date' | 'integer' | 'decimal2';
  width?: number;
}

export type XlsxCell = string | number | Date | null;

const NUMBER_FORMATS: Record<XlsxColumn['type'], string | undefined> = {
  text: undefined,
  date: 'yyyy-mm-dd hh:mm',
  integer: '0',
  decimal2: '#,##0.00',
};

/**
 * Streams a worksheet to `out`, pulling rows from an async iterable so the
 * caller can hand over a Mongo cursor instead of an array (BL-032 CA-6).
 *
 * exceljs's `stream.xlsx.WorkbookWriter` writes and compresses each row as it
 * arrives, keeping only the current row in memory — the reason it is used here
 * rather than the ordinary in-memory `Workbook`. `commit()` on the workbook
 * ends `out`, so the caller must not end it again.
 *
 * A note on precision: the three financial sums are Decimal128 in Mongo and
 * exact decimal strings on the JSON API, but a spreadsheet cell that must be a
 * *number* (CA-5) is an IEEE double — Excel has no wider numeric type. Values
 * beyond ~15 significant digits therefore round in the .xlsx, and only there;
 * the CSV export keeps the exact string. That is a property of the file format,
 * not of this code.
 */
export async function writeXlsx<T>(
  out: Writable,
  sheetName: string,
  columns: XlsxColumn[],
  rows: AsyncIterable<T>,
  toCells: (row: T) => XlsxCell[],
): Promise<void> {
  const workbook = new ExcelJS.stream.xlsx.WorkbookWriter({
    stream: out,
    useStyles: true,
  });
  const sheet = workbook.addWorksheet(sheetName);

  sheet.columns = columns.map((column) => ({
    header: column.header,
    key: column.header,
    width: column.width ?? Math.max(12, column.header.length + 2),
    style: { numFmt: NUMBER_FORMATS[column.type] },
  }));
  sheet.getRow(1).font = { bold: true };
  sheet.getRow(1).commit();

  for await (const row of rows) {
    sheet.addRow(toCells(row)).commit();
  }

  sheet.commit();
  await workbook.commit();
}

/**
 * Buffered variant for the bounded reports (`devices`, `alerts`), which already
 * have their rows in hand. Shares the column spec with the streaming path above
 * so the two can never disagree about headers or cell types.
 */
export async function buildXlsxBuffer(
  sheetName: string,
  columns: XlsxColumn[],
  rows: XlsxCell[][],
): Promise<Buffer> {
  const workbook = new ExcelJS.Workbook();
  const sheet = workbook.addWorksheet(sheetName);

  sheet.columns = columns.map((column) => ({
    header: column.header,
    key: column.header,
    width: column.width ?? Math.max(12, column.header.length + 2),
    style: { numFmt: NUMBER_FORMATS[column.type] },
  }));
  sheet.getRow(1).font = { bold: true };

  for (const row of rows) {
    sheet.addRow(row);
  }

  const buffer = await workbook.xlsx.writeBuffer();
  return Buffer.from(buffer);
}
