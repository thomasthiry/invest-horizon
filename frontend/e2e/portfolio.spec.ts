import { test, expect, type Page } from '@playwright/test';

async function login(page: Page) {
  await page.goto('/login');
  await page.getByTestId('email-input').fill('admin@investhorizon.local');
  await page.getByTestId('password-input').fill('Admin1234!');
  await page.getByTestId('login-button').click();
  await expect(page).toHaveURL('/');
}

test.describe('Instruments and Transactions', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('can add an ETF instrument', async ({ page }) => {
    await page.goto('/instruments');
    await page.getByTestId('add-instrument-btn').click();
    await page.getByTestId('isin-input').fill('IE000BI8OT95');
    await page.getByTestId('instrument-name-input').fill('Amundi Core MSCI World UCITS ETF Acc');
    await page.getByTestId('instrument-type-select').click();
    await page.getByRole('option', { name: 'ETF' }).click();
    await page.getByTestId('submit-instrument').click();
    await expect(page.getByText('IE000BI8OT95')).toBeVisible();
  });

  test('cost preview shows correct values for Keytrade ETF buy', async ({ page }) => {
    // Create a portfolio first via API shortcut if needed, then add transaction
    await page.goto('/');

    // Click Transactions tab
    await page.getByTestId('transactions-tab').click();
    await page.getByTestId('add-transaction-btn').click();

    // Select the MSCI World ETF
    await page.getByTestId('instrument-select').click();
    await page.getByText('Amundi Core MSCI World').click();

    // Broker: Keytrade (default)
    // Side: Buy (default)
    await page.getByTestId('unit-price-input').fill('139.09');
    await page.getByTestId('quantity-input').fill('1');

    // Wait for preview
    await expect(page.getByTestId('cost-preview')).toBeVisible();

    // TOB: 139.09 * 0.12% = 0.17 EUR
    await expect(page.getByTestId('cost-preview')).toContainText('0.17');
    // Broker fee: €7.95 (≤ €2,500 tier)
    await expect(page.getByTestId('cost-preview')).toContainText('7.95');
  });

  test('realized gains tax report renders', async ({ page }) => {
    await page.goto('/');
    await page.getByRole('tab', { name: 'Realized & Tax' }).click();
    await page.getByRole('button', { name: 'Load' }).click();
    await expect(page.getByTestId('tax-report')).toBeVisible();
    await expect(page.getByText('Annual Tax Summary')).toBeVisible();
  });
});
