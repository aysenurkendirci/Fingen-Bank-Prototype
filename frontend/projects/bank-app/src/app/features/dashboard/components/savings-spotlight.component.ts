import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';

import { Goal } from '../../../data-access/models/savings.models';
import { MoneyComponent } from '../../../shared/finance/money.component';

/**
 * The top of the home screen in the young mode: what is put aside, and the goals and wishes closest to done.
 * Without any goal it invites the first one instead.
 */
@Component({
  selector: 'app-savings-spotlight',
  imports: [CurrencyPipe, RouterLink, MoneyComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'panel block bg-brand-tint border-brand/30' },
  template: `
    @if (goals().length === 0) {
      <p class="section-title">İlk kumbaranı aç</p>
      <p class="mt-1 text-sm text-ink-soft">Araba, telefon, bir yaz tatili: ne istediğini yaz, her biri için ayrı bir kumbara açılsın.</p>
      <a routerLink="/dashboard/new-year-goals" class="btn-primary mt-4">Dilek listeme ekle</a>
    } @else {
      <div class="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p class="text-sm text-ink-soft">Kumbaralarında biriken</p>
          <p class="text-3xl sm:text-4xl font-semibold tracking-[-0.03em] mt-1"><app-money display countUp [amount]="saved()" /></p>
          <p class="text-sm text-ink-soft">{{ goals().length }} hedefin toplamı {{ target() | currency: 'TRY' : 'symbol-narrow' : '1.0-0' }}</p>
        </div>
        <a routerLink="/dashboard/goals" class="btn-primary">Kumbarana para at</a>
      </div>

      <ul class="mt-5 grid gap-3 sm:grid-cols-3">
        @for (goal of closest(); track goal.id) {
          <li class="rounded-tile bg-surface p-4">
            <p class="text-sm font-medium truncate">{{ goal.name }}</p>
            <p class="text-xs text-ink-faint mt-0.5">%{{ goal.percent }} tamam</p>
            <div class="mt-2 h-1.5 rounded-full bg-muted overflow-hidden" role="progressbar" [attr.aria-valuenow]="goal.percent" aria-valuemin="0" aria-valuemax="100" [attr.aria-label]="goal.name">
              <div class="h-full rounded-full bg-brand" [style.width.%]="goal.percent"></div>
            </div>
          </li>
        }
      </ul>
    }
  `,
})
export class SavingsSpotlightComponent {
  readonly goals = input.required<readonly Goal[]>();

  protected readonly saved = computed(() => this.goals().reduce((sum, g) => sum + g.savedAmount, 0));
  protected readonly target = computed(() => this.goals().reduce((sum, g) => sum + g.targetAmount, 0));

  /** The three nearest to their target: the ones a little more would finish. */
  protected readonly closest = computed(() =>
    this.goals()
      .map((g) => ({ id: g.id, name: g.name, percent: Math.min(100, Math.floor((g.savedAmount / g.targetAmount) * 100)) }))
      .sort((a, b) => b.percent - a.percent)
      .slice(0, 3),
  );
}
