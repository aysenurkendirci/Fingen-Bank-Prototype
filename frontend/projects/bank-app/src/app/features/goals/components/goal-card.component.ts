import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

import { Goal } from '../../../data-access/models/savings.models';
import { IbanCopyComponent } from '../../../shared/finance/iban-copy.component';
import { MoneyComponent } from '../../../shared/finance/money.component';
import { categoryOf, horizonYears } from '../goal-categories';

export type GoalAction = 'deposit' | 'withdraw' | 'auto-save' | 'edit' | 'close';

/**
 * One goal, or one wish on the list: its photo or the icon of its kind, what exactly is wanted, how far along it is,
 * what it still needs and whether it fills itself. Actions go up to the page.
 */
@Component({
  selector: 'app-goal-card',
  imports: [CurrencyPipe, DatePipe, IbanCopyComponent, MoneyComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'panel block overflow-hidden' },
  template: `
    @if (photoUrl(); as src) {
      <img [src]="src" [alt]="goal().name" class="-mx-5 -mt-5 mb-4 block w-[calc(100%+2.5rem)] max-w-none aspect-[16/9] object-cover" />
    } @else if (category(); as kind) {
      <div class="-mx-5 -mt-5 mb-4 aspect-[16/9] max-h-32 w-[calc(100%+2.5rem)] bg-brand-tint grid place-items-center" aria-hidden="true">
        <span class="material-symbols-outlined text-[40px] text-brand">{{ kind.icon }}</span>
      </div>
    }

    <div class="flex flex-wrap items-start justify-between gap-3">
      <div class="min-w-0">
        @if (category(); as kind) {
          <p class="text-xs text-ink-faint mb-0.5">{{ kind.label }}</p>
        }
        <h3 class="section-title">{{ goal().name }}</h3>
        @if (goal().details; as details) {
          <p class="text-sm text-ink-soft mt-1 whitespace-pre-line break-words">{{ details }}</p>
        }
        <app-iban-copy class="block mt-0.5" [iban]="goal().iban" />
      </div>
      @if (reached()) {
        <span class="badge bg-positive-tint text-positive">Hedefe ulaşıldı</span>
      }
    </div>

    <p class="mt-4 text-2xl font-semibold tracking-[-0.02em]"><app-money display [amount]="goal().savedAmount" /></p>
    <p class="text-sm text-ink-soft">Hedef {{ goal().targetAmount | currency: 'TRY' : 'symbol-narrow' : '1.0-0' }}, %{{ percent() }} tamamlandı</p>

    <div class="mt-3 h-2 rounded-full bg-muted overflow-hidden" role="progressbar" [attr.aria-valuenow]="percent()" aria-valuemin="0" aria-valuemax="100" [attr.aria-label]="goal().name + ' ilerlemesi'">
      <div class="grow-x h-full rounded-full bg-brand transition-[width] duration-500 ease-enter" [style.width.%]="percent()"></div>
    </div>

    <dl class="mt-4 space-y-1 text-sm">
      @if (horizon(); as years) {
        <div class="flex justify-between gap-4"><dt class="text-ink-soft">Süre</dt><dd>{{ years }} yıllık hedef, {{ goal().targetDate | date: 'y' }} sonuna kadar</dd></div>
      } @else if (goal().targetDate; as date) {
        <div class="flex justify-between gap-4"><dt class="text-ink-soft">Hedef tarihi</dt><dd>{{ date | date: 'd MMMM y' }}</dd></div>
      }
      @if (goal().monthlyNeeded; as monthly) {
        <div class="flex justify-between gap-4"><dt class="text-ink-soft">Ayda gereken</dt><dd class="tabular-nums">{{ monthly | currency: 'TRY' : 'symbol-narrow' }}</dd></div>
      }
      <div class="flex justify-between gap-4">
        <dt class="text-ink-soft">Otomatik birikim</dt>
        <dd class="text-right">
          @if (goal().autoSave; as rule) {
            @if (rule.mode === 'Percent') {
              Gelen paradan %{{ rule.value }}
            } @else {
              Her seferinde {{ rule.value | currency: 'TRY' : 'symbol-narrow' }}
            }
            @if (rule.minIncoming > 0) {
              <span class="block text-xs text-ink-faint">{{ rule.minIncoming | currency: 'TRY' : 'symbol-narrow' : '1.0-0' }} ve üzeri girişlerde</span>
            }
          } @else {
            <span class="text-ink-faint">Kapalı</span>
          }
        </dd>
      </div>
    </dl>

    <div class="mt-5 flex flex-wrap gap-2">
      <button type="button" class="btn-primary" (click)="act.emit('deposit')">Para ekle</button>
      <button type="button" class="btn-secondary" [disabled]="goal().savedAmount === 0" (click)="act.emit('withdraw')">Hesaba aktar</button>
      <button type="button" class="btn-secondary" (click)="act.emit('auto-save')">Otomatik birikim</button>
      <button type="button" class="btn-link ml-auto self-center" (click)="act.emit('edit')">Düzenle</button>
      <button type="button" class="btn-link self-center !text-danger" (click)="act.emit('close')">Kapat</button>
    </div>
  `,
})
export class GoalCardComponent {
  readonly goal = input.required<Goal>();
  /** Loaded by the page; null while loading or without a photo. */
  readonly photoUrl = input<string | null>(null);
  readonly act = output<GoalAction>();

  protected readonly category = computed(() => categoryOf(this.goal().category));
  protected readonly horizon = computed(() => horizonYears(this.goal()));

  protected readonly percent = computed(() => {
    const { savedAmount, targetAmount } = this.goal();
    return Math.min(100, Math.floor((savedAmount / targetAmount) * 100));
  });

  protected readonly reached = computed(() => this.goal().savedAmount >= this.goal().targetAmount);
}
