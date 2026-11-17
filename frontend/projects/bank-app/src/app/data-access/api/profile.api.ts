import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { AuthResponse } from '../models/auth.models';
import { ContactDetails, DisplayPreferences, PasswordChange, Profile, TwoFactorSetup, TwoFactorStatus } from '../models/banking.models';
import { BaseApi } from './base-api';

/** The signed-in customer's own data. */
@Injectable({ providedIn: 'root' })
export class ProfileApi extends BaseApi {
  protected readonly root = '/api/me';

  get(): Observable<Profile> {
    return this.http.get<Profile>(this.url());
  }

  updateContact(details: ContactDetails): Observable<Profile> {
    return this.http.put<Profile>(this.url(), details);
  }

  /** How the customer wants the app to look, saved with their account. */
  preferences(): Observable<DisplayPreferences> {
    return this.http.get<DisplayPreferences>(this.url('/preferences'));
  }

  /**
   * Sent with keepalive: the browser finishes the save even when the page is closed or reloaded right after a
   * change, so the next sign-in brings back the latest choice.
   */
  updatePreferences(preferences: DisplayPreferences): Observable<DisplayPreferences> {
    return this.http.put<DisplayPreferences>(this.url('/preferences'), preferences, { keepalive: true });
  }

  /** Ends every other session; the answer is the new token for this one. */
  changePassword(change: PasswordChange): Observable<AuthResponse> {
    return this.http.put<AuthResponse>(this.url('/password'), change);
  }

  /** Whether sign-in asks for an authenticator app code as well as the password. */
  twoFactor(): Observable<TwoFactorStatus> {
    return this.http.get<TwoFactorStatus>(this.url('/two-factor'));
  }

  /** A new key for the authenticator app, shown once; not in use until confirmed with a code. */
  startTwoFactor(): Observable<TwoFactorSetup> {
    return this.http.post<TwoFactorSetup>(this.url('/two-factor/setup'), null);
  }

  confirmTwoFactor(code: string): Observable<TwoFactorStatus> {
    return this.http.post<TwoFactorStatus>(this.url('/two-factor/confirm'), { code });
  }

  disableTwoFactor(password: string, code: string): Observable<TwoFactorStatus> {
    return this.http.delete<TwoFactorStatus>(this.url('/two-factor'), { body: { password, code } });
  }

  /** Everything stored about the signed-in user, as a file (KVKK right of access). */
  exportPersonalData(): Observable<Blob> {
    return this.http.get(this.url('/data-export'), { responseType: 'blob' });
  }

  /** KVKK right to erasure. Final, and refused while the accounts still hold money. */
  erase(password: string): Observable<void> {
    return this.http.delete<void>(this.url(), { body: { password } });
  }
}
