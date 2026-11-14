import { expect, test } from '@playwright/test';

import { field, signIn, signUp } from './support';

test.describe('signing up and in', () => {
  test('the sign-up form explains what is missing', async ({ page }) => {
    await page.goto('/auth/register');
    await page.locator('button[type=submit]').click();

    await expect(page.locator('.field-error')).toHaveCount(6);
  });

  test('a new customer signs up, is greeted by name and signs out', async ({ page }) => {
    const customer = await signUp(page);
    await expect(page.locator('.notice-success')).toContainText('Hesabınız oluşturuldu');
    await expect(field(page, 'tcNo')).toHaveValue(customer.tcNo);

    await signIn(page, customer);
    await expect(page.locator('h1').first()).toContainText('Deniz');
    await expect(page).toHaveTitle(/Ana Sayfa \| FinGen/);

    await page.getByRole('button', { name: 'Çıkış' }).first().click();
    await page.waitForURL('**/auth/login**');
    await page.goto('/dashboard/accounts');
    await expect(page).toHaveURL(/auth\/login/);
  });

  test('a wrong password is refused without saying which part was wrong', async ({ page }) => {
    await signUp(page);

    await field(page, 'password').fill('yanlis-sifre-1');
    await page.locator('button[type=submit]').click();

    await expect(page.locator('.notice-error')).toContainText('hatalı');
    await expect(page).toHaveURL(/auth\/login/);
  });

  test('an unknown address shows the not found page', async ({ page }) => {
    await page.goto('/dashboard/olmayan-sayfa');

    await expect(page.locator('h1')).toHaveText('Sayfa bulunamadı');
  });
});
