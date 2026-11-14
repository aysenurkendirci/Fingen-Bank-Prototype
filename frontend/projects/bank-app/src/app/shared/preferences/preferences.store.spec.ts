import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ThemeService } from '../../core/ui/theme.service';
import { PreferencesStore } from './preferences.store';

describe('PreferencesStore', () => {
  let backend: HttpTestingController;
  let store: PreferencesStore;
  let theme: ThemeService;

  beforeEach(() => {
    const stored = new Map<string, string>();
    Object.defineProperty(globalThis, 'localStorage', {
      configurable: true,
      value: {
        getItem: (key: string) => stored.get(key) ?? null,
        setItem: (key: string, value: string) => stored.set(key, value),
        removeItem: (key: string) => stored.delete(key),
      },
    });

    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    backend = TestBed.inject(HttpTestingController);
    store = TestBed.inject(PreferencesStore);
    theme = TestBed.inject(ThemeService);
  });

  it("takes the account's saved look on sign-in", () => {
    store.load();
    backend.expectOne('/api/me/preferences').flush({ theme: 'dark', accent: 'plum' });

    expect(theme.preference()).toBe('dark');
    expect(theme.accent()).toBe('plum');
  });

  it('shows a change at once and saves the whole choice to the account', () => {
    theme.set('dark');

    store.chooseAccent('ocean');

    expect(theme.accent()).toBe('ocean');
    const save = backend.expectOne('/api/me/preferences');
    expect(save.request.method).toBe('PUT');
    expect(save.request.body).toEqual({ theme: 'dark', accent: 'ocean' });
  });

  it('keeps the look on this device when saving fails', () => {
    store.chooseTheme('light');
    backend.expectOne('/api/me/preferences').flush(null, { status: 503, statusText: 'Unavailable' });

    expect(theme.preference()).toBe('light');
  });
});
