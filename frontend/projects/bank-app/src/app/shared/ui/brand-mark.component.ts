import { ChangeDetectionStrategy, Component } from '@angular/core';

import { BRAND_NAME } from '../../core/brand';

/** The product's name as a wordmark. Colour and size come from where it is placed. */
@Component({
  selector: 'app-brand-mark',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'font-semibold tracking-[-0.02em]' },
  template: `{{ name }}`,
})
export class BrandMarkComponent {
  protected readonly name = BRAND_NAME;
}
