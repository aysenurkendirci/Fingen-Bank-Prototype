import { Injectable, computed, inject } from '@angular/core';

import { ThemeService } from './theme.service';

/**
 * How the app speaks. The standard mode addresses the customer formally; the young mode is on first-name terms
 * and plainer. A screen hands both wordings and gets the one that fits; reading it in a template follows a change
 * of mode at once.
 *
 * Known limit: so far the menu, the home screen and the goal pages speak in both voices; the other screens keep
 * the standard wording in either mode.
 */
@Injectable({ providedIn: 'root' })
export class VoiceService {
  private readonly theme = inject(ThemeService);

  readonly young = computed(() => this.theme.mode() === 'young');

  pick<T>(standard: T, young: T): T {
    return this.young() ? young : standard;
  }
}
