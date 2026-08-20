import { TestBed } from '@angular/core/testing';
import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  const KEY = 'itmas.theme';

  function makeService(): ThemeService {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    return TestBed.inject(ThemeService);
  }

  /** The `effect()` that syncs <html> and localStorage runs on flush. */
  function flush(): void {
    TestBed.tick();
  }

  beforeEach(() => {
    localStorage.removeItem(KEY);
    document.documentElement.removeAttribute('data-theme');
  });

  afterAll(() => {
    localStorage.removeItem(KEY);
    document.documentElement.removeAttribute('data-theme');
  });

  it('defaults to system when nothing is stored', () => {
    expect(makeService().mode()).toBe('system');
  });

  it('restores a stored mode', () => {
    localStorage.setItem(KEY, 'dark');
    expect(makeService().mode()).toBe('dark');
  });

  it('falls back to system when the stored value is not a mode', () => {
    localStorage.setItem(KEY, 'chartreuse');
    expect(makeService().mode()).toBe('system');
  });

  it('sets data-theme on <html> for an explicit mode and persists it', () => {
    const service = makeService();

    service.set('dark');
    flush();

    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    expect(localStorage.getItem(KEY)).toBe('dark');
  });

  it('removes data-theme for system so color-scheme follows the OS', () => {
    const service = makeService();

    service.set('light');
    flush();
    expect(document.documentElement.getAttribute('data-theme')).toBe('light');

    service.set('system');
    flush();
    expect(document.documentElement.hasAttribute('data-theme')).toBeFalse();
    expect(localStorage.getItem(KEY)).toBe('system');
  });

  it('cycles system -> light -> dark -> system', () => {
    const service = makeService();
    expect(service.mode()).toBe('system');

    service.cycle();
    expect(service.mode()).toBe('light');

    service.cycle();
    expect(service.mode()).toBe('dark');

    service.cycle();
    expect(service.mode()).toBe('system');
  });

  it('exposes the icon and the accessible-name key of the current mode', () => {
    const service = makeService();

    expect(service.icon()).toBe('brightness_auto');

    service.set('light');
    expect(service.icon()).toBe('light_mode');

    service.set('dark');
    expect(service.icon()).toBe('dark_mode');
    // A message key since step 7; the template translates it.
    expect(service.labelKey()).toBe('theme.dark');
  });
});
