import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ExchangeRates } from '../../../data-access/models/market.models';

// The currencies a retail customer here actually holds, in the order they are usually asked about.
const SHOWN = ['USD', 'EUR', 'GBP', 'CHF'];

/** Today's rates and a small converter. Who published the rates and for which day is always shown with them. */
@Component({
  selector: 'app-exchange-rates-panel',
  imports: [DatePipe, DecimalPipe, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'panel block' },
  template: `
    <h2 class="section-title">Döviz kurları</h2>
    <p class="text-xs text-ink-faint mt-1">
      {{ rates().source }}, {{ rates().publishedOn | date: 'd MMMM y' }}
      @if (rates().stale) {
        <span class="text-warning">(güncel kurlara şu an ulaşılamıyor, son bilinen kurlar)</span>
      }
    </p>

    <table class="mt-4 w-full text-sm">
      <thead>
        <tr class="text-left text-xs text-ink-faint">
          <th class="font-medium pb-2">Döviz</th>
          <th class="font-medium pb-2 text-right">Alış</th>
          <th class="font-medium pb-2 text-right">Satış</th>
        </tr>
      </thead>
      <tbody class="divide-y divide-line">
        @for (rate of shown(); track rate.currencyCode) {
          <tr>
            <td class="py-2">{{ rate.currencyCode }}</td>
            <td class="py-2 text-right tabular-nums">{{ rate.buying | number: '1.4-4' }}</td>
            <td class="py-2 text-right tabular-nums">{{ rate.selling | number: '1.4-4' }}</td>
          </tr>
        }
      </tbody>
    </table>

    <div class="mt-5 pt-4 border-t border-line">
      <p class="field-label mb-2">Hesaplayıcı</p>
      <div class="flex gap-2">
        <input class="input w-full tabular-nums" type="number" min="0" step="0.01" aria-label="Tutar" [ngModel]="amount()" (ngModelChange)="amount.set($event ?? 0)" />
        <select class="input" aria-label="Döviz" [ngModel]="currency()" (ngModelChange)="currency.set($event)">
          @for (rate of shown(); track rate.currencyCode) {
            <option [value]="rate.currencyCode">{{ rate.currencyCode }}</option>
          }
        </select>
      </div>
      @if (conversion(); as c) {
        <dl class="mt-3 text-sm space-y-1">
          <div class="flex justify-between gap-4"><dt class="text-ink-soft">Almak için</dt><dd class="tabular-nums">₺{{ c.buy | number: '1.2-2' }}</dd></div>
          <div class="flex justify-between gap-4"><dt class="text-ink-soft">Bozdurunca</dt><dd class="tabular-nums">₺{{ c.sell | number: '1.2-2' }}</dd></div>
        </dl>
      }
      <p class="mt-2 text-xs text-ink-faint">Gösterge niteliğindedir; işlem kuru farklı olabilir.</p>
    </div>
  `,
})
export class ExchangeRatesPanelComponent {
  readonly rates = input.required<ExchangeRates>();

  protected readonly amount = signal(100);
  protected readonly currency = signal('USD');

  protected readonly shown = computed(() =>
    SHOWN.map((code) => this.rates().rates.find((r) => r.currencyCode === code)).filter((r) => r !== undefined),
  );

  protected readonly conversion = computed(() => {
    const rate = this.rates().rates.find((r) => r.currencyCode === this.currency());
    const amount = Number(this.amount());
    return rate && amount > 0 ? { buy: amount * rate.selling, sell: amount * rate.buying } : null;
  });
}
