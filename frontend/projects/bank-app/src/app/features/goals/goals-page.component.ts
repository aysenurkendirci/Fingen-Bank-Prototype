import { CurrencyPipe } from '@angular/common';
import { Component, booleanAttribute, computed, inject, input, signal } from '@angular/core';

import { BasePage } from '../../core/page/base-page';
import { ConfirmService } from '../../core/ui/confirm.service';
import { ToastService } from '../../core/ui/toast.service';
import { AccountsApi } from '../../data-access/api/banking.api';
import { GoalsApi } from '../../data-access/api/goals.api';
import { Account } from '../../data-access/models/banking.models';
import { AutoSaveRequest, Goal, GoalMoneyRequest, GoalRequest } from '../../data-access/models/savings.models';
import { IdempotentAttempt } from '../../shared/finance/idempotent-attempt';
import { MoneyComponent } from '../../shared/finance/money.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { NoticeComponent } from '../../shared/ui/notice.component';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { SkeletonComponent } from '../../shared/ui/skeleton.component';
import { AutoSaveFormComponent } from './components/auto-save-form.component';
import { GoalAction, GoalCardComponent } from './components/goal-card.component';
import { GoalFormComponent } from './components/goal-form.component';
import { GoalMoneyFormComponent } from './components/goal-money-form.component';

type Panel = { goalId: number; kind: Exclude<GoalAction, 'close'> };

/** The year a new year's goal is made for by default: next year once the year's last quarter has begun. */
function upcomingNewYear(today = new Date()): number {
  return today.getMonth() >= 9 ? today.getFullYear() + 1 : today.getFullYear();
}

/**
 * Container for savings goals. Used twice: all goals, and the new year area with the resolutions of one year.
 * Every change goes through here; the cards and forms below only show and collect.
 */
@Component({
  imports: [
    CurrencyPipe,
    MoneyComponent,
    EmptyStateComponent,
    NoticeComponent,
    PageHeaderComponent,
    SkeletonComponent,
    GoalCardComponent,
    GoalFormComponent,
    GoalMoneyFormComponent,
    AutoSaveFormComponent,
  ],
  templateUrl: './goals-page.component.html',
})
export class GoalsPageComponent extends BasePage {
  /** From the route's data: true in the new year area. */
  readonly newYear = input(false, { transform: booleanAttribute });

  private readonly api = inject(GoalsApi);
  private readonly accountsApi = inject(AccountsApi);
  private readonly dialog = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly moves = new IdempotentAttempt<{ goalId: number; direction: string; money: GoalMoneyRequest }>();

  protected readonly goals = signal<readonly Goal[]>([]);
  /** Lira accounts money can come from or go to; savings accounts move only through their goal. */
  protected readonly accounts = signal<readonly Account[]>([]);
  protected readonly creating = signal(false);
  protected readonly panel = signal<Panel | null>(null);
  protected readonly selectedYear = signal(upcomingNewYear());

  protected readonly years = computed(() => {
    const years = new Set([new Date().getFullYear(), upcomingNewYear()]);
    for (const goal of this.goals()) if (goal.newYearOf) years.add(goal.newYearOf);
    return [...years].sort();
  });

  protected readonly visibleGoals = computed(() =>
    this.newYear() ? this.goals().filter((g) => g.newYearOf === this.selectedYear()) : this.goals(),
  );

  protected readonly yearSummary = computed(() => {
    const goals = this.visibleGoals();
    if (goals.length === 0) return null;
    const saved = goals.reduce((sum, g) => sum + g.savedAmount, 0);
    const target = goals.reduce((sum, g) => sum + g.targetAmount, 0);
    return { count: goals.length, saved, target, percent: Math.min(100, Math.floor((saved / target) * 100)) };
  });

  constructor() {
    super();
    this.load(this.api.list(), (goals) => this.goals.set(goals), 'Hedefler yüklenemedi.');
    this.loadAccounts();
  }

  protected startCreating(): void {
    this.panel.set(null);
    this.creating.set(true);
  }

  protected async open(goal: Goal, action: GoalAction): Promise<void> {
    this.creating.set(false);
    if (action !== 'close') {
      this.panel.set({ goalId: goal.id, kind: action });
      return;
    }

    const yes = await this.dialog.ask({
      title: `"${goal.name}" kapatılsın mı?`,
      message: goal.savedAmount > 0 ? 'Önce kumbaradaki parayı hesabınıza aktarmanız gerekir.' : 'Hedef ve boş kumbarası kapatılır.',
      confirmLabel: 'Kapat',
      danger: true,
    });
    if (!yes) return;

    this.act(this.api.close(goal.id), {
      fallback: 'Hedef kapatılamadı.',
      onDone: () => {
        this.goals.update((goals) => goals.filter((g) => g.id !== goal.id));
        this.toast.show('Hedef kapatıldı.');
      },
    });
  }

  protected create(request: GoalRequest): void {
    this.act(this.api.create(request), {
      fallback: 'Hedef oluşturulamadı.',
      onDone: (goal) => {
        this.goals.update((goals) => [...goals, goal]);
        this.creating.set(false);
        if (goal.newYearOf) this.selectedYear.set(goal.newYearOf);
        this.toast.show('Hedefiniz ve kumbarası oluşturuldu.');
      },
    });
  }

  protected update(goal: Goal, request: GoalRequest): void {
    this.act(this.api.update(goal.id, request), { fallback: 'Hedef kaydedilemedi.', onDone: (updated) => this.replace(updated, 'Hedef güncellendi.') });
  }

  /** A retry after a failure reuses its key, so the money moves once however often the button is pressed. */
  protected move(goal: Goal, direction: 'deposit' | 'withdraw', money: GoalMoneyRequest): void {
    const key = this.moves.keyFor({ goalId: goal.id, direction, money });
    const call = direction === 'deposit' ? this.api.deposit(goal.id, money, key) : this.api.withdraw(goal.id, money, key);

    this.act(call, {
      fallback: 'Para aktarılamadı.',
      onDone: (updated) => {
        this.moves.finish();
        this.replace(updated, direction === 'deposit' ? 'Para kumbaraya eklendi.' : 'Para hesabınıza aktarıldı.');
        this.loadAccounts();
      },
    });
  }

  protected saveRule(goal: Goal, rule: AutoSaveRequest): void {
    this.act(this.api.setAutoSave(goal.id, rule), {
      fallback: 'Kural kaydedilemedi.',
      onDone: (updated) => this.replace(updated, 'Otomatik birikim açıldı.'),
    });
  }

  protected removeRule(goal: Goal): void {
    this.act(this.api.clearAutoSave(goal.id), {
      fallback: 'Kural kaldırılamadı.',
      onDone: (updated) => this.replace(updated, 'Otomatik birikim kapatıldı.'),
    });
  }

  private replace(updated: Goal, message: string): void {
    this.goals.update((goals) => goals.map((g) => (g.id === updated.id ? updated : g)));
    this.panel.set(null);
    this.toast.show(message);
  }

  private loadAccounts(): void {
    this.accountsApi.list().subscribe({
      next: (accounts) =>
        this.accounts.set(accounts.filter((a) => a.status === 'Active' && a.currencyCode === 'TRY' && a.accountType !== 'Savings')),
      error: () => undefined,
    });
  }
}
