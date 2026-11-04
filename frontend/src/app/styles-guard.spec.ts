import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { join, relative } from 'node:path';

/**
 * SPEC-0010 CA-8, constitution §6.3 and ADR-0021: the visual identity lives only in the theme layer (`src/styles.scss` and
 * `src/styles/**`). Outside it, component styles (style sheets and inline `styles`) are layout only: no selectors of Material or
 * CDK classes, no `::ng-deep` or `ViewEncapsulation.None` beyond the two exceptions of ADR-0021, no `--mat-*`/`--itmas-*`
 * declarations or `mat.*` mixins, and colour, background, border colour, shadow, filter, radius and typography only take
 * `var(--mat-sys-*)`, `var(--itmas-*)`, `inherit`, `currentColor`, `transparent`, `none` or `0`.
 *
 * The checks are pure functions over `{ file, css }` sources, so the last block also feeds them known violations: a guard that
 * stops seeing them fails here instead of passing in silence.
 */

interface StyleSource {
  file: string;
  css: string;
}

interface Declaration {
  file: string;
  property: string;
  value: string;
}

const root = process.cwd();
const appFolder = join(root, 'src', 'app');
const themeFolder = join(root, 'src', 'styles');

/** The two encapsulation exceptions of ADR-0021. */
const NG_DEEP_FILE = 'src/app/features/manual/manual-page.scss';
const UNENCAPSULATED_FILE = 'src/app/features/agenda/calendar/agenda-calendar.ts';

/** Public host selectors of Material components that the theme layer may select, for properties no token offers (ADR-0021 note). */
const THEME_ELEMENT_ALLOWLIST = new Set(['mat-card']);

function filesUnder(folder: string, pattern: RegExp): string[] {
  if (!existsSync(folder)) return [];
  return (readdirSync(folder, { recursive: true }) as string[])
    .filter((file) => pattern.test(file))
    .map((file) => join(folder, file));
}

function name(path: string): string {
  return relative(root, path).split('\\').join('/');
}

/**
 * Removes `/* … *\/` and SCSS `// …` comments, but never inside a string or a `url(…)`: a `//` there is part of an address and a
 * `/*` inside a line comment does not open a block comment.
 */
function stripComments(css: string): string {
  let out = '';
  let quote: string | null = null;
  let inUrl = false;
  for (let index = 0; index < css.length; index++) {
    const char = css[index];
    const next = css[index + 1];
    if (quote) {
      out += char;
      if (char === '\\') out += css[++index] ?? '';
      else if (char === quote) quote = null;
      continue;
    }
    if (inUrl) {
      out += char;
      if (char === ')') inUrl = false;
      else if (char === '"' || char === "'") quote = char;
      continue;
    }
    if (char === '"' || char === "'") {
      quote = char;
      out += char;
      continue;
    }
    if (char === '(' && out.slice(-3).toLowerCase() === 'url') {
      inUrl = true;
      out += char;
      continue;
    }
    if (char === '/' && next === '*') {
      const end = css.indexOf('*/', index + 2);
      index = end < 0 ? css.length : end + 1;
      continue;
    }
    if (char === '/' && next === '/') {
      const end = css.indexOf('\n', index);
      index = end < 0 ? css.length : end - 1;
      continue;
    }
    out += char;
  }
  return out;
}

/** Splits a style sheet into selectors (text before `{`) and declarations (text before `;` or `}`). */
function parse(source: StyleSource): { selectors: string[]; declarations: Declaration[] } {
  const css = stripComments(source.css);
  const selectors: string[] = [];
  const declarations: Declaration[] = [];
  let buffer = '';
  let depth = 0;
  for (const char of css) {
    if (char === '(') depth++;
    if (char === ')') depth--;
    if (depth > 0 || (char !== '{' && char !== ';' && char !== '}')) {
      buffer += char;
      continue;
    }
    const text = buffer.trim();
    buffer = '';
    if (!text) continue;
    if (char === '{') {
      selectors.push(text);
      continue;
    }
    const colon = text.indexOf(':');
    if (colon > 0 && !text.startsWith('@')) {
      declarations.push({
        file: source.file,
        property: text.slice(0, colon).trim().toLowerCase(),
        value: text.slice(colon + 1).trim(),
      });
    }
  }
  return { selectors, declarations };
}

