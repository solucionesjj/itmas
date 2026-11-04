import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

/**
 * SPEC-0010 CA-17 and §14.2: the logo, the backgrounds and the public pages are templates and CSS. They never render HTML from data,
 * never inject markup and never bypass Angular's sanitization.
 */
describe('Layout and public pages rendering safety (SPEC-0010 CA-17)', () => {
  const app = join(process.cwd(), 'src', 'app');
  // CA-17 lists the first four; the logo, shared by the sign-in page and the shell, is checked with them.
  const folders = [
    'layout',
    'features/login',
    'features/errors',
    'shared/components/status-message',
    'shared/components/logo',
  ];
  const sources = folders.flatMap((folder) =>
    (readdirSync(join(app, folder), { recursive: true }) as string[])
      .filter((file) => /\.(ts|html)$/.test(file) && !file.endsWith('.spec.ts'))
      .map((file) => ({
        file: `${folder}/${file}`,
        source: readFileSync(join(app, folder, file), 'utf8'),
      })),
  );

  it('has sources in every folder', () => {
    for (const folder of folders) {
      expect(
        sources.some(({ file }) => file.startsWith(`${folder}/`)),
        folder,
      ).toBe(true);
    }
  });

  it('never uses innerHTML, outerHTML, insertAdjacentHTML, bypassSecurityTrust* or DomSanitizer', () => {
    // Case-insensitive: Angular maps the `[innerHtml]` binding to `innerHTML`.
    const unsafe = /innerhtml|outerhtml|insertadjacenthtml|bypasssecuritytrust|domsanitizer/i;
    expect(sources.filter(({ source }) => unsafe.test(source)).map(({ file }) => file)).toEqual([]);
  });
});
