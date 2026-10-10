import { Marked, Tokens } from 'marked';
import { MANUAL_PATH } from './manual-api';
import { ManualProcess } from './manual.models';

/** Lowercase text without accents, so "Enrolár", "enrolar" and "ENROLAR" match (RF-3). */
export function normalize(text: string): string {
  return text
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .toLocaleLowerCase('es');
}

export function matches(process: ManualProcess, categoryTitle: string, query: string): boolean {
  const wanted = normalize(query.trim());
  return (
    !wanted || normalize(`${process.title} ${process.summary} ${categoryTitle}`).includes(wanted)
  );
}

const markdown = new Marked({
  gfm: true,
  async: false,
  walkTokens: (token) => {
    // Links between processes are written as `(other-process-id)`: point them to the manual page.
    if (token.type === 'link') {
      const link = token as Tokens.Link;
      if (/^[a-z0-9-]+$/.test(link.href)) {
        link.href = `${MANUAL_PATH}/${link.href}`;
      }
    }
  },
});

/**
 * HTML of a process page (without its leading `# Title`, shown from the index). The manual is first-party, versioned
 * content; the result is still inserted with `[innerHTML]`, which Angular sanitizes (no scripts or event handlers).
 */
export function renderProcess(source: string): string {
  const body = source.replace(/^\s*# .*(\r?\n|$)/, '');
  return markdown.parse(body) as string;
}
