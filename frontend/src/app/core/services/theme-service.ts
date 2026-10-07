import { DOCUMENT, Injectable, inject, signal } from '@angular/core';

export type ThemePreference = 'light' | 'dark' | 'system';

const STORAGE_KEY = 'itmas3.theme';
const PREFERENCES: readonly ThemePreference[] = ['light', 'dark', 'system'];

/**
 * Light / dark theme selection. The Material 3 theme is defined once with `color-scheme: light dark`
 * (styles.scss); this service only pins the scheme through `<html data-theme="...">`.
 * 'system' follows the operating-system preference. The choice is a UI preference (not sensitive)
 * persisted in localStorage; the storage can later be swapped for a user-profile setting.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly current = signal<ThemePreference>(this.readStoredPreference());

  readonly preference = this.current.asReadonly();

  constructor() {
    this.apply(this.current());
  }

  setPreference(preference: ThemePreference): void {
    this.current.set(preference);
    this.apply(preference);
    try {
      this.document.defaultView?.localStorage.setItem(STORAGE_KEY, preference);
    } catch {
      // Storage unavailable (private mode, blocked): the preference lasts for this session only.
    }
  }

  private apply(preference: ThemePreference): void {
    const root = this.document.documentElement;
    if (preference === 'system') {
      root.removeAttribute('data-theme');
    } else {
      root.setAttribute('data-theme', preference);
    }
  }

  private readStoredPreference(): ThemePreference {
    try {
      const stored = this.document.defaultView?.localStorage.getItem(STORAGE_KEY);
      return PREFERENCES.find((preference) => preference === stored) ?? 'system';
    } catch {
      return 'system';
    }
  }
}
