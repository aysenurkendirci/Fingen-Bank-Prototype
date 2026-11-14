import { registerLocaleData } from '@angular/common';
import localeTr from '@angular/common/locales/tr';
import { TestBed } from '@angular/core/testing';

import { BalancePrivacyService } from '../../core/ui/balance-privacy.service';
import { MoneyComponent } from './money.component';

describe('MoneyComponent', () => {
  beforeEach(() => {
    registerLocaleData(localeTr);
    const stored = new Map<string, string>();
    Object.defineProperty(globalThis, 'localStorage', {
      configurable: true,
      value: {
        getItem: (key: string) => stored.get(key) ?? null,
        setItem: (key: string, value: string) => stored.set(key, value),
        removeItem: (key: string) => stored.delete(key),
      },
    });
  });

  function render(inputs: Record<string, unknown>) {
    const fixture = TestBed.createComponent(MoneyComponent);
    for (const [name, value] of Object.entries(inputs)) fixture.componentRef.setInput(name, value);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('sets the kuruş apart in a headline figure', () => {
    const element = render({ amount: 183509.75, display: true });

    expect(element.textContent?.replace(/\s/g, '')).toBe('₺183.509,75');
    expect(element.querySelector('span')?.textContent).toBe(',75');
  });

  it('hides balances when asked, but never a receipt', () => {
    TestBed.inject(BalancePrivacyService).toggle();

    expect(render({ amount: 100 }).textContent?.trim()).toBe('••••••');
    expect(render({ amount: 100, concealable: false }).textContent?.replace(/\s/g, '')).toBe('₺100,00');
  });
});
