import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { todayIsoDate } from '../../../core/util/dates';
import { shrinkImage } from '../../../core/util/images';
import { Goal, GoalCategory, GoalRequest } from '../../../data-access/models/savings.models';
import { BaseForm } from '../../../shared/forms/base-form';
import { GOAL_CATEGORIES, categoryOf, horizonEnd, horizonYears } from '../goal-categories';

/** What the form hands to the page: the goal, and what to do with its photo. */
export interface GoalFormResult {
  goal: GoalRequest;
  /** A new photo to upload, null to remove the current one, undefined to leave it as it is. */
  photo?: Blob | null;
}

/**
 * Add or edit a goal. In the new year area the goal is a wish from the start: what kind of thing it is, the details,
 * over how many years, and a photo if the customer likes. Elsewhere the same fields are there, the year optional.
 */
@Component({
  selector: 'app-goal-form',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'panel block' },
  template: `
    <form class="grid gap-5 sm:grid-cols-2" [formGroup]="form" (ngSubmit)="submit()">
      <fieldset class="sm:col-span-2">
        <legend class="field-label mb-2">Ne istiyorsunuz?</legend>
        <div class="flex flex-wrap gap-2">
          @for (option of categories; track option.value) {
            <button
              type="button"
              class="swatch gap-1.5"
              [attr.aria-pressed]="category() === option.value"
              (click)="pickCategory(option.value)"
            >
              <span class="material-symbols-outlined text-[18px]" aria-hidden="true">{{ option.icon }}</span>
              {{ option.label }}
            </button>
          }
        </div>
      </fieldset>

      <label class="field sm:col-span-2">
        <span class="field-label">Adı</span>
        <input class="input" formControlName="name" maxlength="80" [placeholder]="namePlaceholder()" />
        @if (form.controls.name.touched && form.controls.name.invalid) {
          <span class="field-error">2 ile 80 karakter arasında bir ad girin.</span>
        }
      </label>

      <label class="field sm:col-span-2">
        <span class="field-label">Ayrıntılar (isteğe bağlı)</span>
        <textarea class="input !h-auto py-2 min-h-20 resize-y" formControlName="details" maxlength="300" rows="3" [placeholder]="detailsHint()"></textarea>
        <span class="text-xs text-ink-faint text-right tabular-nums">{{ form.controls.details.value.length }}/300</span>
      </label>

      <label class="field">
        <span class="field-label">Hedef tutar</span>
        <input class="input tabular-nums" type="number" min="1" step="100" formControlName="targetAmount" />
        @if (form.controls.targetAmount.touched && form.controls.targetAmount.invalid) {
          <span class="field-error">Sıfırdan büyük bir tutar girin.</span>
        }
      </label>

      @if (isResolution()) {
        <label class="field">
          <span class="field-label">Kaç yıllık hedef?</span>
          <select class="input" formControlName="horizon">
            @for (n of horizons; track n) {
              <option [ngValue]="n">{{ n }} yıl, {{ form.controls.newYearOf.value + n - 1 }} sonuna kadar</option>
            }
          </select>
        </label>
      } @else {
        <label class="field">
          <span class="field-label">Hedef tarihi (isteğe bağlı)</span>
          <input class="input" type="date" [min]="tomorrow" formControlName="targetDate" />
        </label>
      }

      <label class="flex flex-wrap items-center gap-3 sm:col-span-2">
        <input type="checkbox" role="switch" class="switch" formControlName="isResolution" />
        <span class="text-sm">İstek listeme ekle</span>
        @if (isResolution()) {
          <select class="input h-9" formControlName="newYearOf" aria-label="Hangi yılın listesi">
            @for (year of years; track year) {
              <option [ngValue]="year">{{ year }}</option>
            }
          </select>
        }
      </label>

      <div class="sm:col-span-2">
        <span class="field-label">Fotoğraf (isteğe bağlı)</span>
        <div class="mt-2 flex flex-wrap items-center gap-4">
          @if (preview(); as src) {
            <img [src]="src" alt="Seçilen fotoğraf" class="h-20 w-32 rounded-control object-cover border border-line" />
          }
          <label class="btn-secondary cursor-pointer">
            <input type="file" class="sr-only" name="photo" accept="image/jpeg,image/png,image/webp" (change)="pickPhoto($event)" />
            {{ preview() ? 'Fotoğrafı değiştir' : 'Fotoğraf seç' }}
          </label>
          @if (preview()) {
            <button type="button" class="btn-link" (click)="removePhoto()">Fotoğrafı kaldır</button>
          }
        </div>
        @if (photoError(); as message) {
          <p class="field-error mt-1">{{ message }}</p>
        } @else {
          <p class="text-xs text-ink-faint mt-1">Fotoğraf küçültülerek kaydedilir; konum gibi ek bilgiler silinir.</p>
        }
      </div>

      <div class="flex gap-2 sm:col-span-2">
        <button type="submit" class="btn-primary" [disabled]="busy() || shrinking()">{{ goal() ? 'Kaydet' : 'Hedefi oluştur' }}</button>
        <button type="button" class="btn-secondary" (click)="cancel.emit()">Vazgeç</button>
      </div>
    </form>
  `,
})
export class GoalFormComponent extends BaseForm<GoalFormResult> {
  readonly goal = input<Goal | null>(null);
  /** The goal's current photo, shown until a new one is picked. */
  readonly photoUrl = input<string | null>(null);
  /** Preset year when the form opens in the new year area. */
  readonly resolutionYear = input<number | null>(null);

