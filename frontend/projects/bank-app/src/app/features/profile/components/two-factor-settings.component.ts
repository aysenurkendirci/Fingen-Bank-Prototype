import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import QRCode from 'qrcode';

import { BasePage } from '../../../core/page/base-page';
import { ToastService } from '../../../core/ui/toast.service';
import { ProfileApi } from '../../../data-access/api/profile.api';
import { TwoFactorSetup, TwoFactorStatus } from '../../../data-access/models/banking.models';
import { NoticeComponent } from '../../../shared/ui/notice.component';

/**
 * Switching the second factor on and off. Setting it up shows a new key once, as a QR code and as text, and turns
 * it on only after a code from the app proves the key arrived. Turning it off needs the password and a code.
 * The section loads and saves on its own, so the profile page does not have to know about it.
 */
@Component({
  selector: 'app-two-factor-settings',
  imports: [DatePipe, FormsModule, NoticeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    @if (error(); as message) {
      <app-notice class="mb-4" [message]="message" />
    }

    @if (status(); as s) {
      @if (s.enabled) {
        <p class="text-sm text-ink-soft">
          <span class="badge bg-positive-tint text-positive mr-2">Açık</span>
          {{ s.enabledAt | date: 'd MMMM y' }} tarihinden beri girişte doğrulayıcı uygulamanızdaki kod da isteniyor.
        </p>

        <form class="mt-4 grid gap-3 sm:grid-cols-[1fr_10rem_auto] items-end" (ngSubmit)="disable()">
          <label class="field">
            <span class="field-label">Şifreniz</span>
            <input class="input" type="password" name="disablePassword" autocomplete="current-password" [(ngModel)]="password" />
          </label>
          <label class="field">
            <span class="field-label">Kod</span>
            <input class="input tabular-nums" name="disableCode" inputmode="numeric" maxlength="7" autocomplete="one-time-code" [(ngModel)]="code" />
          </label>
          <button type="submit" class="btn-secondary" [disabled]="busy() || !password || !code">Kapat</button>
        </form>
      } @else if (setup(); as key) {
        <ol class="space-y-4 text-sm">
          <li>
            <p>1. Google Authenticator, Microsoft Authenticator gibi bir doğrulayıcı uygulamayla bu kodu okutun.</p>
            <div class="mt-3 flex flex-wrap items-center gap-5">
              @if (qr(); as src) {
                <img [src]="src" alt="Doğrulayıcı uygulama için QR kod" class="size-44 rounded-control border border-line bg-white p-2" />
              }
              <div class="min-w-0">
                <p class="text-xs text-ink-faint">Okutamıyorsanız bu anahtarı elle girin:</p>
                <p class="mt-1 font-mono text-sm tracking-wider break-all" data-testid="two-factor-secret">{{ grouped() }}</p>
                <a class="btn-link mt-2 inline-block" [href]="key.setupUri">Bu cihazdaki uygulamada aç</a>
              </div>
            </div>
          </li>
          <li>
            <p>2. Uygulamanın gösterdiği 6 haneli kodu yazın.</p>
            <form class="mt-2 flex flex-wrap items-end gap-3" (ngSubmit)="confirm()">
              <input class="input w-40 tabular-nums tracking-[0.3em]" name="confirmCode" inputmode="numeric" maxlength="7" autocomplete="one-time-code" placeholder="000000" [(ngModel)]="code" />
              <button type="submit" class="btn-primary" [disabled]="busy() || !code">Doğrula ve aç</button>
              <button type="button" class="btn-link" (click)="cancelSetup()">Vazgeç</button>
            </form>
          </li>
        </ol>
        <p class="mt-4 text-xs text-ink-faint">Anahtar yalnızca şimdi gösterilir ve bankada şifreli saklanır. Telefonunuzu kaybederseniz şifrenizle girip bankamızla iletişime geçin.</p>
      } @else {
        <p class="text-sm text-ink-soft">
          Kapalı. Açarsanız her girişte şifrenizle birlikte telefonunuzdaki doğrulayıcı uygulamanın gösterdiği kod da istenir.
        </p>
        <button type="button" class="btn-primary mt-4" [disabled]="busy()" (click)="start()">İki adımlı doğrulamayı aç</button>
      }
    }
  `,
})
export class TwoFactorSettingsComponent extends BasePage {
  private readonly api = inject(ProfileApi);
  private readonly toast = inject(ToastService);

  protected readonly status = signal<TwoFactorStatus | null>(null);
  protected readonly setup = signal<TwoFactorSetup | null>(null);
  protected readonly qr = signal<string | null>(null);
  protected readonly grouped = computed(() => this.setup()?.secret.replace(/(.{4})(?=.)/g, '$1 ') ?? '');

  protected password = '';
  protected code = '';

  constructor() {
    super();
    this.load(this.api.twoFactor(), (status) => this.status.set(status), 'Doğrulama ayarı okunamadı.');
  }

  protected start(): void {
    this.act(this.api.startTwoFactor(), {
      fallback: 'Anahtar oluşturulamadı.',
      onDone: async (setup) => {
        this.setup.set(setup);
        this.code = '';
        // Drawn here, as an image of the address; the address itself never leaves the browser.
        this.qr.set(await QRCode.toDataURL(setup.setupUri, { margin: 1, width: 352, errorCorrectionLevel: 'M' }));
      },
    });
  }

  protected confirm(): void {
    this.act(this.api.confirmTwoFactor(this.code.replace(/\s/g, '')), {
      fallback: 'Kod doğrulanamadı.',
      onDone: (status) => {
        this.status.set(status);
        this.cancelSetup();
        this.toast.show('İki adımlı doğrulama açıldı.');
      },
      onError: () => (this.code = ''),
    });
  }

  protected disable(): void {
    this.act(this.api.disableTwoFactor(this.password, this.code.replace(/\s/g, '')), {
      fallback: 'İki adımlı doğrulama kapatılamadı.',
      onDone: (status) => {
        this.status.set(status);
        this.password = '';
        this.code = '';
        this.toast.show('İki adımlı doğrulama kapatıldı.');
      },
      onError: () => (this.code = ''),
    });
  }

  protected cancelSetup(): void {
    this.setup.set(null);
    this.qr.set(null);
    this.code = '';
  }
}
