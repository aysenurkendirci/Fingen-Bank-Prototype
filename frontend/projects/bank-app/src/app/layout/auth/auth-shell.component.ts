import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { BrandMarkComponent } from '../../shared/ui/brand-mark.component';
import { BalanceLineComponent } from './balance-line.component';

/** The frame around the sign-in and sign-up forms: a brand panel on wide screens, the form beside it. */
@Component({
  selector: 'app-auth-shell',
  imports: [BrandMarkComponent, BalanceLineComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen lg:grid lg:grid-cols-[5fr_6fr]">
      <aside class="hidden lg:flex flex-col justify-between bg-tile text-tile-ink p-12">
        <app-brand-mark inverse class="text-xl" />
        <div class="max-w-md">
          <app-balance-line class="mb-10 text-tile-ink/80" />
          <p class="text-3xl leading-tight font-semibold tracking-[-0.02em]">Ay sonunu bugünden görün.</p>
          <p class="mt-4 text-tile-ink/70">Maaş, kira, kart ekstresi, taksit. Hepsi takvime oturur; bakiyenizin ne zaman daralacağını önceden bilirsiniz.</p>
        </div>
        <span class="text-sm text-tile-ink/50">Bu bir portföy projesidir, gerçek bir banka değildir.</span>
      </aside>

      <main class="flex items-center justify-center px-4 py-10">
        <div class="w-full max-w-sm">
          <app-brand-mark class="lg:hidden block mb-8 text-xl text-brand" />
          <h1 class="page-title">{{ heading() }}</h1>
          <p class="page-subtitle mb-8">{{ subtitle() }}</p>
          <ng-content />
        </div>
      </main>
    </div>
  `,
})
export class AuthShellComponent {
  readonly heading = input.required<string>();
  readonly subtitle = input.required<string>();
}
