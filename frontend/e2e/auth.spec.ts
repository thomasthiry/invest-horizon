import { test, expect } from '@playwright/test';

test('login with valid credentials', async ({ page }) => {
  await page.goto('/login');
  await page.getByTestId('email-input').fill('admin@investhorizon.local');
  await page.getByTestId('password-input').fill('Admin1234!');
  await page.getByTestId('login-button').click();
  await expect(page).toHaveURL('/');
  await expect(page.getByText('InvestHorizon')).toBeVisible();
});

test('login with wrong password shows error', async ({ page }) => {
  await page.goto('/login');
  await page.getByTestId('email-input').fill('admin@investhorizon.local');
  await page.getByTestId('password-input').fill('wrong');
  await page.getByTestId('login-button').click();
  await expect(page.getByText('Invalid email or password')).toBeVisible();
});
