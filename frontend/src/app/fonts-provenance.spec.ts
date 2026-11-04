import { createHash } from 'node:crypto';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

/**
 * SPEC-0010 CA-18 and RF-15: the self-hosted fonts are third-party files. Each folder of `public/fonts/` carries its licence and a
 * `SOURCE.md` with the SHA-256 of every served file, and the served files are exactly the ones recorded there. The theme layer only
 * points at files that exist, always same-origin and never as `data:`.
 */
describe('Self-hosted fonts provenance (SPEC-0010 CA-18)', () => {
  const root = process.cwd();
  const fonts = join(root, 'public', 'fonts');
  const folders = readdirSync(fonts, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name);
  const NOT_SERVED = new Set(['SOURCE.md', 'OFL.txt', 'LICENSE']);

  /** `| file | sha256 |` rows of the "Archivos servidos" section of SOURCE.md. */
  function recordedHashes(source: string): Map<string, string> {
    const section = source.split(/^## /m).find((part) => part.startsWith('Archivos servidos'));
    const rows = [...(section ?? '').matchAll(/^\|\s*`([^`]+)`\s*\|\s*`([0-9a-f]{64})`\s*\|/gm)];
    return new Map(rows.map((row) => [row[1], row[2]]));
  }

  it('has the Poppins and Material Symbols folders and no loose file outside them', () => {
    expect([...folders].sort()).toEqual(['material-symbols', 'poppins']);
    expect(
      readdirSync(fonts, { withFileTypes: true }).filter((entry) => !entry.isDirectory()),
    ).toEqual([]);
  });

  it('keeps a licence and a SOURCE.md in every folder', () => {
    for (const folder of folders) {
      const files = readdirSync(join(fonts, folder));
      expect(files, folder).toContain('SOURCE.md');
      expect(
        files.some((file) => file === 'OFL.txt' || file === 'LICENSE'),
        folder,
      ).toBe(true);
    }
  });

  it('serves exactly the recorded files, with the recorded SHA-256', () => {
    for (const folder of folders) {
      const recorded = recordedHashes(readFileSync(join(fonts, folder, 'SOURCE.md'), 'utf8'));
      const served = readdirSync(join(fonts, folder)).filter((file) => !NOT_SERVED.has(file));
      expect(served.length, folder).toBeGreaterThan(0);
      expect([...served].sort(), folder).toEqual([...recorded.keys()].sort());
      for (const file of served) {
        const hash = createHash('sha256')
          .update(readFileSync(join(fonts, folder, file)))
          .digest('hex');
        expect(hash, `${folder}/${file}`).toBe(recorded.get(file));
      }
    }
  });

  it('checks that the Poppins licence declares no Reserved Font Name', () => {
    const licence = readFileSync(join(fonts, 'poppins', 'OFL.txt'), 'utf8');
    const header = licence.split('This Font Software is licensed')[0];
    expect(header).not.toMatch(/Reserved Font Name/i);
  });

  it('points @font-face only at existing same-origin files, never at data:', () => {
    const theme = readFileSync(join(root, 'src', 'styles', '_fonts.scss'), 'utf8');
    // The Poppins faces are written once inside `@each $weight in (…)`: expand them for every weight.
    const weights = /@each \$weight in \(([\d,\s]+)\)/.exec(theme)?.[1].split(',') ?? [];
    expect(weights.length).toBe(5);
    const urls = [...theme.matchAll(/url\(\s*['"]?([^'")]+)/g)]
      .map((match) => match[1])
      .flatMap((url) =>
        url.includes('#{$weight}')
          ? weights.map((weight) => url.replace('#{$weight}', weight.trim()))
          : [url],
      );
    expect(urls.length).toBeGreaterThan(5);
    for (const url of urls) {
      expect(url, url).toMatch(/^\/fonts\/[a-z-]+\/[A-Za-z0-9._-]+\.woff2$/);
      expect(existsSync(join(root, 'public', url)), url).toBe(true);
    }
    // Every face declares its display: `swap` for the text (Poppins), `block` for the icons, whose fallback would show their names.
    const faces = [...theme.matchAll(/@font-face\s*\{([^}]*)\}/g)].map((match) => match[1]);
    expect(faces.length).toBeGreaterThan(1);
    for (const face of faces) {
      const family = /font-family:\s*'([^']+)'/.exec(face)?.[1];
      const display = /font-display:\s*([a-z]+);/.exec(face)?.[1];
      expect(display, family).toBe(family === 'Material Symbols Outlined' ? 'block' : 'swap');
    }
  });
});

/**
 * SPEC-0010 RNF-4, CA-2 and CA-11: no third party serves fonts or styles. The CSP of nginx keeps `font-src 'self'` and no external
 * origin in `style-src`, and `index.html` loads nothing from another origin (no style sheets, no preconnect).
 */
describe('No third party serves fonts or styles (SPEC-0010 RNF-4)', () => {
  const root = process.cwd();
  const csp = /Content-Security-Policy "([^"]+)"/.exec(
    readFileSync(join(root, 'nginx', 'security-headers.inc.template'), 'utf8'),
  )?.[1];
  const directive = (name: string) =>
    csp
      ?.split(';')
      .map((part) => part.trim())
      .find((part) => part.startsWith(`${name} `));

  it('keeps font-src on the same origin and no external origin in style-src', () => {
    expect(directive('font-src')).toBe("font-src 'self'");
    expect(directive('style-src')).toBe("style-src 'self' 'unsafe-inline'");
  });

  it('loads nothing from another origin in index.html', () => {
    const index = readFileSync(join(root, 'src', 'index.html'), 'utf8');
    expect(index).not.toMatch(/(href|src)\s*=\s*["']?\s*(https?:)?\/\//i);
  });
});
