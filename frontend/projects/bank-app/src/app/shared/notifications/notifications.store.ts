import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';

import { NotificationsApi } from '../../data-access/api/notifications.api';

const CHECK_EVERY_MS = 60_000;

/**
 * The unread count behind the bell in the menu. Checked on every move to another screen, once a minute while the
 * tab is in view and again when the customer comes back to it; a hidden tab asks nothing. A failed check keeps
 * the last count.
 */
@Injectable({ providedIn: 'root' })
export class NotificationsStore {
  private readonly api = inject(NotificationsApi);
  private readonly router = inject(Router);

  private readonly unreadSignal = signal(0);
  readonly unread = this.unreadSignal.asReadonly();

  private timer: ReturnType<typeof setInterval> | null = null;
  private readonly onVisible = (): void => {
    if (document.visibilityState === 'visible') this.refresh();
  };

  /** Starts watching for the signed-in frame and stops when that frame goes away, e.g. on sign-out. */
  watch(destroyRef: DestroyRef): void {
    this.refresh();
    this.timer ??= setInterval(() => {
      if (document.visibilityState === 'visible') this.refresh();
    }, CHECK_EVERY_MS);
    document.addEventListener('visibilitychange', this.onVisible);
    this.router.events
      .pipe(
        filter((e) => e instanceof NavigationEnd),
        takeUntilDestroyed(destroyRef),
      )
      .subscribe(() => this.refresh());

    destroyRef.onDestroy(() => {
      if (this.timer) clearInterval(this.timer);
      this.timer = null;
      document.removeEventListener('visibilitychange', this.onVisible);
      this.unreadSignal.set(0);
    });
  }

  refresh(): void {
    this.api.feed(1).subscribe({ next: (feed) => this.unreadSignal.set(feed.unreadCount), error: () => undefined });
  }

  /** Set by the notifications page, which has just read the full count. */
  setUnread(count: number): void {
    this.unreadSignal.set(Math.max(0, count));
  }
}
