import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { ShareStatus } from '../../../data-access/models/split.models';

const LABELS: Record<ShareStatus, { text: string; tone: string }> = {
  Open: { text: 'Bekleniyor', tone: 'bg-brand-tint text-brand' },
  Paid: { text: 'Ödendi', tone: 'bg-positive-tint text-positive' },
  Declined: { text: 'Reddedildi', tone: 'bg-danger-tint text-danger' },
  Cancelled: { text: 'Geri çekildi', tone: 'bg-muted text-ink-soft' },
};

/** Where a share stands, in words and colour; the words alone are enough. */
@Component({
  selector: 'app-share-status',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="badge" [class]="label().tone">{{ label().text }}</span>`,
})
export class ShareStatusComponent {
  readonly status = input.required<ShareStatus>();
  protected readonly label = computed(() => LABELS[this.status()]);
}
