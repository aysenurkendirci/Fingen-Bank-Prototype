import { ChangeDetectionStrategy, Component, effect, inject, input, output } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { Account } from '../../../data-access/models/banking.models';
import { AutoSaveMode, AutoSaveRequest, Goal } from '../../../data-access/models/savings.models';
import { BaseForm } from '../../../shared/forms/base-form';

/**
 * The goal's piggy-bank rule: which account it watches, how much it takes and from what size of incoming money
 * on. A minimum like a salary's keeps small refunds and transfers between friends out of it.
 */
@Component({
  selector: 'app-auto-save-form',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <form class="grid gap-4 sm:grid-cols-2" [formGroup]="form" (ngSubmit)="submit()">
      <label class="field sm:col-span-2">
        <span class="field-label">İzlenecek hesap</span>
        <select class="input" formControlName="sourceAccountId">
          @for (account of accounts(); track account.id) {
            <option [ngValue]="account.id">{{ account.iban.slice(-8) }}</option>
          }
        </select>
        <span class="text-xs text-ink-faint">Genellikle maaşınızın yattığı hesap.</span>
      </label>

      <fieldset class="sm:col-span-2">
        <legend class="field-label mb-2">Ne kadar ayrılsın?</legend>
        <div class="segmented">
          @for (option of modes; track option.value) {
            <button type="button" [attr.aria-pressed]="form.controls.mode.value === option.value" (click)="form.controls.mode.setValue(option.value)">
              {{ option.label }}
            </button>
          }
        </div>
      </fieldset>

      <label class="field">
        <span class="field-label">{{ form.controls.mode.value === 'Percent' ? 'Oran (%)' : 'Tutar' }}</span>
        <input class="input tabular-nums" type="number" min="0.01" [max]="form.controls.mode.value === 'Percent' ? 100 : null" step="0.01" formControlName="value" />
      </label>
      <label class="field">
        <span class="field-label">Şu tutar ve üzeri girişlerde</span>
        <input class="input tabular-nums" type="number" min="0" step="100" formControlName="minIncoming" />
      </label>

      <div class="flex flex-wrap gap-2 sm:col-span-2">
        <button type="submit" class="btn-primary" [disabled]="busy()">Kuralı kaydet</button>
        @if (goal().autoSave) {
          <button type="button" class="btn-danger" [disabled]="busy()" (click)="remove.emit()">Kuralı kaldır</button>
        }
        <button type="button" class="btn-secondary" (click)="cancel.emit()">Vazgeç</button>
      </div>
    </form>
  `,
})
export class AutoSaveFormComponent extends BaseForm<AutoSaveRequest> {
  readonly goal = input.required<Goal>();
  readonly accounts = input.required<readonly Account[]>();
  readonly remove = output<void>();

  protected readonly modes: readonly { value: AutoSaveMode; label: string }[] = [
    { value: 'Percent', label: 'Gelen paranın yüzdesi' },
    { value: 'Fixed', label: 'Sabit tutar' },
  ];

  protected readonly form = inject(FormBuilder).nonNullable.group({
    sourceAccountId: [0, Validators.min(1)],
    mode: ['Percent' as AutoSaveMode],
    value: [10, [Validators.required, Validators.min(0.01)]],
    minIncoming: [0, [Validators.required, Validators.min(0)]],
  });

  constructor() {
    super();
    effect(() => {
      const rule = this.goal().autoSave;
      this.form.reset({
        sourceAccountId: rule?.sourceAccountId ?? this.accounts()[0]?.id ?? 0,
        mode: rule?.mode ?? 'Percent',
        value: rule?.value ?? 10,
        minIncoming: rule?.minIncoming ?? 0,
      });
    });
  }

  protected toRequest(): AutoSaveRequest {
    const value = this.form.getRawValue();
    return { ...value, sourceAccountId: Number(value.sourceAccountId) };
  }
}
