import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { NAVIGATION } from '../../layout/navigation/navigation-items';
import { ManualIndex } from './data/manual.models';

/**
 * Keeps the user manual in step with the application (constitution §10.4, SPEC-0004 RF-7): every index entry has its
 * file and vice versa, ids are unique, and every navigation entry is explained by at least one process.
 */
describe('User manual consistency', () => {
  const folder = join(process.cwd(), 'public', 'manual');
  const index = JSON.parse(readFileSync(join(folder, 'index.json'), 'utf8')) as ManualIndex;
  const files = readdirSync(folder).filter((name) => name.endsWith('.md'));

  it('has a Markdown file for every process and no file outside the index', () => {
    const ids = index.processes.map((process) => process.id);
    expect(ids.filter((id) => !existsSync(join(folder, `${id}.md`)))).toEqual([]);
    expect(files.filter((file) => !ids.includes(file.replace(/\.md$/, '')))).toEqual([]);
  });

  it('has unique ids, known categories and complete entries', () => {
    const ids = index.processes.map((process) => process.id);
    const categories = new Set(index.categories.map((category) => category.id));
    expect(new Set(ids).size).toBe(ids.length);
    for (const process of index.processes) {
      expect(process.id, process.id).toMatch(/^[a-z0-9-]+$/);
      expect(categories.has(process.category), `${process.id}: categoría ${process.category}`).toBe(
        true,
      );
      expect(process.title && process.summary, process.id).toBeTruthy();
      expect(process.updated, process.id).toMatch(/^\d{4}-\d{2}-\d{2}$/);
      expect(process.specs.length, process.id).toBeGreaterThan(0);
    }
  });

  it('explains every screen of the navigation', () => {
    const explained = new Set(index.processes.flatMap((process) => process.routes));
    const routes = NAVIGATION.flatMap((group) => group.items.map((item) => item.route));
    expect(routes.filter((route) => !explained.has(route))).toEqual([]);
  });

  it('starts every process with its title and links only to existing processes', () => {
    const ids = new Set(index.processes.map((process) => process.id));
    for (const process of index.processes) {
      const source = readFileSync(join(folder, `${process.id}.md`), 'utf8');
      expect(
        source.startsWith(`# ${process.title}\n`),
        `${process.id}: el título no coincide con el índice`,
      ).toBe(true);
      const links = [...source.matchAll(/\]\(([a-z0-9-]+)\)/g)].map((match) => match[1]);
      expect(
        links.filter((link) => !ids.has(link)),
        process.id,
      ).toEqual([]);
    }
  });
});