const ALLOWED_TOKEN =
  /^(var\(--(mat-sys|itmas)-[a-z0-9-]+\)|inherit|currentcolor|transparent|none|0)$/i;

function splitTopLevel(text: string, separator: string): string[] {
  const parts: string[] = [];
  let depth = 0;
  let current = '';
  for (const char of text) {
    if (char === '(') depth++;
    if (char === ')') depth--;
    if (char === separator && depth === 0) {
      parts.push(current);
      current = '';
    } else {
      current += char;
    }
  }
  parts.push(current);
  return parts;
}

/** `color-mix()` is allowed only when every colour it mixes is itself an allowed value (never a literal). */
function replaceColorMix(value: string): string | null {
  let result = value;
  for (;;) {
    const start = result.toLowerCase().indexOf('color-mix(');
    if (start < 0) return result;
    let depth = 0;
    let end = -1;
    for (let index = start + 'color-mix'.length; index < result.length; index++) {
      if (result[index] === '(') depth++;
      if (result[index] === ')' && --depth === 0) {
        end = index;
        break;
      }
    }
    if (end < 0) return null;
    const args = splitTopLevel(result.slice(start + 'color-mix('.length, end), ',');
    const [space, ...colors] = args.map((arg) => arg.trim());
    if (!/^in [a-z-]+$/i.test(space) || colors.length === 0) return null;
    const valid = colors.every((color) => {
      const parts = color.split(/\s+/).filter((part) => !/^\d+(\.\d+)?%$/.test(part));
      return parts.length === 1 && ALLOWED_TOKEN.test(parts[0]);
    });
    if (!valid) return null;
    result = `${result.slice(0, start)}var(--itmas-mixed)${result.slice(end + 1)}`;
  }
}

/** Properties whose value carries colour, background, shadow, filter, radius or typography (constitution §6.3). */
const RESTRICTED = new RegExp(
  '^(' +
    [
      'color',
      'background(-color|-image)?',
      'border(-(top|right|bottom|left|block|inline)(-(start|end))?)?-color',
      'border-image(-source)?',
      'outline-color',
      'box-shadow',
      'text-shadow',
      '(-webkit-)?backdrop-filter',
      'filter',
      'border(-(top|bottom|start|end)-(left|right|start|end))?-radius',
      'font',
      'font-(family|size|weight|style|stretch|variant|variation-settings|feature-settings)',
      'line-height',
      'letter-spacing',
      'fill',
      'stroke',
      'stop-color',
      'flood-color',
      'lighting-color',
      'caret-color',
      'accent-color',
      'text-decoration-color',
      'text-emphasis(-color)?',
      'column-rule-color',
      'scrollbar-color',
      '-webkit-text-fill-color',
      '-webkit-text-stroke(-color)?',
      '-webkit-tap-highlight-color',
      '(-webkit-)?mask(-image)?',
      'list-style-image',
    ].join('|') +
    ')$',
);

/** Shorthands that mix a colour with widths, styles or lines: only their colour part is restricted. */
const COLOR_SHORTHAND =
  /^(border(-(top|right|bottom|left|block|inline)(-(start|end))?)?|outline|column-rule|text-decoration)$/;

const NON_COLOR_PART =
  /^(\d+(\.\d+)?(px|rem|em)?|thin|medium|thick|solid|dashed|dotted|double|groove|ridge|inset|outset|hidden|wavy|underline|overline|line-through|blink|auto|from-font)$/i;

function allowedValue(property: string, value: string): boolean {
  const mixed = replaceColorMix(value);
  if (mixed === null) return false;
  const parts = splitTopLevel(mixed.trim(), ' ')
    .flatMap((part) => splitTopLevel(part, ','))
    .map((part) => part.trim())
    .filter(Boolean)
    .filter((part) => !(COLOR_SHORTHAND.test(property) && NON_COLOR_PART.test(part)));
  return parts.every((part) => ALLOWED_TOKEN.test(part));
}

