import { ChangeDetectionStrategy, Component, DestroyRef, afterNextRender, inject, input, signal } from '@angular/core';

const INTERVAL_MS = 3800;

/**
 * The sign-in panel's headline, turning through what the app actually does. Each line slides up and out as the
 * next one slides in. The box keeps a fixed height, so nothing around it moves. It pauses while the pointer is
 * over it, and with reduced motion only the first line is shown. Screen readers get the first line only; the
 * rest repeats what the app offers and would be noise read aloud every few seconds.
 */
@Component({
  selector: 'app-rotating-headline',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'relative block h-[5.5rem] overflow-hidden',
    '(mouseenter)': 'paused = true',
    '(mouseleave)': 'paused = false',
  },
  template: `
    <span class="sr-only">{{ lines()[0] }}</span>
    @if (previous() !== null) {
      @for (line of [lines()[previous()!]]; track line) {
        <p class="line line--out" aria-hidden="true">{{ line }}</p>
      }
    }
    @for (line of [lines()[current()]]; track line) {
      <p class="line line--in" aria-hidden="true">{{ line }}</p>
    }
  `,
  styles: `
    .line {
      position: absolute;
      inset: 0 0 auto 0;
      font-size: 1.875rem;
      line-height: 1.2;
      font-weight: 600;
      letter-spacing: -0.02em;
      text-wrap: balance;
    }
    .line--in { animation: line-in 650ms var(--m-ease-enter) both; }
    .line--out { animation: line-out 450ms var(--m-ease) both; }

    @keyframes line-in {
      from { opacity: 0; transform: translateY(60%); filter: blur(2px); }
    }
    @keyframes line-out {
      to { opacity: 0; transform: translateY(-60%); filter: blur(2px); }
    }

    @media (prefers-reduced-motion: reduce) {
      .line--out { display: none; }
    }
  `,
})
export class RotatingHeadlineComponent {
  readonly lines = input.required<readonly string[]>();

  protected readonly current = signal(0);
  protected readonly previous = signal<number | null>(null);
  protected paused = false;

  constructor() {
    const destroyRef = inject(DestroyRef);

    afterNextRender(() => {
      if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) return;

      const timer = setInterval(() => {
        if (this.paused || document.hidden) return;
        const count = this.lines().length;
        this.previous.set(this.current());
        this.current.set((this.current() + 1) % count);
      }, INTERVAL_MS);

      destroyRef.onDestroy(() => clearInterval(timer));
    });
  }
}
