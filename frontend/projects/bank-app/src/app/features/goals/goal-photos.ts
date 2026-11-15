import { DestroyRef, inject, signal } from '@angular/core';

import { GoalsApi } from '../../data-access/api/goals.api';
import { Goal } from '../../data-access/models/savings.models';

/**
 * The photos of the goals on screen, as object URLs. A photo is behind sign-in, so an <img> cannot load it by
 * address; it is fetched with the session and handed to the cards. A photo is fetched again only when it changed,
 * and every URL is released when it is replaced or the page closes.
 */
export class GoalPhotos {
  private readonly api = inject(GoalsApi);
  private readonly loaded = new Map<number, { version: string; url: string }>();
  /** The latest version wanted per goal; an answer for an older one is ignored. */
  private desired = new Map<number, string>();
  private readonly urlsSignal = signal<Readonly<Partial<Record<number, string>>>>({});

  readonly urls = this.urlsSignal.asReadonly();

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      for (const { url } of this.loaded.values()) URL.revokeObjectURL(url);
      this.loaded.clear();
    });
  }

  /** Brings the photos in line with the goals: new or changed ones are fetched, removed ones released. */
  sync(goals: readonly Goal[]): void {
    const wanted = new Map(goals.filter((g) => g.photoUpdatedAt).map((g) => [g.id, g.photoUpdatedAt!]));
    this.desired = wanted;

    for (const [id, { url, version }] of this.loaded) {
      if (wanted.get(id) !== version) this.drop(id, url);
    }

    for (const [id, version] of wanted) {
      if (this.loaded.has(id)) continue;
      // A photo that fails to load leaves the card with the icon of its kind.
      this.api.photo(id).subscribe({
        next: (blob) => {
          if (this.desired.get(id) !== version || this.loaded.has(id)) return;
          const url = URL.createObjectURL(blob);
          this.loaded.set(id, { version, url });
          this.urlsSignal.update((urls) => ({ ...urls, [id]: url }));
        },
        error: () => undefined,
      });
    }
  }

  private drop(id: number, url: string): void {
    URL.revokeObjectURL(url);
    this.loaded.delete(id);
    this.urlsSignal.update((urls) => {
      const { [id]: _, ...rest } = urls;
      return rest;
    });
  }
}
