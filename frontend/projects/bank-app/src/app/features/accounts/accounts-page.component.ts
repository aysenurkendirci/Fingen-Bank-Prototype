import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { BasePage } from '../../core/page/base-page';
import { ConfirmService } from '../../core/ui/confirm.service';
import { ToastService } from '../../core/ui/toast.service';
import { AccountsApi } from '../../data-access/api/banking.api';
import { MarketApi } from '../../data-access/api/market.api';
import { Account, AccountCurrency } from '../../data-access/models/banking.models';
import { ExchangeRates } from '../../data-access/models/market.models';
import { ACCOUNT_STATUS_LABELS, accountLabel } from '../../shared/finance/account-display';
import { IbanCopyComponent } from '../../shared/finance/iban-copy.component';
import { MoneyComponent } from '../../shared/finance/money.component';
import { hideClosedSavings } from '../../shared/finance/visible-accounts';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { NoticeComponent } from '../../shared/ui/notice.component';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { SkeletonComponent } from '../../shared/ui/skeleton.component';
import { ExchangeRatesPanelComponent } from './components/exchange-rates-panel.component';

/** The customer's accounts, with the way into each statement and into opening a new account. */
@Component({
  imports: [FormsModule, RouterLink, IbanCopyComponent, MoneyComponent, EmptyStateComponent, NoticeComponent, PageHeaderComponent, SkeletonComponent, ExchangeRatesPanelComponent],
  template: `
    <app-page-header title="Hesaplarım" subtitle="Her hesabın bakiyesi, IBAN'ı ve hareketleri. IBAN'a dokunun, kopyalansın.">
      <button type="button" class="btn-secondary" (click)="opening.set(!opening())">Hesap aç</button>
      <a routerLink="/dashboard/transfer" class="btn-primary">Para gönder</a>
    </app-page-header>

    @if (error(); as message) {
      <app-notice class="mb-4" [message]="message" />
    }

    @if (opening()) {
      <form class="panel mb-6 flex flex-wrap items-end gap-3" (ngSubmit)="open()">
        <label class="field">
          <span class="field-label">Para birimi</span>
          <select class="input" name="currency" [(ngModel)]="currency">
            @for (option of currencies; track option.code) {
              <option [ngValue]="option.code">{{ option.label }}</option>
            }
          </select>
        </label>
        <button type="submit" class="btn-primary" [disabled]="busy()">{{ busy() ? 'Açılıyor...' : 'Hesabı aç' }}</button>
        <button type="button" class="btn-secondary" (click)="opening.set(false)">Vazgeç</button>
      </form>
    }

    <div class="grid gap-6 lg:grid-cols-[1fr_20rem] items-start">
      <div class="min-w-0">
        @if (loading() && accounts().length === 0) {
          <app-skeleton />
        } @else if (accounts().length === 0 && !error()) {
          <app-empty-state>Henüz bir hesabınız yok. "Hesap aç"a basın, IBAN'ınız birkaç saniyede hazır.</app-empty-state>
        } @else {
          <ul class="bg-surface rounded-panel border border-line divide-y divide-line">
            @for (account of accounts(); track account.id) {
              <li class="px-4 py-4 flex flex-wrap items-center justify-between gap-x-6 gap-y-2">
                <div class="min-w-0">
                  <p class="text-sm font-medium text-ink">
                    {{ label(account) }} <span class="font-normal text-ink-faint">{{ account.currencyCode }}</span>
                    @if (account.status !== 'Active') {
                      <span class="badge bg-canvas text-ink-soft ml-1">{{ statusLabels[account.status] }}</span>
                    }
                  </p>
                  <app-iban-copy class="block mt-1" [iban]="account.iban" />
                </div>
                <div class="flex items-center gap-5">
                  <p class="text-xl font-semibold tracking-[-0.01em]"><app-money display [amount]="account.balance" [currency]="account.currencyCode" /></p>
                  <a class="btn-link" [routerLink]="['/dashboard/accounts', account.id]">Hareketler</a>
                  @if (account.status === 'Active' && account.accountType !== 'Savings') {
                    <a class="btn-link" routerLink="/dashboard/transfer" [queryParams]="{ from: account.id }">Gönder</a>
                    @if (account.balance === 0) {
                      <button type="button" class="btn-link !text-danger" [disabled]="busy()" (click)="close(account)">Kapat</button>
                    }
                  }
                </div>
              </li>
            }
          </ul>
        }
      </div>

      <!-- Rates arrive on their own request; if they fail the accounts are still shown. -->
      @if (rates(); as rates) {
        <app-exchange-rates-panel [rates]="rates" />
      }
    </div>
  `,
})
export class AccountsPageComponent extends BasePage {
  private readonly api = inject(AccountsApi);
  private readonly toast = inject(ToastService);
  private readonly market = inject(MarketApi);
  private readonly dialog = inject(ConfirmService);

  protected readonly label = accountLabel;
  protected readonly statusLabels = ACCOUNT_STATUS_LABELS;
  protected readonly currencies: readonly { code: AccountCurrency; label: string }[] = [
    { code: 'TRY', label: 'Türk Lirası' },
    { code: 'USD', label: 'ABD Doları' },
    { code: 'EUR', label: 'Euro' },
  ];

  protected readonly accounts = signal<readonly Account[]>([]);
  protected readonly opening = signal(false);
  protected readonly rates = signal<ExchangeRates | null>(null);
  protected currency: AccountCurrency = 'TRY';

  constructor() {
    super();
    this.fetch();
    this.market.exchangeRates().subscribe({ next: (rates) => this.rates.set(rates), error: () => undefined });
  }

  protected open(): void {
    this.act(this.api.open(this.currency), {
      fallback: 'Hesap açılamadı.',
      onDone: () => {
        this.opening.set(false);
        this.toast.show('Hesabınız açıldı.');
        this.fetch();
      },
    });
  }

  /** Only an empty account can be closed; the last lira demand account stays, and the API says so if asked. */
  protected async close(account: Account): Promise<void> {
    const yes = await this.dialog.ask({
      title: 'Hesap kapatılsın mı?',
      message: `${account.iban.slice(-8)} ile biten hesabınız kapatılacak. Kapanan hesaba para gönderilemez.`,
      confirmLabel: 'Hesabı kapat',
      danger: true,
    });
    if (!yes) return;

    this.act(this.api.close(account.id), {
      fallback: 'Hesap kapatılamadı.',
      onDone: () => {
        this.toast.show('Hesabınız kapatıldı.');
        this.fetch();
      },
    });
  }

  private fetch(): void {
    this.load(this.api.list(), (accounts) => this.accounts.set(hideClosedSavings(accounts)), 'Hesaplar yüklenemedi.');
  }
}