  protected readonly categories = GOAL_CATEGORIES;
  // The API accepts a wish list from this year up to five years ahead.
  protected readonly years = [0, 1, 2, 3, 4, 5].map((i) => new Date().getFullYear() + i);
  protected readonly horizons = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
  protected readonly tomorrow = (() => {
    const d = new Date();
    d.setDate(d.getDate() + 1);
    return d.toISOString().slice(0, 10);
  })();

  protected readonly form = inject(FormBuilder).nonNullable.group({
    category: [null as GoalCategory | null],
    name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(80)]],
    details: ['', [Validators.maxLength(300)]],
    targetAmount: [null as number | null, [Validators.required, Validators.min(1)]],
    targetDate: [''],
    isResolution: [false],
    newYearOf: [this.years[1]],
    horizon: [1],
  });

  protected readonly category = toSignal(this.form.controls.category.valueChanges, { initialValue: null });
  protected readonly isResolution = toSignal(this.form.controls.isResolution.valueChanges, { initialValue: false });
  protected readonly namePlaceholder = computed(() => {
    const option = categoryOf(this.category());
    return option && option.value !== 'other' ? `Örn. ${option.label.toLowerCase()} hedefim` : 'Örn. Araba, ev peşinatı, tatil';
  });
  protected readonly detailsHint = computed(() => categoryOf(this.category())?.hint ?? 'Model, renk, nereye: ne istediğinizi yazın');

  /** undefined: keep the current photo; null: remove it; a blob: upload it. */
  private readonly newPhoto = signal<Blob | null | undefined>(undefined);
  private readonly newPhotoUrl = signal<string | null>(null);
  protected readonly shrinking = signal(false);
  protected readonly photoError = signal<string | null>(null);
  protected readonly preview = computed(() => {
    const photo = this.newPhoto();
    if (photo === null) return null;
    return photo === undefined ? this.photoUrl() : this.newPhotoUrl();
  });

  constructor() {
    super();
    inject(DestroyRef).onDestroy(() => this.revokePreview());

    effect(() => {
      const goal = this.goal();
      const year = this.resolutionYear();
      this.form.reset({
        category: goal?.category ?? null,
        name: goal?.name ?? '',
        details: goal?.details ?? '',
        targetAmount: goal?.targetAmount ?? null,
        targetDate: goal?.targetDate ?? '',
        isResolution: (goal?.newYearOf ?? year) !== null,
        newYearOf: goal?.newYearOf ?? year ?? this.years[1],
        horizon: (goal && horizonYears(goal)) ?? 1,
      });
    });
  }

  protected pickCategory(value: GoalCategory): void {
    this.form.controls.category.setValue(this.form.controls.category.value === value ? null : value);
  }

  protected async pickPhoto(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;

    this.photoError.set(null);
    this.shrinking.set(true);
    try {
      const photo = await shrinkImage(file);
      this.revokePreview();
      this.newPhoto.set(photo);
      this.newPhotoUrl.set(URL.createObjectURL(photo));
    } catch {
      this.photoError.set('Bu dosya okunamadı. JPEG, PNG ya da WebP bir fotoğraf seçin.');
    } finally {
      this.shrinking.set(false);
    }
  }

  protected removePhoto(): void {
    this.revokePreview();
    // Removing a photo that was only picked, not saved, simply goes back to none.
    this.newPhoto.set(this.photoUrl() ? null : undefined);
  }

  protected toRequest(): GoalFormResult {
    const value = this.form.getRawValue();
    const details = value.details.trim();
    const targetDate = value.isResolution
      ? horizonEnd(value.newYearOf, value.horizon)
      : value.targetDate && value.targetDate > todayIsoDate()
        ? value.targetDate
        : null;

    return {
      goal: {
        name: value.name.trim(),
        targetAmount: value.targetAmount ?? 0,
        targetDate,
        newYearOf: value.isResolution ? value.newYearOf : null,
        category: value.category,
        details: details || null,
      },
      photo: this.newPhoto(),
    };
  }

  private revokePreview(): void {
    const url = this.newPhotoUrl();
    if (url) URL.revokeObjectURL(url);
    this.newPhotoUrl.set(null);
  }
}