/** Every string or `url(…)` argument that points at another origin (a scheme or `//`), ignoring surrounding spaces. */
function externalReferences(css: string): string[] {
  const text = stripComments(css);
  // Strings are read left to right, so the single quotes inside a double-quoted data URI stay inside it.
  const strings = [...text.matchAll(/"([^"]*)"|'([^']*)'/g)].map((match) => match[1] ?? match[2]);
  const bareUrls = [...text.matchAll(/url\(\s*([^'"\s)][^)]*)\)/gi)].map((match) => match[1]);
  return (
    [...strings, ...bareUrls]
      .map((reference) => reference.trim())
      .filter((reference) => /^([a-z][a-z0-9+.-]*:|\/\/)/i.test(reference))
      // Sass built-in modules (`@use 'sass:map'`) are not addresses.
      .filter((reference) => !reference.startsWith('sass:'))
  );
}

const MATERIAL_CLASS = /\.(mat|mdc|cdk)-|\[class[*^$|~]?=\s*['"]?[^\]]*\b(mat|mdc|cdk)-/;
const ORANGE = /#f2982a|#fa9241|rgba?\(\s*242[\s,]+152[\s,]+42\b|rgba?\(\s*250[\s,]+146[\s,]+65\b/i;

/** Violations of §6.3 in component styles (style sheets and inline `styles`). */
function componentViolations(sources: readonly StyleSource[]): string[] {
  const violations: string[] = [];
  for (const source of sources) {
    const at = (message: string) => violations.push(`${source.file}: ${message}`);
    const { selectors, declarations } = parse(source);
    const css = stripComments(source.css);
    for (const selector of selectors) {
      if (MATERIAL_CLASS.test(selector)) at(`Material/CDK class selector «${selector}»`);
      // A SCSS nested property (`font: { size: … }`) would hide the property from the declaration check.
      if (/^[a-z-]+\s*:$/i.test(selector)) at(`nested property «${selector}»`);
    }
    if (/::ng-deep|>>>|\/deep\//.test(css)) {
      const allowed =
        source.file === NG_DEEP_FILE &&
        selectors
          .filter((selector) => /::ng-deep|>>>|\/deep\//.test(selector))
          .every((selector) => selector === ':host ::ng-deep .content');
      if (!allowed) at('::ng-deep (or >>> / /deep/) outside the exception of ADR-0021');
    }
    if (/#\{/.test(css)) at('SCSS interpolation');
    if (/@extend\b/.test(css)) at('@extend');
    if (/@include\s+mat\./.test(css)) at('mat.* mixin');
    for (const match of css.matchAll(/@(use|forward|import)\s+['"]?([^'";\s]+)/g)) {
      if (!match[2].startsWith('sass:')) at(`@${match[1]} ${match[2]}`);
    }
    if (externalReferences(source.css).length) at('reference to another origin');
    if (ORANGE.test(css)) at('the orange of the identity');
    for (const declaration of declarations) {
      const { property, value } = declaration;
      if (/^--(mat|itmas)-/.test(property)) {
        at(`declares ${property}`);
      } else if (property.startsWith('--')) {
        // Custom properties of third parties (FullCalendar's --fc-*) take theme roles only.
        if (!allowedValue('color', value)) at(`${property}: ${value}`);
      } else if (RESTRICTED.test(property) || COLOR_SHORTHAND.test(property)) {
        if (!allowedValue(property, value)) at(`${property}: ${value}`);
      }
    }
  }
  return violations;
}

/** Violations in TypeScript and HTML: encapsulation, the inline `styles` form and styles set from templates. */
function templateViolations(files: readonly { file: string; source: string }[]): string[] {
  const violations: string[] = [];
  for (const { file, source } of files) {
    const at = (message: string) => violations.push(`${file}: ${message}`);
    if (/encapsulation\s*:/.test(source) && file !== UNENCAPSULATED_FILE) at('sets encapsulation');
    if (/styles\s*:\s*\[/.test(source)) at('inline styles as an array: use one template literal');
    if (/<style[\s>]/i.test(source)) at('<style> element in a template');
    if (
      /\sstyle\s*=\s*['"]|\[style[.\]]|\[attr\.style\]|\[ngStyle\]|['"]\[?style(\.[a-z-]+)?\]?['"]\s*:/i.test(
        source,
      )
    ) {
      at('style set from a template or a host binding');
    }
  }
  return violations;
}

/** Violations in the theme layer itself. */
function themeViolations(sources: readonly StyleSource[]): string[] {
  const violations: string[] = [];
  for (const source of sources) {
    const at = (message: string) => violations.push(`${source.file}: ${message}`);
    const css = stripComments(source.css);
    for (const selector of parse(source).selectors) {
      if (MATERIAL_CLASS.test(selector)) at(`Material/CDK class selector «${selector}»`);
      for (const match of selector.matchAll(/(?:^|[\s>+~,(])(mat-[a-z-]+)/g)) {
        if (!THEME_ELEMENT_ALLOWLIST.has(match[1])) at(`selects the Material element ${match[1]}`);
      }
    }
    if (/!important/.test(css)) at('!important');
    if (/@import\b/.test(css)) at('@import');
    for (const reference of externalReferences(source.css)) {
      if (!reference.startsWith('data:image/svg+xml')) at(`reference to ${reference.slice(0, 40)}`);
    }
  }
  return violations;
}

function componentSources(): StyleSource[] {
  const sheets = filesUnder(appFolder, /\.s?css$/).map((path) => ({
    file: name(path),
    css: readFileSync(path, 'utf8'),
  }));
  const inline = filesUnder(appFolder, /\.ts$/)
    .filter((path) => !path.endsWith('.spec.ts'))
    .flatMap((path) =>
      [...readFileSync(path, 'utf8').matchAll(/styles:\s*`([\s\S]*?)`/g)].map((match) => ({
        file: name(path),
        css: match[1],
      })),
    );
  return [...sheets, ...inline];
}

function templateSources(): { file: string; source: string }[] {
  return [
    ...filesUnder(appFolder, /\.html$/),
    ...filesUnder(appFolder, /\.ts$/).filter((path) => !path.endsWith('.spec.ts')),
  ].map((path) => ({ file: name(path), source: readFileSync(path, 'utf8') }));
}

function themeSources(): StyleSource[] {
  return [join(root, 'src', 'styles.scss'), ...filesUnder(themeFolder, /\.s?css$/)].map((path) => ({
    file: name(path),
    css: readFileSync(path, 'utf8'),
  }));
}

describe('Component styles are layout only (SPEC-0010 CA-8, constitution §6.3)', () => {
  const sources = componentSources();

  it('finds the style sheets and the inline styles', () => {
    expect(sources.filter((source) => source.file.endsWith('.scss')).length).toBeGreaterThan(10);
    expect(sources.filter((source) => source.file.endsWith('.ts')).length).toBeGreaterThan(20);
    expect(sources.flatMap((source) => parse(source).declarations).length).toBeGreaterThan(300);
  });

  it('follows §6.3 in every style sheet and inline style', () => {
    expect(componentViolations(sources)).toEqual([]);
  });

  it('keeps the two encapsulation exceptions of ADR-0021 and sets no styles from templates', () => {
    expect(templateViolations(templateSources())).toEqual([]);
    expect(
      sources.some((source) => source.file === NG_DEEP_FILE && source.css.includes('::ng-deep')),
    ).toBe(true);
    expect(readFileSync(join(root, UNENCAPSULATED_FILE), 'utf8')).toContain(
      'ViewEncapsulation.None',
    );
  });
});

describe('The theme layer (SPEC-0010 CA-8, RF-2)', () => {
  const sources = themeSources();

  it('exists as styles.scss and its partials', () => {
    expect(sources.map((source) => source.file)).toContain('src/styles.scss');
    expect(sources.length).toBeGreaterThan(5);
  });

  it('selects no internal classes, uses no !important and loads nothing from another origin', () => {
    expect(themeViolations(sources)).toEqual([]);
  });

  it('keeps the orange detail inside the theme layer', () => {
    const outside = (readdirSync(join(root, 'src'), { recursive: true }) as string[])
      .filter((file) => /\.(ts|html|s?css)$/.test(file) && !file.endsWith('.spec.ts'))
      .map((file) => join(root, 'src', file))
      .filter((path) => name(path) !== 'src/styles.scss' && !name(path).startsWith('src/styles/'))
      .filter((path) => ORANGE.test(readFileSync(path, 'utf8')))
      .map(name);
    expect(outside).toEqual([]);
  });
});

// The guards themselves: each known violation must be reported (SPEC-0010 testing review, T-4 to T-9).
describe('The style guards catch known violations', () => {
  const component = (css: string) => componentViolations([{ file: 'x.scss', css }]);

  it.each([
    ['a literal colour', '.a { color: #f00; }'],
    ['a literal in a colour mix', '.a { color: color-mix(in srgb, red 10%, transparent); }'],
    ['a Material class', '.mat-mdc-button { margin: 0; }'],
    ['a Material class by attribute', "[class*='mat-'] { margin: 0; }"],
    ['::ng-deep', ':host ::ng-deep .a { margin: 0; }'],
    ['the >>> alias of ::ng-deep', ':host >>> .a { margin: 0; }'],
    ['the /deep/ alias of ::ng-deep', ':host /deep/ .a { margin: 0; }'],
    ['an interpolated literal', '.a { color: #{"#f00"}; }'],
    ['a nested property', '.a { font: { size: 12px; } }'],
    ['@extend', '.a { @extend .b; }'],
    ['a theme partial', "@use '../../styles/surfaces' as s;"],
    ['a mat.* mixin', '.a { @include mat.button-overrides((x: 1)); }'],
    ['--mat-* declared', '.a { --mat-sys-primary: red; }'],
    ['a third-party variable with a literal', '.a { --fc-classic-today: #ff0; }'],
    ['text-decoration with a colour', '.a { text-decoration: underline red; }'],
    ['border-image', '.a { border-image: linear-gradient(red, blue) 1; }'],
    ['a literal radius', '.a { border-radius: 4px; }'],
    ['a literal weight', '.a { font-weight: 500; }'],
    ['another origin with spaces', '.a { background-image: url( //evil.example/a.png); }'],
    ['@import of a string', "@import 'https://evil.example/a.css';"],
    ['image-set', ".a { background-image: image-set('https://evil.example/a.png' 1x); }"],
    ['the orange', '.a { outline: 1px solid rgba(242, 152, 42, 0.5); }'],
  ])('reports %s', (_, css) => {
    expect(component(css).length).toBeGreaterThan(0);
  });

  it('does not lose code after a /* inside a line comment', () => {
    expect(component('// see src/*.scss\n.a { color: red; }\n/* end */')).not.toEqual([]);
  });

  it('accepts layout and theme roles', () => {
    expect(
      component(
        '.a { display: flex; color: var(--mat-sys-primary); border: 1px solid var(--mat-sys-outline-variant);' +
          ' text-decoration: line-through; --fc-x: color-mix(in srgb, var(--mat-sys-primary) 8%, transparent); }',
      ),
    ).toEqual([]);
  });

  it.each([
    ['encapsulation', '@Component({ encapsulation: ViewEncapsulation.None })'],
    ['encapsulation by number', '@Component({ encapsulation: 2 })'],
    ['styles as an array', '@Component({ styles: [`a[x] { color: red; }`] })'],
    ['a <style> element', '<style>.a { color: red; }</style>'],
    ['a single-quoted style attribute', "<div style='color: red'></div>"],
    ['an attribute style binding', '<div [attr.style]="x"></div>'],
    ['a host style binding', "host: { '[style.color]': 'x' }"],
  ])('reports %s in a template or component', (_, source) => {
    expect(templateViolations([{ file: 'x.ts', source }]).length).toBeGreaterThan(0);
  });

  it.each([
    ['an internal class', '.mdc-button { margin: 0; }'],
    ['a Material element outside the allowlist', 'mat-form-field { color: red; }'],
    ['!important', '.a { color: red !important; }'],
    ['another origin', '@font-face { src: url( //evil.example/a.woff2); }'],
    ['a quoted address with spaces', '.a { background: url(" https://evil.example/a.png"); }'],
  ])('reports %s in the theme layer', (_, css) => {
    expect(themeViolations([{ file: 'x.scss', css }]).length).toBeGreaterThan(0);
  });
});
