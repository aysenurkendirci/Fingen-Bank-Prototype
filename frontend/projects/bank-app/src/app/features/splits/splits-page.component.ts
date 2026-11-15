import { CurrencyPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { Observable, map, of, switchMap, tap } from 'rxjs';

import { BasePage } from '../../core/page/base-page';
import { ConfirmService } from '../../core/ui/confirm.service';
import { ToastService } from '../../core/ui/toast.service';
import { AccountsApi, TransfersApi } from '../../data-access/api/banking.api';
import { SplitsApi } from '../../data-access/api/splits.api';
import { Account } from '../../data-access/models/banking.models';
import { ShareAsked, Split, SplitRequestBody } from '../../data-access/models/split.models';
import { IdempotentAttempt } from '../../shared/finance/idempotent-attempt';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { NoticeComponent } from '../../shared/ui/notice.component';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { SkeletonComponent } from '../../shared/ui/skeleton.component';
import { ShareRequestAction, ShareRequestCardComponent } from './components/share-request-card.component';
import { SplitAction, SplitCardComponent } from './components/split-card.component';
import { SplitFormComponent } from './components/split-form.component';

type Tab = 'mine' | 'asked';

/**
 * Splitting expenses with friends: the ones the customer paid and shared out, and the shares others ask of them.
 * Paying a share is an ordinary transfer to the payer; the share is then settled with that transfer.
 */
@Component({
  imports: [
    CurrencyPipe,
    EmptyStateComponent,
    NoticeComponent,
    PageHeaderComponent,
    SkeletonComponent,
    ShareRequestCardComponent,
    SplitCardComponent,
    SplitFormComponent,
  ],
  template: `
    <app-page-header title="Harcama bölme" subtitle="Ödediğiniz bir harcamayı arkadaşlarınızla bölüşün, kimin ne kadar borçlu olduğunu buradan izleyin.">
      <button type="button" class="btn-primary" (click)="startCreating()">Harcama bölüş</button>
    </app-page-header>

    <div class="mb-6 flex flex-wrap items-center gap-2" role="tablist" aria-label="Görünüm">
      <button type="button" role="tab" class="swatch" [attr.aria-selected]="tab() === 'mine'" [attr.aria-pressed]="tab() === 'mine'" (click)="tab.set('mine')">
        Bölüştüklerim
      </button>
      <button type="button" role="tab" class="swatch" [attr.aria-selected]="tab() === 'asked'" [attr.aria-pressed]="tab() === 'asked'" (click)="tab.set('asked')">
        Benden istenenler
        @if (openAsked() > 0) {
          <span class="unread-badge">{{ openAsked() }}</span>
        }
      </button>
    </div>

    @if (error(); as message) {
      <app-notice class="mb-4" [message]="message" />
    }

    @if (creating()) {
      <app-split-form class="mb-6" [accounts]="accounts()" [busy]="busy()" (save)="create($event)" (cancel)="creating.set(false)" />
    }

    @if (tab() === 'mine') {
      @if (summary(); as s) {
        <section class="panel mb-6 flex flex-wrap gap-x-10 gap-y-3">
          <div>
            <p class="text-sm text-ink-soft">Size borçlu olunan</p>
            <p class="text-2xl font-semibold tracking-[-0.02em] tabular-nums">{{ s.owed | currency: 'TRY' : 'symbol-narrow' }}</p>
          </div>
          <div>
            <p class="text-sm text-ink-soft">Geri gelen</p>
            <p class="text-2xl font-semibold tracking-[-0.02em] tabular-nums">{{ s.collected | currency: 'TRY' : 'symbol-narrow' }}</p>
          </div>
        </section>
      }

      @if (loading() && splits().length === 0) {
        <app-skeleton />
      } @else if (splits().length === 0 && !creating()) {
        <app-empty-state>Henüz bölüştüğünüz bir harcama yok. Bir akşam yemeğini ya da ev kirasını "Harcama bölüş" ile paylaştırın.</app-empty-state>
      } @else {
        <div class="grid gap-4 lg:grid-cols-2 items-start">
          @for (split of splits(); track split.id) {
            <app-split-card [split]="split" (act)="onSplit(split, $event)" />
          }
        </div>
      }
    } @else {
      @if (asked().length === 0) {
        <app-empty-state>Kimse sizden bir pay istemedi. Bir arkadaşınız harcamasını sizinle bölüştüğünde burada göreceksiniz.</app-empty-state>
      } @else {
        <div class="grid gap-4 lg:grid-cols-2 items-start">
          @for (request of asked(); track request.shareId) {
            <app-share-request-card [request]="request" [accounts]="accounts()" [busy]="busy()" (act)="onRequest(request, $event)" />
          }
        </div>
      }
    }
  `,
})
export class SplitsPageComponent extends BasePage {
  private readonly api = inject(SplitsApi);
  private readonly accountsApi = inject(AccountsApi);
  private readonly transfers = inject(TransfersApi);
  private readonly dialog = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly payments = new IdempotentAttempt<{ shareId: number; sourceAccountId: number }>();
  /** Transfers that went through but whose share is not settled yet, e.g. after a dropped connection. */
  private readonly sent = new Map<number, number>();

  protected readonly tab = signal<Tab>('mine');
  protected readonly creating = signal(false);
  protected readonly splits = signal<readonly Split[]>([]);
  protected readonly asked = signal<readonly ShareAsked[]>([]);
  protected readonly accounts = signal<readonly Account[]>([]);

  protected readonly openAsked = computed(() => this.asked().filter((r) => r.status === 'Open').length);
  protected readonly summary = computed(() => {
    const open = this.splits().filter((s) => s.status === 'Open');
    if (open.length === 0) return null;
    const owed = open.flatMap((s) => s.shares).filter((x) => x.status === 'Open').reduce((sum, x) => sum + x.amount, 0);
    return { owed, collected: open.reduce((sum, s) => sum + s.collected, 0) };
  });

  constructor() {
    super();
    this.load(this.api.list(), (splits) => this.splits.set(splits), 'Harcamalar yüklenemedi.');
    this.api.requests().subscribe({
      next: (asked) => {
        this.asked.set(asked);
        // Someone who came here because of a request sees it first.
        if (asked.some((r) => r.status === 'Open')) this.tab.set('asked');
      },
      error: () => undefined,
    });
    this.loadAccounts();
  }

  protected startCreating(): void {
    this.tab.set('mine');
    this.creating.set(true);
  }

  protected create(request: SplitRequestBody): void {
    this.act(this.api.create(request), {
      fallback: 'Harcama bölüşülemedi.',
      onDone: (split) => {
        this.splits.update((splits) => [split, ...splits]);
        this.creating.set(false);
        this.toast.show(split.shares.some((s) => s.inApp) ? 'Harcama bölüşüldü, arkadaşlarınıza bildirim gitti.' : 'Harcama bölüşüldü.');
      },
    });
  }

  protected async onSplit(split: Split, action: SplitAction): Promise<void> {
    if (action.kind === 'cash') {
      this.act(this.api.markPaid(split.id, action.share.id), {
        fallback: 'Pay kapatılamadı.',
        onDone: (updated) => this.replaceSplit(updated, `${action.share.name} ödendi olarak işaretlendi.`),
      });
      return;
    }

    const yes = await this.dialog.ask({
      title: `"${split.title}" için açık istekler geri çekilsin mi?`,
      message: 'Henüz ödenmemiş paylar kapanır ve kişilere bildirilir. Ödenmiş paylar olduğu gibi kalır.',
      confirmLabel: 'Geri çek',
      danger: true,
    });
    if (!yes) return;

    this.act(this.api.cancel(split.id).pipe(switchMap(() => this.api.list())), {
      fallback: 'İstekler geri çekilemedi.',
      onDone: (splits) => {
        this.splits.set(splits);
        this.toast.show('Açık istekler geri çekildi.');
      },
    });
  }

  protected async onRequest(request: ShareAsked, action: ShareRequestAction): Promise<void> {
    if (action.kind === 'decline') {
      const yes = await this.dialog.ask({
        title: 'Bu pay isteği reddedilsin mi?',
        message: `${request.ownerName} bilgilendirilir.`,
        confirmLabel: 'Reddet',
        danger: true,
      });
      if (!yes) return;
      this.act(this.api.decline(request.shareId), {
        fallback: 'İstek reddedilemedi.',
        onDone: (updated) => this.replaceRequest(updated, 'İstek reddedildi.'),
      });
      return;
    }

    const yes = await this.dialog.ask({
      title: 'Payınız gönderilsin mi?',
      message: `${request.ownerName} adlı alıcıya "${request.title}" için ${request.amount.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} TL gönderilecek.`,
      confirmLabel: 'Gönder',
    });
    if (!yes) return;

    this.act(this.pay(request, action.sourceAccountId), {
      fallback: 'Payınız gönderilemedi.',
      onDone: (updated) => {
        this.payments.finish();
        this.replaceRequest(updated, 'Payınız gönderildi.');
        this.loadAccounts();
      },
    });
  }

  /**
   * The transfer first, with a key that a retry reuses, then the settlement. If only the settlement failed, a
   * retry settles with the transfer that already went through instead of sending the money again.
   */
  private pay(request: ShareAsked, sourceAccountId: number): Observable<ShareAsked> {
    const already = this.sent.get(request.shareId);
    const transferId$ = already
      ? of(already)
      : this.transfers
          .send(
            {
              sourceAccountId,
              targetIban: request.receivingIban,
              recipientName: request.ownerName,
              amount: request.amount,
              description: `Harcama payı: ${request.title}`.slice(0, 140),
            },
            this.payments.keyFor({ shareId: request.shareId, sourceAccountId }),
          )
          .pipe(
            map((receipt) => receipt.id),
            tap((id) => this.sent.set(request.shareId, id)),
          );

    return transferId$.pipe(
      switchMap((transferId) => this.api.settle(request.shareId, transferId)),
      tap(() => this.sent.delete(request.shareId)),
    );
  }

  private replaceSplit(updated: Split, message: string): void {
    this.splits.update((splits) => splits.map((s) => (s.id === updated.id ? updated : s)));
    this.toast.show(message);
  }

  private replaceRequest(updated: ShareAsked, message: string): void {
    this.asked.update((asked) => asked.map((r) => (r.shareId === updated.shareId ? updated : r)));
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
