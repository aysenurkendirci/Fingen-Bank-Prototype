import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { BalancePoint } from './running-balance';

const WIDTH = 240;
const HEIGHT = 64;
const PAD = 4;

/**
 * The balance over the coming days as a small step line, the same shape as the logo and the sign-in drawing but
 * with the customer's real numbers. It draws itself once; the figures stay in the tiles next to it, so the line
 * only has to show the shape: where it dips and where it recovers.
 */
@Component({
  selector: 'app-balance-sparkline',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <svg [attr.viewBox]="'0 0 ' + width + ' ' + height" class="w-full h-full overflow-visible" role="img" [attr.aria-label]="label()">
      @if (zeroY(); as y) {
        <line [attr.x1]="0" [attr.x2]="width" [attr.y1]="y" [attr.y2]="y" class="stroke-danger/50" stroke-width="1" stroke-dasharray="3 4" />
      }
      <path [attr.d]="path()" class="draw stroke-brand" pathLength="1" fill="none" stroke-width="2" stroke-linejoin="round" stroke-linecap="round" />
      @if (end(); as e) {
        <circle [attr.cx]="e.x" [attr.cy]="e.y" r="3.5" class="fill-brand" />
      }
    </svg>
  `,
  styles: `
    .draw {
      stroke-dasharray: 1;
      stroke-dashoffset: 1;
      animation: draw 1400ms var(--m-ease-enter) 150ms forwards;
    }
    @keyframes draw {
      to { stroke-dashoffset: 0; }
    }
    @media (prefers-reduced-motion: reduce) {
      .draw { stroke-dashoffset: 0; }
    }
  `,
})
export class BalanceSparklineComponent {
  readonly points = input.required<readonly BalancePoint[]>();
  readonly label = input('Önümüzdeki günlerde bakiyenin seyri');

  protected readonly width = WIDTH;
  protected readonly height = HEIGHT;

  private readonly scale = computed(() => {
    const values = this.points().map((p) => p.balance);
    const min = Math.min(0, ...values);
    const max = Math.max(...values);
    const span = max - min || 1;
    const step = (WIDTH - PAD * 2) / Math.max(1, values.length - 1);
    return {
      x: (i: number) => PAD + i * step,
      y: (v: number) => PAD + (HEIGHT - PAD * 2) * (1 - (v - min) / span),
      min,
    };
  });

  /** Steps rather than slopes: a balance changes on a day, it does not drift between days. */
  protected readonly path = computed(() => {
    const { x, y } = this.scale();
    return this.points()
      .map((p, i) => (i === 0 ? `M${x(0)} ${y(p.balance)}` : `H${x(i)} V${y(p.balance)}`))
      .join(' ');
  });

  protected readonly end = computed(() => {
    const points = this.points();
    if (points.length === 0) return null;
    const { x, y } = this.scale();
    return { x: x(points.length - 1), y: y(points[points.length - 1].balance) };
  });

  /** The zero line is drawn only when the balance actually goes below it. */
  protected readonly zeroY = computed(() => (this.scale().min < 0 ? this.scale().y(0) : null));
}
