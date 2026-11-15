import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';

import { todayIsoDate } from '../../../core/util/dates';
import { formatIban, isValidIban } from '../../../core/util/iban';
import { Account } from '../../../data-access/models/banking.models';
import { SplitRequestBody } from '../../../data-access/models/split.models';
import { BaseForm } from '../../../shared/forms/base-form';
import { evenShares, ownPart } from '../split-math';

/** An IBAN is optional; when there is one it must be a valid one. */
const optionalIban = (control: AbstractControl<string>): ValidationErrors | null =>
  !control.value.trim() || isValidIban(control.value) ? null : { iban: true };

const MAX_PEOPLE = 20;

/**
 * A new split: what was paid, how much, and who owes which part. A friend with an IBAN banks here and gets the
 * request in the app; without one they are a name, and the payer marks their share once they get the money.
 */
@Component({
  selector: 'app-split-form',
  imports: [CurrencyPipe, ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'panel block' },
  template: `
    <form class="grid gap-5" [formGroup]="form" (ngSubmit)="submit()">
      <div class="grid gap-4 sm:grid-cols-3">
        <label class="field sm:col-span-3">
          <span class="field-label">Ne için ödediniz?</span>
          <input class="input" formControlName="title" maxlength="80" placeholder="Örn. Akşam yemeği, ev kirası, tatil villası" />
          @if (form.controls.title.touched && form.controls.title.invalid) {
            <span class="field-error">2 ile 80 karakter arasında bir ad girin.</span>
          }
        </label>

        <label class="field">
          <span class="field-label">Toplam tutar</span>
          <input class="input tabular-nums" type="number" min="0.01" step="0.01" formControlName="totalAmount" />
          @if (form.controls.totalAmount.touched && form.controls.totalAmount.invalid) {
            <span class="field-error">Sıfırdan büyük bir tutar girin.</span>
          }
        </label>

        <label class="field">
          <span class="field-label">Tarih</span>
          <input class="input" type="date" [max]="today" formControlName="spentOn" />
        </label>

        <label class="field">
          <span class="field-label">Ödemeler hangi hesaba gelsin?</span>
          <select class="input" formControlName="receivingAccountId">
            @for (account of accounts(); track account.id) {
              <option [ngValue]="account.id">{{ printed(account.iban) }}</option>
            }
          </select>
        </label>
      </div>

      <fieldset class="grid gap-3" formArrayName="shares">
        <legend class="field-label mb-1">Kimler ne kadar ödeyecek?</legend>
        @for (share of form.controls.shares.controls; track share; let i = $index) {
          <div class="grid gap-2 sm:grid-cols-[1fr_1.4fr_9rem_auto] items-start" [formGroupName]="i">
            <input class="input" formControlName="name" maxlength="100" placeholder="Ad soyad" [attr.aria-label]="(i + 1) + '. kişinin adı'" />
            <span class="grid gap-1">
              <input
                class="input tabular-nums"
                formControlName="iban"
                placeholder="IBAN (FinGen müşterisiyse)"
                autocomplete="off"
                [attr.aria-label]="(i + 1) + '. kişinin IBAN numarası'"
                (blur)="groupIban(i)"
              />
              @if (share.controls.iban.touched && share.controls.iban.invalid) {
                <span class="field-error">IBAN geçerli değil.</span>
              }
            </span>
            <input class="input tabular-nums" type="number" min="0.01" step="0.01" formControlName="amount" [attr.aria-label]="(i + 1) + '. kişinin payı'" />
            <button type="button" class="btn-link self-center" [disabled]="form.controls.shares.length === 1" (click)="removePerson(i)">Çıkar</button>
          </div>
        }
        <div class="flex flex-wrap gap-2">
          <button type="button" class="btn-secondary" [disabled]="form.controls.shares.length >= maxPeople" (click)="addPerson()">Kişi ekle</button>
          <button type="button" class="btn-secondary" (click)="splitEvenly()">Benimle eşit böl</button>
        </div>
      </fieldset>

      <p class="text-sm" [class.text-danger]="own() < 0" [class.text-ink-soft]="own() >= 0">
        @if (own() < 0) {
          Payların toplamı harcamayı {{ -own() | currency: 'TRY' : 'symbol-narrow' }} aşıyor.
        } @else {
          Sizin payınız: <span class="tabular-nums text-ink font-medium">{{ own() | currency: 'TRY' : 'symbol-narrow' }}</span>
        }
      </p>

      <div class="flex gap-2">
        <button type="submit" class="btn-primary" [disabled]="busy() || own() < 0">Bölüş ve iste</button>
        <button type="button" class="btn-secondary" (click)="cancel.emit()">Vazgeç</button>
      </div>
    </form>
  `,
})
export class SplitFormComponent extends BaseForm<SplitRequestBody> {
  /** The customer's open lira accounts, where the money can come back to. */
  readonly accounts = input.required<readonly Account[]>();

  protected readonly today = todayIsoDate();
  protected readonly maxPeople = MAX_PEOPLE;
  protected readonly printed = formatIban;

  private readonly fb = inject(FormBuilder).nonNullable;

  protected readonly form = this.fb.group({
    title: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(80)]],
    totalAmount: [null as number | null, [Validators.required, Validators.min(0.01)]],
    spentOn: [todayIsoDate() as string, [Validators.required]],
    receivingAccountId: [null as number | null, [Validators.required]],
    shares: this.fb.array([this.person()]),
  });

  private readonly values = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });
  protected readonly own = computed(() => {
    const value = this.values();
    return ownPart(value.totalAmount ?? 0, (value.shares ?? []).map((s) => s.amount ?? 0));
  });

  constructor() {
    super();
    // The first account is the obvious place for the money to come back to.
    effect(() => {
      const first = this.accounts()[0];
      if (first && this.form.controls.receivingAccountId.value === null) this.form.controls.receivingAccountId.setValue(first.id);
    });
  }

  protected addPerson(): void {
    this.form.controls.shares.push(this.person());
  }

  protected removePerson(index: number): void {
    this.form.controls.shares.removeAt(index);
  }

  protected splitEvenly(): void {
    const shares = this.form.controls.shares;
    const amounts = evenShares(this.form.controls.totalAmount.value ?? 0, shares.length);
    amounts.forEach((amount, i) => shares.at(i).controls.amount.setValue(amount));
  }

  protected groupIban(index: number): void {
    const control = this.form.controls.shares.at(index).controls.iban;
    control.setValue(formatIban(control.value.toUpperCase()), { emitEvent: false });
  }

  protected toRequest(): SplitRequestBody {
    const value = this.form.getRawValue();
    return {
      title: value.title.trim(),
      totalAmount: value.totalAmount ?? 0,
      spentOn: value.spentOn,
      receivingAccountId: value.receivingAccountId!,
      shares: value.shares.map((s) => ({
        name: s.name.trim(),
        iban: s.iban.trim() ? s.iban.replace(/\s+/g, '') : null,
        amount: s.amount ?? 0,
      })),
    };
  }

  private person() {
    return this.fb.group({
      name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
      iban: ['', [optionalIban]],
      amount: [null as number | null, [Validators.required, Validators.min(0.01)]],
    });
  }
}
