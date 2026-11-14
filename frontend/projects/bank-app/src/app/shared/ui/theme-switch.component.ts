import { ChangeDetectionStrategy, Component, inject } from '@angular/core';

import { ThemePreference, ThemeService } from '../../core/ui/theme.service';

/** System, light or dark, as one segmented control. */
@Component({
  selector: 'app-theme-switch',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="segmented" role="group" aria-label="Görünüm">
      @for (option of options; track option.value) {
        <button type="button" [attr.aria-pressed]="theme.preference() === option.value" (click)="theme.set(option.value)">
          <span class="material-symbols-outlined text-[18px]" aria-hidden="true">{{ option.icon }}</span>
          {{ option.label }}
        </button>
      }
    </div>
  `,
})
export class ThemeSwitchComponent {
  protected readonly theme = inject(ThemeService);

  protected readonly options: readonly { value: ThemePreference; label: string; icon: string }[] = [
    { value: 'system', label: 'Sistem', icon: 'contrast' },
    { value: 'light', label: 'Açık', icon: 'light_mode' },
    { value: 'dark', label: 'Koyu', icon: 'dark_mode' },
  ];
}
