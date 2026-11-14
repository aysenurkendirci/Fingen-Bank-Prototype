import { expect, test } from '@playwright/test';

import { confirmDialog, field, newCustomer, toast } from './support';

test.describe('accounts and transfers', () => {
  test.beforeEach(async ({ page }) => {
    await newCustomer(page);
  });

  test('accounts open in lira and dollars, and an empty one shows an empty statement', async ({ page }) => {
    await page.goto('/dashboard/accounts');

    for (const currency of ['Türk Lirası', 'ABD Doları']) {
      await page.getByRole('button', { name: 'Hesap aç' }).click();
      await page.locator('select[name=currency]').selectOption({ label: currency });
      await page.getByRole('button', { name: 'Hesabı aç' }).click();
      await expect(toast(page)).toHaveText('Hesabınız açıldı.');
    }
    await expect(page.locator('app-iban-copy')).toHaveCount(2);

    await page.getByRole('link', { name: 'Hareketler' }).first().click();
    await expect(page.locator('app-empty-state')).toContainText('henüz hareket yok');
  });

  test('a second empty lira account can be closed, the last one cannot', async ({ page }) => {
    await page.goto('/dashboard/accounts');
    for (let i = 0; i < 2; i++) {
      await page.getByRole('button', { name: 'Hesap aç' }).click();
      await page.getByRole('button', { name: 'Hesabı aç' }).click();
      await expect(page.locator('app-iban-copy')).toHaveCount(i + 1);
    }

    await page.getByRole('button', { name: 'Kapat' }).first().click();
    await confirmDialog(page, 'Hesabı kapat');
    await expect(toast(page)).toHaveText('Hesabınız kapatıldı.');

    await page.getByRole('button', { name: 'Kapat' }).first().click();
    await confirmDialog(page, 'Hesabı kapat');
    await expect(page.locator('.notice-error')).toContainText('Son vadesiz TL hesabınız kapatılamaz');
  });

  test('a transfer is checked before it is sent', async ({ page }) => {
    await page.goto('/dashboard/accounts');
    await page.getByRole('button', { name: 'Hesap aç' }).click();
    await page.getByRole('button', { name: 'Hesabı aç' }).click();
    await expect(page.locator('app-iban-copy')).toHaveCount(1);

    await page.goto('/dashboard/transfer');
    await field(page, 'targetIban').pressSequentially('TR330006100519786457841327');
    await page.locator('button[type=submit]').click();
    await expect(page.getByText('IBAN geçerli değil')).toBeVisible();
    await expect(field(page, 'targetIban')).toHaveValue('TR33 0006 1005 1978 6457 8413 27');

    await field(page, 'targetIban').fill('');
    await field(page, 'targetIban').pressSequentially('TR330006100519786457841326');
    await field(page, 'recipientName').fill('Ayşe Nur Kendirci');
    await field(page, 'amount').fill('50');
    await field(page, 'description').fill('Deneme');
    await page.locator('button[type=submit]').click();
    await expect(page.getByText('Tutar hesap bakiyesini aşıyor.')).toBeVisible();
  });
});
