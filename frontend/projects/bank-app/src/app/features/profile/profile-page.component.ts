import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { SessionService } from '../../core/auth/session.service';
import { BasePage } from '../../core/page/base-page';
import { ConfirmService } from '../../core/ui/confirm.service';
import { ToastService } from '../../core/ui/toast.service';
import { ProfileApi } from '../../data-access/api/profile.api';
import { ContactDetails, PasswordChange, Profile } from '../../data-access/models/banking.models';
import { NoticeComponent } from '../../shared/ui/notice.component';
import { SkeletonComponent } from '../../shared/ui/skeleton.component';
import { AppearanceSettingsComponent } from './components/appearance-settings.component';
import { ContactFormComponent } from './components/contact-form.component';
import { PasswordFormComponent } from './components/password-form.component';

/** The customer's own record and the KVKK rights over it: seeing it, taking a copy and having it erased. */
@Component({
  imports: [DatePipe, FormsModule, NoticeComponent, SkeletonComponent, AppearanceSettingsComponent, ContactFormComponent, PasswordFormComponent],
  templateUrl: './profile-page.component.html',
})
export class ProfilePageComponent extends BasePage {
  private readonly api = inject(ProfileApi);
  private readonly session = inject(SessionService);
  private readonly dialog = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly passwordForm = viewChild(PasswordFormComponent);

  protected readonly profile = signal<Profile | null>(null);
  protected readonly erasing = signal(false);
  protected readonly initials = computed(() => {
    const profile = this.profile();
    return profile ? `${profile.firstName.charAt(0)}${profile.lastName.charAt(0)}`.toLocaleUpperCase('tr-TR') : '';
  });

  protected readonly sections = [
    { id: 'kisisel', label: 'Kişisel bilgiler' },
    { id: 'iletisim', label: 'İletişim' },
    { id: 'guvenlik', label: 'Şifre' },
    { id: 'gorunum', label: 'Tema' },
    { id: 'verilerim', label: 'Verilerim' },
    { id: 'hesabi-kapat', label: 'Hesabımı sil' },
  ] as const;
  protected erasePassword = '';

  constructor() {
    super();
    this.load(this.api.get(), (profile) => this.profile.set(profile), 'Bilgileriniz yüklenemedi.');
  }

  /** Jumps to a section; smoothly unless the system asks for reduced motion. */
  protected scrollTo(id: string): void {
    const still = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
    document.getElementById(id)?.scrollIntoView({ behavior: still ? 'auto' : 'smooth', block: 'start' });
  }

  protected saveContact(details: ContactDetails): void {
    this.act(this.api.updateContact(details), {
      fallback: 'Bilgileriniz kaydedilemedi.',
      onDone: (profile) => {
        this.profile.set(profile);
        this.toast.show('İletişim bilgileriniz güncellendi.');
      },
    });
  }

  protected changePassword(change: PasswordChange): void {
    this.act(this.api.changePassword(change), {
      fallback: 'Şifreniz değiştirilemedi.',
      onDone: (response) => {
        this.session.start(response);
        this.passwordForm()?.clear();
        this.toast.show('Şifreniz değiştirildi.');
      },
    });
  }

  protected downloadData(): void {
    this.act(this.api.exportPersonalData(), {
      fallback: 'Verileriniz hazırlanamadı.',
      onDone: (file) => {
        const url = URL.createObjectURL(file);
        const link = document.createElement('a');
        link.href = url;
        link.download = 'kisisel-verilerim.json';
        link.click();
        URL.revokeObjectURL(url);
      },
    });
  }

  protected async erase(): Promise<void> {
    const yes = await this.dialog.ask({
      title: 'Hesabınız kalıcı olarak silinecek',
      message: 'Hesaplarınız kapatılacak ve kişisel bilgileriniz silinecek. Bu işlem geri alınamaz.',
      confirmLabel: 'Kalıcı olarak sil',
      danger: true,
    });
    if (!yes) return;

    this.act(this.api.erase(this.erasePassword), {
      fallback: 'Hesabınız silinemedi.',
      onDone: () => {
        this.session.end();
        this.router.navigate(['/auth/login'], { queryParams: { erased: '1' } });
      },
    });
  }
}
