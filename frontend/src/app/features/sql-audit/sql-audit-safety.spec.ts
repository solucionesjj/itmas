import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

/**
 * CA-17, SPEC-0005 §14.1: engine-provided names are untrusted, so the feature never renders HTML from data nor
 * bypasses Angular's sanitization. Angular interpolation escapes everything else.
 */
describe('SQL audit rendering safety', () => {
  const folder = join(process.cwd(), 'src', 'app', 'features', 'sql-audit');
  const sources = (readdirSync(folder, { recursive: true }) as string[])
    .filter((file) => /\.(ts|html)$/.test(file) && !file.endsWith('.spec.ts'))
    .map((file) => ({ file, source: readFileSync(join(folder, file), 'utf8') }));

  it('has sources to check', () => {
    expect(sources.length).toBeGreaterThan(10);
  });

  it('never uses innerHTML, outerHTML or bypassSecurityTrust*', () => {
    const unsafe = /innerHTML|outerHTML|insertAdjacentHTML|bypassSecurityTrust|DomSanitizer/;
    expect(sources.filter(({ source }) => unsafe.test(source)).map(({ file }) => file)).toEqual([]);
  });
});
