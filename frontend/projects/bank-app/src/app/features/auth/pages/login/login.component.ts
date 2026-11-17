import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { SessionService } from '../../../../core/auth/session.service';
import { BasePage } from '../../../../core/page/base-page';
import { AuthApi } from '../../../../data-access/api/auth.api';
import { AuthShellComponent } from '../../../../layout/auth/auth-shell.component';
import { NoticeComponent } from '../../../../shared/ui/notice.component';

/**
 * Signs the customer in and takes them back to the page they came from. For a customer with a second factor the
 * API answers the right password by asking for a code; the form then shows the code field and sends both again.
 */
@Component({
  imports: [ReactiveFormsModule, RouterLink, AuthShellComponent, NoticeComponent],
  templateUrl: './login.component.html',
})
export class LoginComponent extends BasePage {
  private readonly auth = inject(AuthApi);
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly params = inject(ActivatedRoute).snapshot.queryParamMap;

  protected readonly notice = signal<string | null>(this.initialNotice());

  // Only presence is checked: the API deliberately does not say which part of a login was wrong.
  protected readonly form = inject(FormBuilder).nonNullable.group({
    tcNo: [this.params.get('tcNo') ?? '', Validators.required],
    password: ['', Validators.required],
    code: [''],
  });

  /** True once the API said this customer signs in with an authenticator code as well. */
  protected readonly needsCode = signal(false);

  protected submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.busy()) return;

    const { tcNo, password, code } = this.form.getRawValue();
    if (this.needsCode() && !/^\d{6}$/.test(code.replace(/\s/g, ''))) {
      this.error.set('Doğrulayıcı uygulamanızdaki 6 haneli kodu girin.');
      return;
    }

    this.act(this.auth.login(this.needsCode() ? { tcNo, password, code: code.replace(/\s/g, '') } : { tcNo, password }), {
      fallback: 'Giriş yapılamadı.',
      onDone: (response) => {
        this.session.start(response);
        this.router.navigateByUrl(this.safeReturnUrl());
      },
      onError: (err) => {
        if (err instanceof HttpErrorResponse && err.error?.secondFactorRequired === true) {
          // Not a failure: the password was right and the code comes next.
          this.error.set(null);
          this.needsCode.set(true);
        }
        this.form.controls.code.reset('');
      },
    });
  }

  private initialNotice(): string | null {
    if (this.params.get('registered') === '1') return 'Hesabınız oluşturuldu. Giriş yapabilirsiniz.';
    if (this.params.get('expired') === '1') return 'Oturumunuzun süresi doldu. Lütfen tekrar giriş yapın.';
    if (this.params.get('erased') === '1') return 'Hesabınız silindi.';
    return null;
  }

  // Only in-app paths are followed, so a crafted link cannot send the user to another site after login.
  private safeReturnUrl(): string {
    const url = this.params.get('returnUrl');
    return url && url.startsWith('/') && !url.startsWith('//') ? url : '/dashboard';
  }
}
