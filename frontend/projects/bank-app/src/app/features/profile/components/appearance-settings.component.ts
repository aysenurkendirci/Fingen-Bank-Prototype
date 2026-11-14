import { ChangeDetectionStrategy, Component, inject } from '@angular/core';

import { ACCENTS, ThemePreference, ThemeService } from '../../../core/ui/theme.service';
import { PreferencesStore } from '../../../shared/preferences/preferences.store';

/**
 * Light, dark or the system setting, and the accent colour. Every change shows at once, in the small preview and
 * across the whole app, and is saved to the account so other devices open the same way.
 */
@Component({
  selector: 'app-appearance-settings',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <fieldset>
      <legend class="field-label mb-2">Görünüm</legend>
      <div class="segmented">
        @for (option of modes; track option.value) {
          <button type="button" [attr.aria-pressed]="theme.preference() === option.value" (click)="store.chooseTheme(option.value)">
            <span class="material-symbols-outlined text-[18px]" aria-hidden="true">{{ option.icon }}</span>
            {{ option.label }}
          </button>
        }
      </div>
      <p class="mt-2 text-xs text-ink-faint">Sistem seçiliyken cihazınızın açık ya da koyu ayarını izler.</p>
    </fieldset>

    <fieldset class="mt-6">
      <legend class="field-label mb-2">Vurgu rengi</legend>
      <div class="flex flex-wrap gap-2">
        @for (accent of accents; track accent.value) {
          <button
            type="button"
            class="swatch"
            [attr.aria-pressed]="theme.accent() === accent.value"
            (click)="store.chooseAccent(accent.value)"
          >
            <span class="size-4 rounded-full" [style.background]="theme.theme() === 'dark' ? accent.swatch.dark : accent.swatch.light"></span>
            {{ accent.label }}
          </button>
        }
      </div>
    </fieldset>

    <div class="mt-6 rounded-panel border border-line bg-canvas p-4 flex flex-wrap items-center justify-between gap-4" aria-label="Önizleme">
      <div>
        <p class="text-xs text-ink-faint">Önizleme</p>
        <p class="text-lg font-semibold tabular-nums">₺12.480<span class="text-[0.6em] font-medium">,50</span></p>
        <span class="btn-link">Hesap hareketleri</span>
      </div>
      <span class="btn-primary pointer-events-none">Para gönder</span>
    </div>
  `,
})
export class AppearanceSettingsComponent {
  protected readonly theme = inject(ThemeService);
  protected readonly store = inject(PreferencesStore);
  protected readonly accents = ACCENTS;

  protected readonly modes: readonly { value: ThemePreference; label: string; icon: string }[] = [
    { value: 'system', label: 'Sistem', icon: 'contrast' },
    { value: 'light', label: 'Açık', icon: 'light_mode' },
    { value: 'dark', label: 'Koyu', icon: 'dark_mode' },
  ];
}
