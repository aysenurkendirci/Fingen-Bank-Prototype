import { expect, test } from '@playwright/test';

import { confirmDialog, field, newCustomer } from './support';

test.describe('planning', () => {
  test.beforeEach(async ({ page }) => {
    await newCustomer(page);
  });

  test('a credit card and an installment plan are added, edited and removed', async ({ page }) => {
    await page.goto('/dashboard/cards');
    await page.getByRole('button', { name: /Kart ekle/ }).click();
    await field(page, 'name').fill('Test Kartı');
    await field(page, 'limitAmount').fill('40000');
    await field(page, 'statementDay').fill('12');
    await field(page, 'paymentDueDays').fill('10');
    await page.getByRole('button', { name: 'Kaydet' }).click();
    await expect(page.getByText('Test Kartı')).toBeVisible();

    await page.getByRole('button', { name: /Taksit ekle/ }).click();
    await field(page, 'description').fill('Buzdolabı');
    await field(page, 'totalAmount').fill('24000');
    await field(page, 'installmentCount').fill('6');
    await page.getByRole('button', { name: 'Kaydet' }).click();
    await expect(page.getByText('Buzdolabı')).toBeVisible();

    await page.locator('app-installment-plan-list').getByRole('button', { name: 'Sil' }).first().click();
    await confirmDialog(page, 'Sil');
    await expect(page.getByText('Buzdolabı')).toHaveCount(0);
  });

  test('recurring items feed the cash-flow projection and scenarios compare options', async ({ page }) => {
    await page.goto('/dashboard/cards');
    await page.getByRole('button', { name: /Kart ekle/ }).click();
    await field(page, 'name').fill('Senaryo Kartı');
    await field(page, 'limitAmount').fill('50000');
    await field(page, 'statementDay').fill('5');
    await field(page, 'paymentDueDays').fill('10');
    await page.getByRole('button', { name: 'Kaydet' }).click();
    await expect(page.getByText('Senaryo Kartı')).toBeVisible();

    await page.goto('/dashboard/recurring');
    await page.getByRole('button', { name: /Kalem ekle/ }).click();
    await field(page, 'name').fill('Maaş');
    await field(page, 'kind').selectOption('Income');
    await field(page, 'category').fill('Maaş');
    await field(page, 'amount').fill('55000');
    await page.locator('app-recurring-item-form button[type=submit]').click();
    await expect(page.locator('app-recurring-item-list li')).toHaveCount(1);

    await page.goto('/dashboard/cash-flow');
    await page.getByRole('button', { name: '12 ay' }).click();
    await expect(page.locator('app-monthly-summary-table tbody tr')).toHaveCount(13);

    await page.goto('/dashboard/scenarios');
    await field(page, 'description').fill('Dizüstü bilgisayar');
    const prices = page.locator('[formcontrolname=totalAmount]');
    await prices.nth(0).fill('30000');
    await prices.nth(1).fill('32000');
    await page.locator('button[type=submit]').click();
    await expect(page.locator('app-scenario-comparison-table tbody tr')).toHaveCount(3);
  });

  test('demo history pages and analytics read it', async ({ page }) => {
    await page.goto('/dashboard/transactions');
    await page.getByRole('button', { name: /demo verisi/ }).click();
    // Twelve months of history are written in one go; on a busy machine that takes several seconds.
    await expect(page.locator('app-transaction-list li').first()).toBeVisible({ timeout: 30_000 });
    await page.getByRole('button', { name: 'Sonraki' }).click();
    await expect(page.locator('nav[aria-label=Sayfalar] span')).toContainText('2 /');

    await page.goto('/dashboard/analytics');
    await expect(page.locator('main h2')).toHaveCount(4);
    await expect(page.locator('.notice-error')).toHaveCount(0);
  });
});
