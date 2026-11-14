import { ChangeDetectionStrategy, Component, effect, inject, input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { todayIsoDate } from '../../../core/util/dates';
import { Goal, GoalRequest } from '../../../data-access/models/savings.models';
import { BaseForm } from '../../../shared/forms/base-form';

/** Add or edit a goal. In the new year area the year is preset and the goal is a resolution from the start. */
@Component({
  selector: 'app-goal-form',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'panel block' },
  template: `
    <form class="grid gap-4 sm:grid-cols-2" [formGroup]="form" (ngSubmit)="submit()">
      <label class="field sm:col-span-2">
        <span class="field-label">Ne için biriktiriyorsunuz?</span>
        <input class="input" formControlName="name" maxlength="80" placeholder="Örn. Araba, ev peşinatı, tatil" />
        @if (form.controls.name.touched && form.controls.name.invalid) {
          <span class="field-error">2 ile 80 karakter arasında bir ad girin.</span>
        }
      </label>

      <label class="field">
        <span class="field-label">Hedef tutar</span>
        <input class="input tabular-nums" type="number" min="1" step="100" formControlName="targetAmount" />
        @if (form.controls.targetAmount.touched && form.controls.targetAmount.invalid) {
          <span class="field-error">Sıfırdan büyük bir tutar girin.</span>
        }
      </label>

      <label class="field">
        <span class="field-label">Hedef tarihi (isteğe bağlı)</span>
        <input class="input" type="date" [min]="tomorrow" formControlName="targetDate" />
      </label>

      <label class="flex items-center gap-3 sm:col-span-2">
        <input type="checkbox" role="switch" class="switch" formControlName="isResolution" />
        <span class="text-sm">Yılbaşı hedefi</span>
        @if (form.controls.isResolution.value) {
          <select class="input h-9" formControlName="newYearOf" aria-label="Hangi yıl için">
            @for (year of years; track year) {
              <option [ngValue]="year">{{ year }}</option>
            }
          </select>
        }
      </label>

      <div class="flex gap-2 sm:col-span-2">
        <button type="submit" class="btn-primary" [disabled]="busy()">{{ goal() ? 'Kaydet' : 'Hedefi oluştur' }}</button>
        <button type="button" class="btn-secondary" (click)="cancel.emit()">Vazgeç</button>
      </div>
    </form>
  `,
})
export class GoalFormComponent extends BaseForm<GoalRequest> {
  readonly goal = input<Goal | null>(null);
  /** Preset year when the form opens in the new year area. */
  readonly resolutionYear = input<number | null>(null);

  protected readonly years = [0, 1, 2].map((i) => new Date().getFullYear() + i);
  protected readonly tomorrow = (() => {
    const d = new Date();
    d.setDate(d.getDate() + 1);
    return d.toISOString().slice(0, 10);
  })();

  protected readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(80)]],
    targetAmount: [null as number | null, [Validators.required, Validators.min(1)]],
    targetDate: [''],
    isResolution: [false],
    newYearOf: [this.years[1]],
  });

  constructor() {
    super();
    effect(() => {
      const goal = this.goal();
      const year = this.resolutionYear();
      this.form.reset({
        name: goal?.name ?? '',
        targetAmount: goal?.targetAmount ?? null,
        targetDate: goal?.targetDate ?? '',
        isResolution: (goal?.newYearOf ?? year) !== null,
        newYearOf: goal?.newYearOf ?? year ?? this.years[1],
      });
    });
  }

  protected toRequest(): GoalRequest {
    const value = this.form.getRawValue();
    return {
      name: value.name.trim(),
      targetAmount: value.targetAmount ?? 0,
      targetDate: value.targetDate && value.targetDate > todayIsoDate() ? value.targetDate : null,
      newYearOf: value.isResolution ? value.newYearOf : null,
    };
  }
}
