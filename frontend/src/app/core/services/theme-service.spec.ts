import { TestBed } from '@angular/core/testing';
import { ThemeService } from './theme-service';

describe('ThemeService', () => {
  const root = document.documentElement;

  beforeEach(() => {
    localStorage.clear();
    root.removeAttribute('data-theme');
    TestBed.resetTestingModule();
  });

  it('follows the system preference by default', () => {
    const service = TestBed.inject(ThemeService);

    expect(service.preference()).toBe('system');
    expect(root.hasAttribute('data-theme')).toBe(false);
  });

  it('pins the dark theme and remembers the choice', () => {
    TestBed.inject(ThemeService).setPreference('dark');

    expect(root.getAttribute('data-theme')).toBe('dark');
    expect(localStorage.getItem('itmas3.theme')).toBe('dark');
  });

  it('restores a stored preference on start-up', () => {
    localStorage.setItem('itmas3.theme', 'light');

    const service = TestBed.inject(ThemeService);

    expect(service.preference()).toBe('light');
    expect(root.getAttribute('data-theme')).toBe('light');
  });

  it('ignores unknown stored values', () => {
    localStorage.setItem('itmas3.theme', 'neon');

    expect(TestBed.inject(ThemeService).preference()).toBe('system');
  });
});
