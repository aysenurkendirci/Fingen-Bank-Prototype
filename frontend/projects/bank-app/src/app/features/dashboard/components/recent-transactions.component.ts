import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { TransactionItem } from '../../../data-access/models/dashboard.models';

/** The latest account movements. Incoming money is green; outgoing stays in the normal text colour. */
@Component({
  selector: 'app-recent-transactions',
  imports: [CurrencyPipe, DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <h2 class="section-title mb-3">Son hareketler</h2>
    <div class="bg-surface rounded-panel border border-line">
      @if (transactions().length === 0) {
        <p class="p-5 text-sm text-ink-soft">Henüz işlem yok.</p>
      }
      <ul class="divide-y divide-line">
        @for (tx of transactions(); track tx.txId) {
          <li class="grid grid-cols-[1fr_auto] gap-x-4 gap-y-0.5 px-4 py-3">
            <p class="text-sm font-medium text-ink truncate">{{ tx.title }}</p>
            <p class="text-sm font-medium tabular-nums text-right" [class]="tx.direction === 'Inbound' ? 'text-positive' : 'text-ink'">
              {{ tx.direction === 'Inbound' ? '+' : '−' }}{{ tx.amount | currency }}
            </p>
            <p class="text-xs text-ink-faint truncate">{{ tx.category }}</p>
            <p class="text-xs text-ink-faint text-right">{{ tx.createdAt | date: 'd MMM, HH:mm' }}</p>
          </li>
        }
      </ul>
    </div>
  `,
})
export class RecentTransactionsComponent {
  readonly transactions = input.required<readonly TransactionItem[]>();
}
