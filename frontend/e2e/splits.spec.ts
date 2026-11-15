import { Page, expect, test } from '@playwright/test';

import { confirmDialog, field, newCustomer, toast } from './support';

/** Opens a lira account and returns its IBAN as printed on the accounts screen. */
async function openAccount(page: Page): Promise<string> {
  await page.goto('/dashboard/accounts');
  await page.getByRole('button', { name: 'Hesap aç' }).click();
  await page.getByRole('button', { name: 'Hesabı aç' }).click();
  await expect(page.locator('app-iban-copy')).toHaveCount(1);
  const printed = await page.locator('app-iban-copy').first().innerText();
  return printed.match(/TR[\d\s]+/)![0].replace(/\s+/g, '');
}

test.describe('splitting an expense', () => {
  test('a friend here is asked and declines, a friend outside is marked paid', async ({ page, browser }) => {
    // The friend: a customer of the bank with an account the payer can name.
    const friendContext = await browser.newContext();
    const friend = await friendContext.newPage();
    await newCustomer(friend);
    const friendIban = await openAccount(friend);

    await newCustomer(page);
    await openAccount(page);
    await page.goto('/dashboard/splits');
    await page.getByRole('button', { name: 'Harcama bölüş' }).click();
    await field(page, 'title').fill('Akşam yemeği');
    await field(page, 'totalAmount').fill('900');

    await page.getByRole('button', { name: 'Kişi ekle' }).click();
    const names = page.locator('[formcontrolname=name]');
    // Every test customer is called Deniz Kaya; the name is typed differently on purpose, as in a transfer.
    await names.nth(0).fill('deniz kaya');
    await page.locator('[formcontrolname=iban]').nth(0).fill(friendIban);
    await names.nth(1).fill('Can Yıldız');
    await page.getByRole('button', { name: 'Benimle eşit böl' }).click();
    await expect(page.getByText('Sizin payınız')).toContainText('300,00');
    await page.getByRole('button', { name: 'Bölüş ve iste' }).click();
    await expect(toast(page)).toHaveText('Harcama bölüşüldü, arkadaşlarınıza bildirim gitti.');

    const card = page.locator('app-split-card').filter({ hasText: 'Akşam yemeği' });
    await card.getByRole('button', { name: 'Ödendi say' }).last().click();
    await expect(card).toContainText('Can Yıldız');
    await expect(card.locator('li').filter({ hasText: 'Can Yıldız' })).toContainText('Ödendi');

    // The friend sees the request first, and declines it.
    await friend.goto('/dashboard/splits');
    const request = friend.locator('app-share-request-card').filter({ hasText: 'Akşam yemeği' });
    await expect(request).toContainText('300,00');
    await request.getByRole('button', { name: 'Reddet' }).click();
    await confirmDialog(friend, 'Reddet');
    await expect(request).toContainText('Reddedildi');

    await page.reload();
    await expect(card.locator('li').filter({ hasText: 'deniz kaya' })).toContainText('Reddedildi');
    await friendContext.close();
  });
});
