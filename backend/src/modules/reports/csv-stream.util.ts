import { Writable } from 'stream';
import { CsvCell, escapeCsvField } from './csv.util';

/**
 * Streaming counterpart of `toCsv` for an unbounded row source (BL-032 CA-6),
 * sharing the same field-escaping so the two paths can never disagree about
 * quoting.
 *
 * Backpressure is respected explicitly: `Writable.write()` returning false
 * means the consumer's buffer is full, and ignoring it is how a fast Mongo
 * cursor turns into unbounded memory growth in the very export that exists to
 * avoid exactly that.
 */
export async function writeCsvStream<T>(
  out: Writable,
  headers: string[],
  rows: AsyncIterable<T>,
  toCells: (row: T) => CsvCell[],
): Promise<void> {
  await write(out, `${headers.map(escapeCsvField).join(',')}\r\n`);
  for await (const row of rows) {
    await write(out, `${toCells(row).map(escapeCsvField).join(',')}\r\n`);
  }
  await new Promise<void>((resolve) => out.end(resolve));
}

function write(out: Writable, chunk: string): Promise<void> {
  if (out.write(chunk)) {
    return Promise.resolve();
  }
  return new Promise((resolve) => out.once('drain', resolve));
}
