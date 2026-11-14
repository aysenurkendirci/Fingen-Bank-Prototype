import { ChangeDetectionStrategy, Component, DestroyRef, booleanAttribute, computed, effect, inject, input, signal, untracked } from '@angular/core';

import { BalancePrivacyService } from '../../core/ui/balance-privacy.service';

const formatters = new Map<string, Intl.NumberFormat>();

function formatter(currency: string): Intl.NumberFormat {
  let format = formatters.get(currency);
  if (!format) {
    format = new Intl.NumberFormat('tr-TR', { style: 'currency', currency, currencyDisplay: 'narrowSymbol' });
    formatters.set(currency, format);
  }
  return format;
}

/**
 * An amount of money. As a headline (`display`) the kuruş are set smaller than the lira, the way a figure is
 * read aloud. Balances also respect the "hide my balances" choice; a receipt passes `concealable` false.
 */
@Component({
  selector: 'app-money',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'tabular-nums whitespace-nowrap' },
  template: `
    @if (concealable() && privacy.hidden()) {
      <span aria-label="Tutar gizli">••••••</span>
    } @else if (display()) {
      {{ parts().whole }}<span class="text-[0.58em] font-medium align-[0.08em] ml-px">{{ parts().minor }}</span>
    } @else {
      {{ parts().whole }}{{ parts().minor }}
    }
  `,
})
export class MoneyComponent {
  readonly amount = input.required<number>();
  readonly currency = input('TRY');
  /** Headline treatment, for the one big figure on a screen. */
  readonly display = input(false, { transform: booleanAttribute });
  readonly concealable = input(true, { transform: booleanAttribute });
  /** Counts up to the amount when it first appears or changes; for the one headline figure on a screen. */
  readonly countUp = input(false, { transform: booleanAttribute });

  /** What is on screen right now: the amount itself, or a step on the way to it while counting. */
  private readonly shown = signal(0);

  protected readonly privacy = inject(BalancePrivacyService);

  /** "₺183.509" and ",75": the decimal separator and digits after it are the minor part. */
  protected readonly parts = computed(() => {
    let whole = '';
    let minor = '';
    for (const part of formatter(this.currency()).formatToParts(this.shown())) {
      if (part.type === 'decimal' || part.type === 'fraction') minor += part.value;
      else whole += part.value;
    }
    return { whole, minor };
  });

  constructor() {
    let frame = 0;
    inject(DestroyRef).onDestroy(() => cancelAnimationFrame(frame));

    effect(() => {
      const target = this.amount();
      const still = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
      cancelAnimationFrame(frame);

      if (!this.countUp() || still) {
        this.shown.set(target);
        return;
      }

      // Ease-out over 0.8 s, rounded to the kuruş on every frame. Tabular figures keep the width steady.
      const from = untracked(this.shown);
      const start = performance.now();
      const tick = (now: number) => {
        const t = Math.min(1, (now - start) / 800);
        const eased = 1 - Math.pow(1 - t, 3);
        this.shown.set(t === 1 ? target : Math.round((from + (target - from) * eased) * 100) / 100);
        if (t < 1) frame = requestAnimationFrame(tick);
      };
      frame = requestAnimationFrame(tick);
    });
  }
}
