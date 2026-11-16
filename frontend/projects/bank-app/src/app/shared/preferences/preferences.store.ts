import { Injectable, inject } from '@angular/core';

import { Accent, Mode, ThemePreference, ThemeService } from '../../core/ui/theme.service';
import { ProfileApi } from '../../data-access/api/profile.api';
import { DisplayPreferences } from '../../data-access/models/banking.models';

/**
 * Keeps the look of the app in step with the customer's account. A change shows at once and is then saved, so
 * the next sign-in on any device opens the same way. On sign-in the saved choice replaces this device's.
 */
@Injectable({ providedIn: 'root' })
export class PreferencesStore {
  private readonly theme = inject(ThemeService);
  private readonly api = inject(ProfileApi);

  /** Takes the account's saved choice. A failure leaves this device's look as it is. */
  load(): void {
    this.api.preferences().subscribe({ next: (saved) => this.apply(saved), error: () => undefined });
  }

  chooseTheme(theme: ThemePreference): void {
    this.theme.set(theme);
    this.save();
  }

  chooseAccent(accent: Accent): void {
    this.theme.setAccent(accent);
    this.save();
  }

  chooseMode(mode: Mode): void {
    this.theme.setMode(mode);
    this.save();
  }

  private apply(saved: DisplayPreferences): void {
    this.theme.set(saved.theme);
    this.theme.setAccent(saved.accent);
    this.theme.setMode(saved.mode ?? 'standard');
  }

  // The look already changed on screen; if saving fails it simply stays local to this device.
  // Known limit: a save still on its way is lost if the page is closed or reloaded at that moment; the next
  // sign-in then brings back the earlier choice. Sending it with fetch's keepalive would let it finish.
  private save(): void {
    this.api
      .updatePreferences({ theme: this.theme.preference(), accent: this.theme.accent(), mode: this.theme.mode() })
      .subscribe({ error: () => undefined });
  }
}
