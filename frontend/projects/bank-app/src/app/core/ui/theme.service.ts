import { Injectable, computed, effect, signal } from '@angular/core';

import dna from '../../../design/design-dna.json';

export type ThemePreference = 'system' | 'light' | 'dark';
export type Theme = 'light' | 'dark';

const PALETTES = dna.design_system.accents;
export type Accent = keyof typeof PALETTES;

/** The accent colours on offer, in the design profile's order; the first is the default. */
export const ACCENTS = (Object.keys(PALETTES) as Accent[]).map((value) => ({
  value,
  label: PALETTES[value].label,
  swatch: { light: PALETTES[value].light.brand.DEFAULT, dark: PALETTES[value].dark.brand.DEFAULT },
}));
const DEFAULT_ACCENT = ACCENTS[0].value;

// Read by the small script in index.html too, so the right look is painted before the app starts.
const THEME_KEY = 'theme';
const ACCENT_KEY = 'accent';

const storedTheme = (): ThemePreference => {
  const value = localStorage.getItem(THEME_KEY);
  return value === 'light' || value === 'dark' ? value : 'system';
};

const storedAccent = (): Accent => {
  const value = localStorage.getItem(ACCENT_KEY);
  return value !== null && value in PALETTES ? (value as Accent) : DEFAULT_ACCENT;
};

/**
 * How the app looks on this device: light or dark, and the accent colour. Follows the operating system until
 * the user picks a theme, and keeps following it live. The colours themselves are CSS variables; this only marks
 * the page with which set applies. Saving the choice to the user's account is PreferencesStore's job.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly media = window.matchMedia?.('(prefers-color-scheme: dark)');
  private readonly systemDark = signal(this.media?.matches ?? false);

  readonly preference = signal<ThemePreference>(storedTheme());
  readonly accent = signal<Accent>(storedAccent());
  readonly theme = computed<Theme>(() => {
    const preference = this.preference();
    return preference === 'system' ? (this.systemDark() ? 'dark' : 'light') : preference;
  });

  constructor() {
    this.media?.addEventListener('change', (event) => this.systemDark.set(event.matches));

    effect(() => {
      const root = document.documentElement;
      const preference = this.preference();
      const accent = this.accent();

      if (preference === 'system') root.removeAttribute('data-theme');
      else root.setAttribute('data-theme', preference);

      if (accent === DEFAULT_ACCENT) root.removeAttribute('data-accent');
      else root.setAttribute('data-accent', accent);
    });
  }

  set(preference: ThemePreference): void {
    this.preference.set(preference);
    if (preference === 'system') localStorage.removeItem(THEME_KEY);
    else localStorage.setItem(THEME_KEY, preference);
  }

  setAccent(accent: Accent): void {
    this.accent.set(accent);
    if (accent === DEFAULT_ACCENT) localStorage.removeItem(ACCENT_KEY);
    else localStorage.setItem(ACCENT_KEY, accent);
  }

  /** The opposite of what is on screen now. */
  opposite(): Theme {
    return this.theme() === 'dark' ? 'light' : 'dark';
  }
}
