import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

/**
 * SPEC-0006 CA-22, §14.1 and ADR-0017: what a person typed (titles, descriptions, tags, notes, links) is untrusted, so the feature never
 * renders HTML from data nor bypasses Angular's sanitization. FullCalendar can draw HTML (`eventContent: { html }`) and navigate by itself:
 * the calendar only uses Angular templates, and links are built from the id alone.
 */
describe('Agenda rendering safety (CA-22)', () => {
  const folder = join(process.cwd(), 'src', 'app', 'features', 'agenda');
  const sources = (readdirSync(folder, { recursive: true }) as string[])
    .filter((file) => /\.(ts|html)$/.test(file) && !file.endsWith('.spec.ts'))
    .map((file) => ({ file, source: readFileSync(join(folder, file), 'utf8') }));

  it('has sources to check', () => {
    expect(sources.length).toBeGreaterThan(30);
  });

  it('never uses innerHTML, outerHTML or bypassSecurityTrust*', () => {
    const unsafe =
      /innerHTML|outerHTML|insertAdjacentHTML|bypassSecurityTrust|DomSanitizer|dangerouslySetInnerHTML/;
    expect(sources.filter(({ source }) => unsafe.test(source)).map(({ file }) => file)).toEqual([]);
  });

  it('never gives FullCalendar HTML to draw (the `html` form of its content hooks)', () => {
    const htmlContent = /\bhtml\s*:/;
    expect(
      sources.filter(({ source }) => htmlContent.test(source)).map(({ file }) => file),
    ).toEqual([]);
  });

  it('never navigates by itself to an address that came from data', () => {
    const navigates = /window\.open|\blocation\.(href|assign|replace)\b/;
    expect(sources.filter(({ source }) => navigates.test(source)).map(({ file }) => file)).toEqual(
      [],
    );
  });
});
