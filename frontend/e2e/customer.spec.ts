import { readFileSync } from 'node:fs';

import { expect, test } from '@playwright/test';

import { confirmDialog, field, newCustomer, signIn, toast } from './support';

test.describe('goals and the customer', () => {
  test('a goal gets a savings account, an auto-save rule, and closes when empty', async ({ page }) => {
    await newCustomer(page);
    await page.goto('/dashboard/goals');

    await page.getByRole('button', { name: 'Hedef ekle' }).click();
    await field(page, 'name').fill('Araba');
    await field(page, 'targetAmount').fill('400000');
    await page.getByRole('button', { name: 'Hedefi oluştur' }).click();
    const card = page.locator('app-goal-card').filter({ hasText: 'Araba' });
    await expect(card).toBeVisible();

    // An account to watch, since rules only follow lira demand accounts.
    await page.goto('/dashboard/accounts');
    await page.getByRole('button', { name: 'Hesap aç' }).click();
    await page.getByRole('button', { name: 'Hesabı aç' }).click();
    await expect(toast(page)).toHaveText('Hesabınız açıldı.');
    await page.goto('/dashboard/goals');
    await card.getByRole('button', { name: 'Otomatik birikim' }).click();
    await field(page, 'value').fill('10');
    await field(page, 'minIncoming').fill('30000');
    await page.getByRole('button', { name: 'Kuralı kaydet' }).click();
    await expect(card).toContainText('Gelen paradan %10');

    await card.getByRole('button', { name: 'Kapat' }).click();
    await confirmDialog(page, 'Kapat');
    await expect(toast(page)).toHaveText('Hedef kapatıldı.');
  });

  test('new year goals are kept apart and summed for their year', async ({ page }) => {
    await newCustomer(page);
    await page.goto('/dashboard/new-year-goals');

    await page.getByRole('button', { name: 'Hedef ekle' }).click();
    await expect(field(page, 'isResolution')).toBeChecked();
    await field(page, 'name').fill('İngilizce kursu');
    await field(page, 'targetAmount').fill('20000');
    await page.getByRole('button', { name: 'Hedefi oluştur' }).click();

    await expect(page.locator('section.panel').first()).toContainText('1 hedef');
  });

  test('a new password is announced in the notification centre and marked read when opened', async ({ page }) => {
    const customer = await newCustomer(page);
    await page.goto('/dashboard/profile');
    await field(page, 'currentPassword').fill(customer.password);
    await field(page, 'newPassword').fill('Yeni2026Sifre!x');
    await field(page, 'repeat').fill('Yeni2026Sifre!x');
    await page.getByRole('button', { name: 'Şifreyi değiştir' }).click();
    await expect(toast(page)).toHaveText('Şifreniz değiştirildi.');

    await page.goto('/dashboard/notifications');
    const item = page.getByRole('button', { name: /^Okunmadı\. Şifreniz değiştirildi/ });
    await expect(item).toBeVisible();
    await expect(page.locator('.unread-badge')).toContainText('1');

    await item.click();
    await expect(page.getByRole('button', { name: /^Şifreniz değiştirildi/ })).toBeVisible();
    await expect(page.locator('.unread-badge')).toHaveCount(0);
  });

  test('profile, theme and data follow the customer, and the account can be erased', async ({ page, browser }) => {
    const customer = await newCustomer(page);
    await page.goto('/dashboard/profile');

    await field(page, 'phone').fill('5559876543');
    await page.locator('app-contact-form button[type=submit]').click();
    await expect(toast(page)).toHaveText('İletişim bilgileriniz güncellendi.');

    const appearance = page.locator('app-appearance-settings');
    await appearance.getByRole('button', { name: 'Koyu' }).click();
    await appearance.getByRole('button', { name: 'Okyanus' }).click();
    await expect(page.locator('html')).toHaveAttribute('data-accent', 'ocean');

    // A fresh browser has nothing stored; the look comes from the account.
    const other = await browser.newPage();
    await signIn(other, customer);
    await expect(other.locator('html')).toHaveAttribute('data-theme', 'dark');
    await expect(other.locator('html')).toHaveAttribute('data-accent', 'ocean');
    await other.close();

    const [download] = await Promise.all([page.waitForEvent('download'), page.getByRole('button', { name: 'Verilerimi indir' }).click()]);
    const exported = JSON.parse(readFileSync((await download.path())!, 'utf8'));
    expect(exported.preferences.accent).toBe('ocean');

    await page.locator('#hesabi-kapat').getByRole('button', { name: 'Hesabımı sil' }).click();
    await page.locator('input[name=erasePassword]').fill(customer.password);
    await page.getByRole('button', { name: 'Hesabımı kalıcı olarak sil' }).click();
    await confirmDialog(page, 'Kalıcı olarak sil');
    await page.waitForURL('**/auth/login**');
    await expect(page.locator('.notice-success')).toContainText('Hesabınız silindi');
  });
});
