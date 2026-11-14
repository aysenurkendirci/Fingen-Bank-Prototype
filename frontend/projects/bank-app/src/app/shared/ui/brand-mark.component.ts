import { ChangeDetectionStrategy, Component, booleanAttribute, input } from '@angular/core';

import { BRAND_NAME } from '../../core/brand';

/**
 * The FinGen logo: a rounded tile in the accent colour holding a balance line that steps up and ends in a dot,
 * money growing over time, the thing the app is built around. Next to it the name. Colours come from the theme,
 * so the logo follows light, dark and the chosen accent.
 */
@Component({
  selector: 'app-brand-mark',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'inline-flex items-center gap-2 font-semibold tracking-[-0.02em]' },
  template: `
    <svg viewBox="0 0 32 32" class="size-[1.6em] shrink-0" aria-hidden="true">
      <rect width="32" height="32" rx="9" [class]="inverse() ? 'fill-tile-ink' : 'fill-brand'" />
      <path
        d="M7.5 21.5h5.25v-4.5h5.25v-4.75h4.5"
        fill="none"
        stroke-width="2.6"
        stroke-linecap="round"
        stroke-linejoin="round"
        [class]="inverse() ? 'stroke-tile' : 'stroke-brand-contrast'"
      />
      <circle cx="24.5" cy="12.25" r="2.3" [class]="inverse() ? 'fill-tile' : 'fill-brand-contrast'" />
    </svg>
    @if (!markOnly()) {
      <span>{{ name }}</span>
    }
  `,
})
export class BrandMarkComponent {
  /** On a coloured panel the tile turns light and the line takes the panel's colour. */
  readonly inverse = input(false, { transform: booleanAttribute });
  readonly markOnly = input(false, { transform: booleanAttribute });

  protected readonly name = BRAND_NAME;
}
