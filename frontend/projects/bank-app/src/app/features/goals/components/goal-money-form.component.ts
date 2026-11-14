import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { Account } from '../../../data-access/models/banking.models';
import { Goal, GoalMoneyRequest } from '../../../data-access/models/savings.models';
import { BaseForm } from '../../../shared/forms/base-form';

/** Moves money into a goal from an account, or back out of it to one. */
@Component({
  selector: 'app-goal-money-form',
  imports: [CurrencyPipe, ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <form class="flex flex-wrap items-end gap-3" [formGroup]="form" (ngSubmit)="submit()">
      <label class="field min-w-48 flex-1">
        <span class="field-label">{{ direction() === 'deposit' ? 'Hangi hesaptan' : 'Hangi hesaba' }}</span>
        <select class="input" formControlName="accountId">
          @for (account of accounts(); track account.id) {
            <option [ngValue]="account.id">{{ account.iban.slice(-8) }} ({{ account.balance | currency: 'TRY' : 'symbol-narrow' }})</option>
          }
        </select>
      </label>
      <label class="field w-40">
        <span class="field-label">Tutar</span>
        <input class="input tabular-nums" type="number" min="0.01" step="0.01" formControlName="amount" />
      </label>
      <button type="submit" class="btn-primary" [disabled]="busy()">{{ direction() === 'deposit' ? 'Kumbaraya ekle' : 'Hesaba aktar' }}</button>
      <button type="button" class="btn-secondary" (click)="cancel.emit()">Vazgeç</button>
      @if (form.controls.amount.touched && form.controls.amount.invalid) {
        <p class="field-error w-full">Sıfırdan büyük bir tutar girin{{ direction() === 'withdraw' ? ', en fazla kumbaradaki kadar' : '' }}.</p>
      }
    </form>
  `,
})
export class GoalMoneyFormComponent extends BaseForm<GoalMoneyRequest> {
  readonly goal = input.required<Goal>();
  readonly accounts = input.required<readonly Account[]>();
  readonly direction = input.required<'deposit' | 'withdraw'>();

  private readonly limit = computed(() => (this.direction() === 'withdraw' ? this.goal().savedAmount : null));

  protected readonly form = inject(FormBuilder).nonNullable.group({
    accountId: [0, Validators.min(1)],
    amount: [null as number | null, [Validators.required, Validators.min(0.01)]],
  });

  constructor() {
    super();
    effect(() => {
      const limit = this.limit();
      this.form.reset({ accountId: this.accounts()[0]?.id ?? 0, amount: limit });
      this.form.controls.amount.setValidators(
        limit === null ? [Validators.required, Validators.min(0.01)] : [Validators.required, Validators.min(0.01), Validators.max(limit)],
      );
    });
  }

  protected toRequest(): GoalMoneyRequest {
    const { accountId, amount } = this.form.getRawValue();
    return { accountId: Number(accountId), amount: amount ?? 0 };
  }
}
