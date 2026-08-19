import { Injectable, computed, effect, signal } from '@angular/core';

/**
 * Colour-scheme preference. `system` follows the operating system via
 * `color-scheme: light dark`; the other two pin `<html data-theme>`.
 * See design.md §2.3.
 */
export type ThemeMode = 'system' | 'light' | 'dark';

const MODES: readonly ThemeMode[] = ['system', 'light', 'dark'];

const ICONS: Record<ThemeMode, string> = {
  // design.md §13.1 gives `light_mode` / `dark_mode` for the theme concept; it has
  // no entry for "follow the system", so MD3's `brightness_auto` is used there.
  system: 'brightness_auto',
  light: 'light_mode',
  dark: 'dark_mode'
};

const LABELS: Record<ThemeMode, string> = {
  system: 'Tema: seguir el sistema',
  light: 'Tema: claro',
  dark: 'Tema: oscuro'
};

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly key = 'itmas.theme';

  readonly mode = signal<ThemeMode>(this.read());

  /** Icon ligature for the toolbar toggle (design.md §9.10). */
  readonly icon = computed(() => ICONS[this.mode()]);

  /** Accessible name for the toolbar toggle, naming the *current* mode. */
  readonly label = computed(() => LABELS[this.mode()]);

  constructor() {
    effect(() => {
      const mode = this.mode();
      const root = document.documentElement;
      if (mode === 'system') root.removeAttribute('data-theme');
      else root.setAttribute('data-theme', mode);
      localStorage.setItem(this.key, mode);
    });
  }

  set(mode: ThemeMode): void {
    this.mode.set(mode);
  }

  /** Advances system → light → dark → system. */
  cycle(): void {
    const next = (MODES.indexOf(this.mode()) + 1) % MODES.length;
    this.mode.set(MODES[next]);
  }

  private read(): ThemeMode {
    const stored = localStorage.getItem(this.key);
    return MODES.includes(stored as ThemeMode) ? (stored as ThemeMode) : 'system';
  }
}
