import { Page, expect } from '@playwright/test';

/** A valid T.C. identity number that is different on every call, so every test signs up its own customer. */
export function newTcNo(): string {
  const d = [1 + Math.floor(Math.random() * 9), ...Array.from({ length: 8 }, () => Math.floor(Math.random() * 10))];
  const tenth = ((((d[0] + d[2] + d[4] + d[6] + d[8]) * 7 - (d[1] + d[3] + d[5] + d[7])) % 10) + 10) % 10;
  const eleventh = (d.reduce((a, b) => a + b, 0) + tenth) % 10;
  return d.join('') + tenth + eleventh;
}

export interface Customer {
  tcNo: string;
  password: string;
}

export const field = (page: Page, name: string) => page.locator(`[formcontrolname=${name}]`);

/** Signs up through the form and lands on the sign-in page with the identity number filled in. */
export async function signUp(page: Page, password = 'Deneme1234!'): Promise<Customer> {
  const tcNo = newTcNo();
  await page.goto('/auth/register');
  await field(page, 'firstName').fill('Deniz');
  await field(page, 'lastName').fill('Kaya');
  await field(page, 'tcNo').fill(tcNo);
  await field(page, 'email').fill(`deniz.${tcNo}@example.com`);
  await field(page, 'phone').fill('5551234567');
  await field(page, 'password').fill(password);
  await page.locator('button[type=submit]').click();
  await page.waitForURL('**/auth/login**');
  return { tcNo, password };
}

export async function signIn(page: Page, customer: Customer): Promise<void> {
  await page.goto('/auth/login');
  await field(page, 'tcNo').fill(customer.tcNo);
  await field(page, 'password').fill(customer.password);
  await page.locator('button[type=submit]').click();
  await page.waitForURL('**/dashboard**');
}

export async function newCustomer(page: Page): Promise<Customer> {
  const customer = await signUp(page);
  await signIn(page, customer);
  return customer;
}

export const toast = (page: Page) => page.locator('app-toast-host p').last();

/** Confirms the app's own dialog (not the browser's). */
export async function confirmDialog(page: Page, button: string): Promise<void> {
  const dialog = page.locator('dialog[open]');
  await expect(dialog).toBeVisible();
  await dialog.getByRole('button', { name: button }).click();
}
