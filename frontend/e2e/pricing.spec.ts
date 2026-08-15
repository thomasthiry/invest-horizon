import { test, expect, type Page } from '@playwright/test';

const PORTFOLIO_ID = '11111111-1111-1111-1111-111111111111';

const now = new Date().toISOString();
const threeDaysAgo = new Date(Date.now() - 3 * 24 * 60 * 60 * 1000).toISOString();

// One fresh USD holding (FX-converted), one stale EUR holding, one with no quote yet.
const initialHoldings = [
  {
    instrumentId: 'a', isin: 'US0378331005', name: 'Apple', currency: 'USD',
    openQuantity: 10, avgCostEur: 100, totalInvestedEur: 1000,
    currentPriceNative: 200, priceCurrency: 'USD',
    marketValueEur: 1600, unrealizedGainEur: 600, priceAsOf: threeDaysAgo, priceSource: 'Yahoo',
  },
  {
    instrumentId: 'b', isin: 'IE00B4L5Y983', name: 'iShares World', currency: 'EUR',
    openQuantity: 5, avgCostEur: 80, totalInvestedEur: 400,
    currentPriceNative: 90, priceCurrency: 'EUR',
    marketValueEur: 450, unrealizedGainEur: 50, priceAsOf: threeDaysAgo, priceSource: 'Yahoo',
  },
  {
    instrumentId: 'c', isin: 'BE0974293251', name: 'Zeta Corp', currency: 'EUR',
    openQuantity: 3, avgCostEur: 50, totalInvestedEur: 150,
    currentPriceNative: null, priceCurrency: null,
    marketValueEur: null, unrealizedGainEur: null, priceAsOf: null, priceSource: null,
  },
];

// After refresh: every holding has a fresh quote.
const refreshedHoldings = initialHoldings.map(h => ({
  ...h,
  currentPriceNative: h.instrumentId === 'c' ? 55 : h.currentPriceNative,
  priceCurrency: 'EUR',
  marketValueEur: h.instrumentId === 'c' ? 165 : h.marketValueEur,
  unrealizedGainEur: h.instrumentId === 'c' ? 15 : h.unrealizedGainEur,
  priceAsOf: now,
  priceSource: 'Yahoo',
}));

async function login(page: Page) {
  await page.goto('/login');
  await page.getByTestId('email-input').fill('admin@investhorizon.local');
  await page.getByTestId('password-input').fill('Admin1234!');
  await page.getByTestId('login-button').click();
  await expect(page).toHaveURL('/');
}

test.describe('Live portfolio valuation', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/portfolios', route =>
      route.fulfill({ json: [{ id: PORTFOLIO_ID, name: 'Test Portfolio', baseCurrency: 'EUR' }] }));

    // Each holding row draws a sparkline from this endpoint; an empty series renders a dash.
    await page.route('**/price-history*', route => route.fulfill({ json: [] }));

    await page.route('**/holdings/refresh-prices', route =>
      route.fulfill({ json: refreshedHoldings }));

    await page.route('**/holdings', route =>
      route.fulfill({ json: initialHoldings }));
  });

  test('shows valuation, staleness banner, and refresh updates prices', async ({ page }) => {
    await login(page);

    // Holdings is the default tab.
    const banner = page.getByTestId('prices-asof');
    await expect(banner).toContainText('Prices as of');
    await expect(banner).toContainText('may be outdated');
    await expect(banner).toContainText('some positions have no quote');

    // Apple market value (10 * 200 / 1.25 = 1,600) and unrealized P/L (+600).
    const marketValues = page.getByTestId('market-value');
    await expect(marketValues.nth(0)).toContainText('1,600');
    await expect(page.getByTestId('unrealized-pl').nth(0)).toContainText('+600');

    // The unpriced holding renders a dash, not a value.
    await expect(marketValues.nth(2)).toHaveText('—');

    // Refresh prices -> banner no longer stale, and the previously-unpriced holding gets a value.
    await page.getByTestId('refresh-prices-btn').click();
    await expect(banner).not.toContainText('may be outdated');
    await expect(banner).not.toContainText('some positions have no quote');
    await expect(marketValues.nth(2)).toContainText('165');
  });
});
