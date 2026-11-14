import { TestBed } from '@angular/core/testing';

import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  let systemDark: boolean;
  let listener: ((event: { matches: boolean }) => void) | undefined;

  beforeEach(() => {
    systemDark = false;
    listener = undefined;
    const stored = new Map<string, string>();
    Object.defineProperty(globalThis, 'localStorage', {
      configurable: true,
      value: {
        getItem: (key: string) => stored.get(key) ?? null,
        setItem: (key: string, value: string) => stored.set(key, value),
        removeItem: (key: string) => stored.delete(key),
      },
    });
    Object.defineProperty(window, 'matchMedia', {
      configurable: true,
      value: () => ({
        get matches() {
          return systemDark;
        },
        addEventListener: (_: string, fn: typeof listener) => (listener = fn),
      }),
    });
  });

  const create = () => TestBed.inject(ThemeService);

  it('follows the system until the user picks a theme', () => {
    systemDark = true;
    const theme = create();

    expect(theme.theme()).toBe('dark');

    theme.set('light');
    expect(theme.theme()).toBe('light');
  });

  it('keeps following the system live', () => {
    const theme = create();
    expect(theme.theme()).toBe('light');

    listener?.({ matches: true });

    expect(theme.theme()).toBe('dark');
  });

  it('marks the page only for an explicit choice', () => {
    const theme = create();

    theme.set('dark');
    TestBed.tick();
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');

    theme.set('system');
    TestBed.tick();
    expect(document.documentElement.hasAttribute('data-theme')).toBe(false);
  });

  it('marks the page with a chosen accent and leaves the default unmarked', () => {
    const theme = create();

    theme.setAccent('ocean');
    TestBed.tick();
    expect(document.documentElement.getAttribute('data-accent')).toBe('ocean');
    expect(localStorage.getItem('accent')).toBe('ocean');

    theme.setAccent('spruce');
    TestBed.tick();
    expect(document.documentElement.hasAttribute('data-accent')).toBe(false);
  });
});
