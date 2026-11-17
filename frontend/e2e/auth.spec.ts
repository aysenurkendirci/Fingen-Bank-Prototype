import { createHmac } from 'node:crypto';

import { expect, test } from '@playwright/test';

import { field, newCustomer, signIn, signUp } from './support';

/** RFC 6238 code for a Base32 secret, `offset` steps from now; what an authenticator app would show. */
function totp(secret: string, offset = 0): string {
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  let bits = '';
  for (const c of secret.replace(/\s/g, '')) bits += alphabet.indexOf(c).toString(2).padStart(5, '0');
  const key = Buffer.from(bits.match(/.{8}/g)!.map((b) => parseInt(b, 2)));
  const counter = Buffer.alloc(8);
  counter.writeBigUInt64BE(BigInt(Math.floor(Date.now() / 30_000) + offset));
  const hash = createHmac('sha1', key).update(counter).digest();
  const o = hash[hash.length - 1] & 0x0f;
  return String((hash.readUInt32BE(o) & 0x7fffffff) % 1_000_000).padStart(6, '0');
}

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

  test('with two-step verification on, signing in asks for the authenticator code', async ({ page }) => {
    const customer = await newCustomer(page);
    await page.goto('/dashboard/profile');
    await page.getByRole('button', { name: 'İki adımlı doğrulamayı aç' }).click();
    await expect(page.getByAltText('Doğrulayıcı uygulama için QR kod')).toBeVisible();
    const secret = (await page.getByTestId('two-factor-secret').innerText()).replace(/\s/g, '');

    await page.locator('input[name=confirmCode]').fill(totp(secret));
    await page.getByRole('button', { name: 'Doğrula ve aç' }).click();
    await expect(page.locator('#iki-adim')).toContainText('Açık');

    await page.getByRole('button', { name: 'Çıkış' }).first().click();
    await page.waitForURL('**/auth/login**');
    await field(page, 'tcNo').fill(customer.tcNo);
    await field(page, 'password').fill(customer.password);
    await page.locator('button[type=submit]').click();
    await expect(field(page, 'code')).toBeVisible();

    // The code used to switch it on cannot sign in again; the next one can.
    await field(page, 'code').fill(totp(secret, 1));
    await page.locator('button[type=submit]').click();
    await page.waitForURL('**/dashboard**');
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
