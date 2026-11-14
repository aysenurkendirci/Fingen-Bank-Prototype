import { Injectable, computed, effect, signal } from '@angular/core';

export type ThemePreference = 'system' | 'light' | 'dark';
export type Theme = 'light' | 'dark';

// Read by the small script in index.html too, so the right theme is painted before the app starts.
const KEY = 'theme';

const stored = (): ThemePreference => {
  const value = localStorage.getItem(KEY);
  return value === 'light' || value === 'dark' ? value : 'system';
};

/**
 * Light or dark. Follows the operating system until the user picks one, and keeps following it live (a
 * laptop switching to dark at sunset). The colours themselves live in CSS variables; this only says which
 * set applies.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly media = window.matchMedia?.('(prefers-color-scheme: dark)');
  private readonly systemDark = signal(this.media?.matches ?? false);

  readonly preference = signal<ThemePreference>(stored());
  readonly theme = computed<Theme>(() => {
    const preference = this.preference();
    return preference === 'system' ? (this.systemDark() ? 'dark' : 'light') : preference;
  });

  constructor() {
    this.media?.addEventListener('change', (event) => this.systemDark.set(event.matches));

    effect(() => {
      const preference = this.preference();
      const root = document.documentElement;
      if (preference === 'system') root.removeAttribute('data-theme');
      else root.setAttribute('data-theme', preference);
    });
  }

  set(preference: ThemePreference): void {
    this.preference.set(preference);
    if (preference === 'system') localStorage.removeItem(KEY);
    else localStorage.setItem(KEY, preference);
  }

  /** The quick switch in the menu: the opposite of what is on screen now. */
  toggle(): void {
    this.set(this.theme() === 'dark' ? 'light' : 'dark');
  }
}
