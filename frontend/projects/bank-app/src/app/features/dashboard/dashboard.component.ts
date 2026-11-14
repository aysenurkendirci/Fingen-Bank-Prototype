import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { BasePage } from '../../core/page/base-page';
import { BalancePrivacyService } from '../../core/ui/balance-privacy.service';
import { DashboardApi } from '../../data-access/api/dashboard.api';
import { PlanningApi } from '../../data-access/api/finance.api';
import { MarketApi } from '../../data-access/api/market.api';
import { DashboardResponse } from '../../data-access/models/dashboard.models';
import { Projection } from '../../data-access/models/finance.models';
import { ExchangeRates } from '../../data-access/models/market.models';
import { totalInLira } from '../../shared/finance/currency-conversion';
import { NoticeComponent } from '../../shared/ui/notice.component';
import { BankCardTileComponent } from '../../shared/finance/bank-card-tile.component';
import { BalanceSparklineComponent } from '../../shared/finance/balance-sparkline.component';
import { MoneyComponent } from '../../shared/finance/money.component';
import { ProjectionSummaryComponent } from '../../shared/finance/projection-summary.component';
import { runningBalance } from '../../shared/finance/running-balance';
import { SkeletonComponent } from '../../shared/ui/skeleton.component';
import { AccountListComponent } from './components/account-list.component';
import { RecentTransactionsComponent } from './components/recent-transactions.component';

function greetingFor(hour: number): string {
  if (hour < 6) return 'İyi geceler';
  if (hour < 12) return 'Günaydın';
  if (hour < 18) return 'İyi günler';
  return 'İyi akşamlar';
}

/** Container of the home screen: fetches the data and hands it to the display components below. */
@Component({
  selector: 'app-dashboard-page',
  imports: [
    DatePipe,
    RouterLink,
    MoneyComponent,
    BalanceSparklineComponent,
    NoticeComponent,
    SkeletonComponent,
    ProjectionSummaryComponent,
    BankCardTileComponent,
    RecentTransactionsComponent,
    AccountListComponent,
  ],
  templateUrl: './dashboard.component.html',
})
export class DashboardComponent extends BasePage {
  private readonly api = inject(DashboardApi);
  private readonly planning = inject(PlanningApi);
  private readonly market = inject(MarketApi);

  protected readonly privacy = inject(BalancePrivacyService);
  protected readonly today = new Date();
  protected readonly greeting = greetingFor(this.today.getHours());

  protected readonly data = signal<DashboardResponse | null>(null);

  /** The next three months at a glance; loaded on its own so a failure here never hides the accounts. */
  protected readonly outlook = signal<Projection | null>(null);
  /** The outlook failed to load; its section is left out instead of showing a placeholder forever. */
  protected readonly outlookFailed = signal(false);
  protected readonly outlookPoints = computed(() => {
    const p = this.outlook();
    return p ? runningBalance(p.openingBalance, p.from, p.entries) : null;
  });

  protected readonly rates = signal<ExchangeRates | null>(null);

  /** Every active account in lira at today's buying rate, shown only when there is foreign currency to add. */
  protected readonly estimate = computed(() => {
    const data = this.data();
    const rates = this.rates();
    const active = data?.accounts.filter((a) => a.status === 'Active') ?? [];
    if (!rates || !active.some((a) => a.currencyCode !== 'TRY')) return null;
    const total = totalInLira(active, rates.rates);
    return total === null ? null : { total, source: rates.source, publishedOn: rates.publishedOn };
  });

  constructor() {
    super();
    this.market.exchangeRates().subscribe({ next: (rates) => this.rates.set(rates), error: () => undefined });
    this.planning.projection(3).subscribe({
      next: (projection) => this.outlook.set(projection),
      error: () => this.outlookFailed.set(true),
    });
    this.load(this.api.getDashboard(), (response) => this.data.set(response), 'Panel verileri yüklenemedi.');
  }
}
