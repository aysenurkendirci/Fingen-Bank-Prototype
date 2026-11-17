import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Subject, catchError, debounceTime, of, switchMap } from 'rxjs';

import { problemMessage } from '../../../core/http/problem-details';
import { MarketApi } from '../../../data-access/api/market.api';
import { Conversion, ConversionRequest } from '../../../data-access/models/market.models';

/**
 * What an amount of one currency buys in another, asked from the API: the bank's own calculator answers when it
 * is set up, otherwise the conversion is worked out from today's rates. Asks once typing pauses, and only the
 * latest question's answer is shown.
 */
@Component({
  selector: 'app-currency-converter',
  imports: [DecimalPipe, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <p class="field-label mb-2">Hesaplayıcı</p>
    <div class="grid grid-cols-[1fr_auto] gap-2">
      <input class="input tabular-nums" type="number" min="0.01" step="0.01" aria-label="Tutar" name="amount" [ngModel]="amount()" (ngModelChange)="amount.set($event ?? 0)" />
      <select class="input" aria-label="Elinizdeki döviz" name="from" [ngModel]="from()" (ngModelChange)="from.set($event)">
        @for (code of currencies(); track code) {
          <option [value]="code">{{ label(code) }}</option>
        }
      </select>
    </div>
    <div class="mt-2 flex items-center gap-2">
      <button type="button" class="btn-link" aria-label="Yönü değiştir" (click)="swap()">
        <span class="material-symbols-outlined text-[18px] align-middle" aria-hidden="true">swap_vert</span>
      </button>
      <select class="input flex-1" aria-label="Almak istediğiniz döviz" name="to" [ngModel]="to()" (ngModelChange)="to.set($event)">
        @for (code of currencies(); track code) {
          <option [value]="code" [disabled]="code === from()">{{ label(code) }}</option>
        }
      </select>
    </div>

    <div class="mt-3 min-h-12" aria-live="polite">
      @if (error(); as message) {
        <p class="text-sm text-danger">{{ message }}</p>
      } @else if (result(); as r) {
        <p class="text-lg font-semibold tabular-nums" data-testid="conversion-result">{{ r.targetAmount | number: '1.2-2' }} {{ label(r.targetCurrencyCode) }}</p>
        <p class="text-xs text-ink-faint">
          1 {{ foreign(r) }} = {{ r.rate | number: '1.4-4' }} TL, {{ r.source === 'VakıfBank' ? 'VakıfBank hesaplayıcısı' : r.source + ' kurlarıyla' }}
        </p>
      }
    </div>
    <p class="mt-1 text-xs text-ink-faint">Gösterge niteliğindedir; işlem kuru farklı olabilir.</p>
  `,
})
export class CurrencyConverterComponent {
  /** The foreign currencies offered; the lira is always there too. */
  readonly foreignCurrencies = input.required<readonly string[]>();

  private readonly api = inject(MarketApi);
  private readonly asks = new Subject<ConversionRequest>();

  protected readonly amount = signal(1000);
  protected readonly from = signal('TRY');
  protected readonly to = signal('USD');
  protected readonly result = signal<Conversion | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly currencies = computed(() => ['TRY', ...this.foreignCurrencies()]);

  constructor() {
    this.asks
      .pipe(
        debounceTime(350),
        switchMap((request) =>
          this.api.convert(request).pipe(
            catchError((err: unknown) => {
              this.error.set(problemMessage(err, 'Hesaplanamadı.'));
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe((conversion) => {
        if (conversion) {
          this.error.set(null);
          this.result.set(conversion);
        }
      });

    effect(() => {
      const request = { sourceCurrencyCode: this.from(), sourceAmount: Number(this.amount()), targetCurrencyCode: this.to() };
      if (request.sourceAmount > 0 && request.sourceCurrencyCode !== request.targetCurrencyCode) this.asks.next(request);
      else this.result.set(null);
    });
  }

  protected swap(): void {
    const from = this.from();
    this.from.set(this.to());
    this.to.set(from);
  }

  protected label(code: string): string {
    return code === 'TRY' ? 'TL' : code;
  }

  /** The foreign side of a conversion, which the rate is quoted for. */
  protected foreign(c: Conversion): string {
    return c.sourceCurrencyCode === 'TRY' ? c.targetCurrencyCode : c.sourceCurrencyCode;
  }
}
