import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { formatIban } from '../../../core/util/iban';
import { Account } from '../../../data-access/models/banking.models';
import { ShareAsked } from '../../../data-access/models/split.models';
import { ShareStatusComponent } from './share-status.component';

export type ShareRequestAction = { kind: 'pay'; sourceAccountId: number } | { kind: 'decline' };

/** A share someone asked of the customer: who, what for, how much, and paying or declining it. */
@Component({
  selector: 'app-share-request-card',
  imports: [CurrencyPipe, DatePipe, FormsModule, ShareStatusComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'panel block' },
  template: `
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div class="min-w-0">
        <p class="text-xs text-ink-faint">{{ request().ownerName }} istiyor</p>
        <h3 class="section-title">{{ request().title }}</h3>
        <p class="text-xs text-ink-faint mt-0.5">{{ request().spentOn | date: 'd MMMM y' }}</p>
      </div>
      <app-share-status [status]="request().status" />
    </div>

    <p class="mt-4 text-2xl font-semibold tracking-[-0.02em] tabular-nums">{{ request().amount | currency: 'TRY' : 'symbol-narrow' }}</p>
    <p class="text-sm text-ink-soft">Alıcı hesap {{ printed(request().receivingIban) }}</p>

    @if (request().status === 'Open') {
      <div class="mt-4 flex flex-wrap items-end gap-3">
        <label class="field min-w-56 flex-1">
          <span class="field-label">Hangi hesaptan?</span>
          <select class="input" name="source" [ngModel]="source()" (ngModelChange)="source.set($event)">
            @for (account of accounts(); track account.id) {
              <option [ngValue]="account.id">{{ printed(account.iban) }} ({{ account.balance | currency: 'TRY' : 'symbol-narrow' }})</option>
            }
          </select>
        </label>
        <button type="button" class="btn-primary" [disabled]="busy() || source() === null || !enough()" (click)="pay()">Payımı öde</button>
        <button type="button" class="btn-secondary" [disabled]="busy()" (click)="act.emit({ kind: 'decline' })">Reddet</button>
      </div>
      @if (source() !== null && !enough()) {
        <p class="field-error mt-2">Seçtiğiniz hesabın bakiyesi bu payı karşılamıyor.</p>
      }
    }
  `,
})
export class ShareRequestCardComponent {
  readonly request = input.required<ShareAsked>();
  /** The customer's open lira accounts to pay from. */
  readonly accounts = input.required<readonly Account[]>();
  readonly busy = input(false);
  readonly act = output<ShareRequestAction>();

  protected readonly printed = formatIban;
  protected readonly source = signal<number | null>(null);
  protected readonly enough = computed(() => {
    const account = this.accounts().find((a) => a.id === this.source());
    return !!account && account.balance >= this.request().amount;
  });

  constructor() {
    // The account that can pay is the better first choice.
    effect(() => {
      if (this.source() !== null) return;
      const accounts = this.accounts();
      const able = accounts.find((a) => a.balance >= this.request().amount) ?? accounts[0];
      if (able) this.source.set(able.id);
    });
  }

  protected pay(): void {
    const source = this.source();
    if (source !== null) this.act.emit({ kind: 'pay', sourceAccountId: source });
  }
}
