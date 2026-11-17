import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { ExchangeRates } from '../../../data-access/models/market.models';
import { CurrencyConverterComponent } from './currency-converter.component';

// The currencies a retail customer here actually holds, in the order they are usually asked about.
const SHOWN = ['USD', 'EUR', 'GBP', 'CHF'];

/** Today's rates and a converter. Who published the rates and for which day is always shown with them. */
@Component({
  selector: 'app-exchange-rates-panel',
  imports: [DatePipe, DecimalPipe, CurrencyConverterComponent],
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

    <app-currency-converter class="mt-5 pt-4 border-t border-line" [foreignCurrencies]="shownCodes()" />
  `,
})
export class ExchangeRatesPanelComponent {
  readonly rates = input.required<ExchangeRates>();

  protected readonly shown = computed(() =>
    SHOWN.map((code) => this.rates().rates.find((r) => r.currencyCode === code)).filter((r) => r !== undefined),
  );

  protected readonly shownCodes = computed(() => this.shown().map((r) => r.currencyCode));
}
