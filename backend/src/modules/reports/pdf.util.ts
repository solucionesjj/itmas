import PDFDocument from 'pdfkit';
import type { CsvCell } from './csv.util';

/**
 * Renders a simple tabular PDF: a title, a generation timestamp, and one
 * plain-text line per row (column values joined with a fixed separator).
 * This is an MVP export, not a designed document — no fonts/graphics beyond
 * pdfkit's built-in defaults.
 */
export function buildPdfReport(
  title: string,
  headers: string[],
  rows: CsvCell[][],
): Promise<Buffer> {
  return new Promise((resolve, reject) => {
    const doc = new PDFDocument({ margin: 40, size: 'A4' });
    const chunks: Buffer[] = [];
    doc.on('data', (chunk: Buffer) => chunks.push(chunk));
    doc.on('end', () => resolve(Buffer.concat(chunks)));
    doc.on('error', reject);

    doc.fontSize(18).text(title);
    doc
      .fontSize(9)
      .fillColor('gray')
      .text(`Generado: ${new Date().toISOString()}`);
    doc.moveDown();
    doc.fillColor('black');

    doc.fontSize(10).text(headers.join('  |  '), { underline: true });
    doc.moveDown(0.5);

    doc.fontSize(9);
    for (const row of rows) {
      const line = row
        .map((cell) =>
          cell === null || cell === undefined ? '' : String(cell),
        )
        .join('  |  ');
      doc.text(line);
    }

    if (rows.length === 0) {
      doc.text('(sin datos)');
    }

    doc.end();
  });
}
