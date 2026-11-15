import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

import { Share, Split } from '../../../data-access/models/split.models';
import { ShareStatusComponent } from './share-status.component';

export type SplitAction = { kind: 'cash'; share: Share } | { kind: 'cancel' };

/** One split the customer made: what came back, who still owes, and what can be closed by hand. */
@Component({
  selector: 'app-split-card',
  imports: [CurrencyPipe, DatePipe, ShareStatusComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'panel block' },
  template: `
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div class="min-w-0">
        <h3 class="section-title">{{ split().title }}</h3>
        <p class="text-xs text-ink-faint mt-0.5">{{ split().spentOn | date: 'd MMMM y' }}</p>
      </div>
      @if (split().status === 'Cancelled') {
        <span class="badge bg-muted text-ink-soft">Geri çekildi</span>
      } @else if (outstanding() === 0) {
        <span class="badge bg-positive-tint text-positive">Tamamlandı</span>
      }
    </div>

    <p class="mt-4 text-2xl font-semibold tracking-[-0.02em] tabular-nums">{{ split().collected | currency: 'TRY' : 'symbol-narrow' }}</p>
    <p class="text-sm text-ink-soft">
      {{ asked() | currency: 'TRY' : 'symbol-narrow' }} istendi, toplam {{ split().totalAmount | currency: 'TRY' : 'symbol-narrow' }},
      sizin payınız {{ split().ownShare | currency: 'TRY' : 'symbol-narrow' }}
    </p>

    <div class="mt-3 h-2 rounded-full bg-muted overflow-hidden" role="progressbar" [attr.aria-valuenow]="percent()" aria-valuemin="0" aria-valuemax="100" [attr.aria-label]="split().title + ' geri gelen'">
      <div class="h-full rounded-full bg-brand transition-[width] duration-500 ease-enter" [style.width.%]="percent()"></div>
    </div>

    <ul class="mt-4 divide-y divide-line text-sm">
      @for (share of split().shares; track share.id) {
        <li class="py-2 flex flex-wrap items-center justify-between gap-x-4 gap-y-1">
          <span class="min-w-0">
            <span class="font-medium">{{ share.name }}</span>
            <span class="text-xs text-ink-faint ml-1">{{ share.inApp ? 'FinGen' : 'uygulama dışı' }}</span>
          </span>
          <span class="flex items-center gap-3">
            <span class="tabular-nums">{{ share.amount | currency: 'TRY' : 'symbol-narrow' }}</span>
            <app-share-status [status]="share.status" />
            @if (share.status === 'Open' && split().status === 'Open') {
              <button type="button" class="btn-link" (click)="act.emit({ kind: 'cash', share })">Ödendi say</button>
            }
          </span>
        </li>
      }
    </ul>

    @if (split().status === 'Open' && outstanding() > 0) {
      <div class="mt-4 flex justify-end">
        <button type="button" class="btn-link !text-danger" (click)="act.emit({ kind: 'cancel' })">Açık istekleri geri çek</button>
      </div>
    }
  `,
})
export class SplitCardComponent {
  readonly split = input.required<Split>();
  readonly act = output<SplitAction>();

  protected readonly asked = computed(() => this.split().shares.reduce((sum, s) => sum + s.amount, 0));
  protected readonly outstanding = computed(() => this.split().shares.filter((s) => s.status === 'Open').length);
  protected readonly percent = computed(() => {
    const asked = this.asked();
    return asked > 0 ? Math.min(100, Math.floor((this.split().collected / asked) * 100)) : 0;
  });
}
