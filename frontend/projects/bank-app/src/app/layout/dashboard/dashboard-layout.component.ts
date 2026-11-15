import { Component, DestroyRef, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { SessionService } from '../../core/auth/session.service';
import { ThemeService } from '../../core/ui/theme.service';
import { VoiceService } from '../../core/ui/voice.service';
import { NotificationsStore } from '../../shared/notifications/notifications.store';
import { PreferencesStore } from '../../shared/preferences/preferences.store';
import { BrandMarkComponent } from '../../shared/ui/brand-mark.component';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog.component';
import { ToastHostComponent } from '../../shared/ui/toast-host.component';

interface NavItem {
  readonly path: string;
  readonly label: string;
  /** The label in the young mode. */
  readonly young: string;
  readonly icon: string;
}

interface NavGroup {
  readonly title: string;
  readonly young: string;
  readonly items: readonly NavItem[];
}

/** The frame of the signed-in app: the menu, the user's corner with the notification bell, and the dialog and toast outlets. */
@Component({
  selector: 'app-dashboard-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, BrandMarkComponent, ConfirmDialogComponent, ToastHostComponent],
  templateUrl: './dashboard-layout.component.html',
})
export class DashboardLayoutComponent {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);

  protected readonly fullName = this.session.fullName;
  protected readonly theme = inject(ThemeService);
  private readonly preferences = inject(PreferencesStore);
  private readonly notifications = inject(NotificationsStore);
  protected readonly unread = this.notifications.unread;

  private readonly voice = inject(VoiceService);

  private readonly groups: readonly NavGroup[] = [
    {
      title: 'Bankacılık', young: 'Param',
      items: [
        { path: '/dashboard', label: 'Ana Sayfa', young: 'Özet', icon: 'home' },
        { path: '/dashboard/accounts', label: 'Hesaplarım', young: 'Hesapların', icon: 'account_balance' },
        { path: '/dashboard/transfer', label: 'Para transferi', young: 'Para gönder', icon: 'send_money' },
        { path: '/dashboard/bank-cards', label: 'Banka kartlarım', young: 'Kartların', icon: 'payments' },
        { path: '/dashboard/splits', label: 'Harcama bölme', young: 'Hesabı bölüş', icon: 'call_split' },
      ],
    },
    {
      title: 'Birikim', young: 'Biriktir',
      items: [
        { path: '/dashboard/goals', label: 'Hedeflerim', young: 'Kumbaraların', icon: 'savings' },
        { path: '/dashboard/new-year-goals', label: 'İstek listem', young: 'Dilek listen', icon: 'featured_seasonal_and_gifts' },
      ],
    },
    {
      title: 'Planlama', young: 'Planla',
      items: [
        { path: '/dashboard/cash-flow', label: 'Nakit akışı', young: 'Önümüzdeki aylar', icon: 'timeline' },
        { path: '/dashboard/scenarios', label: 'Taksit senaryosu', young: 'Taksit mi, peşin mi?', icon: 'compare_arrows' },
        { path: '/dashboard/recurring', label: 'Gelir ve giderler', young: 'Gelirlerin, giderlerin', icon: 'event_repeat' },
        { path: '/dashboard/cards', label: 'Kartlar ve taksitler', young: 'Kart borçların', icon: 'credit_card' },
        { path: '/dashboard/transactions', label: 'İşlem geçmişi', young: 'Harcamaların', icon: 'receipt_long' },
        { path: '/dashboard/analytics', label: 'Analiz', young: 'Nereye gidiyor?', icon: 'insights' },
      ],
    },
  ];

  /** The menu in the words of the current mode. */
  protected readonly navGroups = computed(() =>
    this.groups.map((group) => ({
      title: this.voice.pick(group.title, group.young),
      items: group.items.map((item) => ({ ...item, label: this.voice.pick(item.label, item.young) })),
    })),
  );

  constructor() {
    this.preferences.load();
    this.notifications.watch(inject(DestroyRef));
  }

  protected flipTheme(): void {
    this.preferences.chooseTheme(this.theme.opposite());
  }

  protected logout(): void {
    this.session.signOut().subscribe(() => this.router.navigateByUrl('/auth/login'));
  }
}
