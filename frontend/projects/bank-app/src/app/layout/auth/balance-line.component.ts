import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * The sign-in screen's one moment: a balance line that draws itself once, dips towards zero on a card payment
 * and recovers. It is an illustration of what the app does, not anyone's data, so it carries no numbers.
 */
@Component({
  selector: 'app-balance-line',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block', 'aria-hidden': 'true' },
  template: `
    <svg viewBox="0 0 400 140" class="w-full h-auto overflow-visible" fill="none">
      <line x1="0" y1="118" x2="400" y2="118" class="stroke-current opacity-25" stroke-width="1" stroke-dasharray="2 6" />
      <path
        class="draw stroke-current"
        pathLength="1"
        stroke-width="2"
        stroke-linejoin="round"
        d="M0 70 H48 V58 H96 V92 H132 V104 H170 V48 H214 V62 H252 V40 H296 V54 H334 V26 H400"
      />
    </svg>
  `,
  styles: `
    .draw {
      stroke-dasharray: 1;
      stroke-dashoffset: 1;
      animation: draw 1600ms var(--m-ease-enter) 250ms forwards;
    }

    @keyframes draw {
      to { stroke-dashoffset: 0; }
    }

    @media (prefers-reduced-motion: reduce) {
      .draw { stroke-dashoffset: 0; }
    }
  `,
})
export class BalanceLineComponent {}
