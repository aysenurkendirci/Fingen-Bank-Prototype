import { readFileSync } from 'node:fs';

import { expect, test } from '@playwright/test';

import { confirmDialog, field, newCustomer, signIn, toast } from './support';

// The smallest PNG there is: a 1x1 transparent pixel.
const PNG = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==';

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

  test('a wish on the list keeps its details, its years and a photo', async ({ page }) => {
    await newCustomer(page);
    await page.goto('/dashboard/new-year-goals');

    await page.getByRole('button', { name: 'Dilek ekle' }).click();
    await expect(field(page, 'isResolution')).toBeChecked();
    await page.getByRole('button', { name: 'Araba' }).click();
    await field(page, 'name').fill('İlk arabam');
    await field(page, 'details').fill('Elektrikli, beyaz, otomatik vites');
    await field(page, 'targetAmount').fill('900000');
    await field(page, 'horizon').selectOption({ index: 2 });
    await page.locator('input[name=photo]').setInputFiles({ name: 'araba.png', mimeType: 'image/png', buffer: Buffer.from(PNG, 'base64') });
    await expect(page.getByAltText('Seçilen fotoğraf')).toBeVisible();
    await page.getByRole('button', { name: 'Hedefi oluştur' }).click();
    await expect(toast(page)).toHaveText('Dileğiniz listenize eklendi, kumbarası açıldı.');

    const card = page.locator('app-goal-card').filter({ hasText: 'İlk arabam' });
    await expect(card).toContainText('Elektrikli, beyaz, otomatik vites');
    await expect(card).toContainText('3 yıllık hedef');
    await expect(card.getByRole('img', { name: 'İlk arabam' })).toBeVisible();
    await expect(page.locator('section.panel').first()).toContainText('1 dilek');
  });

  test('the young mode changes the look and the words, and follows the customer', async ({ page }) => {
    const customer = await newCustomer(page);
    await page.goto('/dashboard/profile');
    // No waiting for the save: it is sent with keepalive, so leaving the page right away must not lose it.
    await page.getByRole('button', { name: /Genç/ }).click();

    await expect(page.locator('html')).toHaveAttribute('data-mode', 'young');
    await expect(page.getByRole('navigation', { name: 'Ana menü' }).first()).toContainText('Kumbaraların');

    await page.goto('/dashboard');
    await expect(page.locator('app-savings-spotlight')).toContainText('İlk kumbaranı aç');

    // Another device: the choice comes back with the account.
    await page.evaluate(() => localStorage.clear());
    await signIn(page, customer);
    await expect(page.locator('html')).toHaveAttribute('data-mode', 'young');
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
    // The sign-in itself is announced too, so two are unread.
    await expect(page.getByRole('button', { name: /^Okunmadı\. Hesabınıza giriş yapıldı/ })).toBeVisible();
    await expect(page.locator('.unread-badge')).toContainText('2');

    await item.click();
    await expect(page.getByRole('button', { name: /^Şifreniz değiştirildi/ })).toBeVisible();
    await expect(page.locator('.unread-badge')).toContainText('1');

    await page.getByRole('button', { name: 'Tümünü okundu say' }).click();
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
