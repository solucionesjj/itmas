import { Writable } from 'stream';

/**
 * A report is either fully materialised (`buffer`) or written straight to the
 * response as it is produced (`stream`).
 *
 * Both shapes exist because the two cases genuinely differ. `devices` and
 * `alerts` are bounded by the size of the estate — hundreds of rows — so
 * buffering them is simpler and lets AllExceptionsFilter still turn a late
 * failure into a proper JSON error. `sac-statistics` grows by one row per
 * database per day and has no default TTL, so its unfiltered history has no
 * ceiling and must be streamed (BL-032 CA-6).
 *
 * A `stream` report OWNS the output: `write` must end it (exceljs's workbook
 * writer does so on commit). The trade-off is that once the first byte is out,
 * the status line is already sent — a mid-stream failure truncates the download
 * instead of becoming a 500, which is inherent to streaming, not an oversight.
 */
export type ReportFile =
  | {
      kind: 'buffer';
      buffer: Buffer;
      contentType: string;
      filename: string;
    }
  | {
      kind: 'stream';
      contentType: string;
      filename: string;
      write: (out: Writable) => Promise<void>;
    };
