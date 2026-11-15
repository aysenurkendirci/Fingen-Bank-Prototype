import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';

import { BasePage } from '../../core/page/base-page';
import { NotificationsApi } from '../../data-access/api/notifications.api';
import { AppNotification, NotificationKind } from '../../data-access/models/notification.models';
import { NotificationsStore } from '../../shared/notifications/notifications.store';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { NoticeComponent } from '../../shared/ui/notice.component';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { SkeletonComponent } from '../../shared/ui/skeleton.component';

const ICONS: Record<NotificationKind, string> = {
  'money-in': 'south_west',
  'money-out': 'north_east',
  savings: 'savings',
  goal: 'flag',
  security: 'shield',
};

/** The notification centre: the newest fifty, unread ones marked. Opening one marks it read. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, EmptyStateComponent, NoticeComponent, PageHeaderComponent, SkeletonComponent],
  template: `
    <app-page-header title="Bildirimler" subtitle="Hesaplarınıza giren ve çıkan para, birikimleriniz ve güvenliğinizle ilgili gelişmeler.">
      @if (unread() > 0) {
        <button type="button" class="btn-secondary" [disabled]="busy()" (click)="markAllRead()">Tümünü okundu say</button>
      }
    </app-page-header>

    @if (error(); as message) {
      <app-notice class="mb-4" [message]="message" />
    }

    @if (loading() && items().length === 0) {
      <app-skeleton [rows]="4" />
    } @else if (items().length === 0) {
      <app-empty-state>Henüz bildiriminiz yok. Hesabınıza para geldiğinde ya da bir hedefinize ulaştığınızda burada göreceksiniz.</app-empty-state>
    } @else {
      <ul class="panel !p-0 divide-y divide-line" aria-label="Bildirimler">
        @for (item of items(); track item.notificationId) {
          <li>
            <button
              type="button"
              class="row-link w-full text-left px-4 py-3 sm:px-5 flex gap-3 items-start"
              [attr.aria-label]="(item.readAt ? '' : 'Okunmadı. ') + item.title + '. ' + item.body"
              (click)="open(item)"
            >
              <span
                class="material-symbols-outlined text-[20px] mt-0.5"
                [class.text-brand]="!item.readAt"
                [class.text-ink-faint]="item.readAt"
                aria-hidden="true"
                >{{ icon(item.kind) }}</span
              >
              <span class="min-w-0 flex-1">
                <span class="flex items-baseline justify-between gap-3">
                  <span class="text-sm" [class.font-semibold]="!item.readAt" [class.text-ink-soft]="item.readAt">{{ item.title }}</span>
                  <time class="text-xs text-ink-faint whitespace-nowrap" [attr.datetime]="item.createdAt">{{ item.createdAt | date: 'd MMM, HH:mm' }}</time>
                </span>
                <span class="block text-sm text-ink-soft mt-0.5">{{ item.body }}</span>
              </span>
              @if (!item.readAt) {
                <span class="mt-2 size-2 shrink-0 rounded-full bg-brand" aria-hidden="true"></span>
              }
            </button>
          </li>
        }
      </ul>
    }
  `,
})
export class NotificationsPageComponent extends BasePage {
  private readonly api = inject(NotificationsApi);
  private readonly store = inject(NotificationsStore);

  protected readonly items = signal<AppNotification[]>([]);
  protected readonly unread = computed(() => this.items().filter((n) => !n.readAt).length);

  constructor() {
    super();
    this.load(
      this.api.feed(50),
      (feed) => {
        this.items.set(feed.items);
        this.store.setUnread(feed.unreadCount);
      },
      'Bildirimler yüklenemedi.',
    );
  }

  protected icon(kind: NotificationKind): string {
    return ICONS[kind] ?? 'notifications';
  }

  protected open(item: AppNotification): void {
    if (item.readAt) return;
    // Shown as read at once; the count behind the bell follows. A failed save only means it is unread next time.
    this.markLocally((n) => n.notificationId === item.notificationId);
    this.store.setUnread(this.store.unread() - 1);
    this.api.markRead(item.notificationId).subscribe({ error: () => undefined });
  }

  protected markAllRead(): void {
    this.act(this.api.markAllRead(), {
      fallback: 'Bildirimler okundu olarak işaretlenemedi.',
      onDone: () => {
        this.markLocally(() => true);
        this.store.setUnread(0);
      },
    });
  }

  private markLocally(which: (n: AppNotification) => boolean): void {
    const now = new Date().toISOString();
    this.items.update((items) => items.map((n) => (!n.readAt && which(n) ? { ...n, readAt: now } : n)));
  }
}
