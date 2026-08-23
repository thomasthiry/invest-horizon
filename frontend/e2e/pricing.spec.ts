import { test, expect, type Page } from '@playwright/test';

const PORTFOLIO_ID = '11111111-1111-1111-1111-111111111111';

const now = new Date().toISOString();
const threeDaysAgo = new Date(Date.now() - 3 * 24 * 60 * 60 * 1000).toISOString();

// One fresh USD holding (FX-converted), one stale EUR holding, one with no quote yet.
const initialHoldings = [
  {
    instrumentId: 'a', isin: 'US0378331005', name: 'Apple', currency: 'USD',
    openQuantity: 10, avgCostEur: 100, totalInvestedEur: 1000,
    purchaseAmountEur: 990.55, buyCostsEur: 9.45,
    currentPriceNative: 200, priceCurrency: 'USD',
    // 1,600 gross − 11.55 exit costs − 1,000 invested = 588.45. All at one broker → one order.
    marketValueEur: 1600, estimatedSellCostsEur: 11.55, unrealizedGainEur: 588.45,
    exitCostOrders: [
      { broker: 'Keytrade', quantity: 10, unitPriceEur: 160, orderValueEur: 1600, brokerFeeEur: 5.95, tobEur: 5.60, totalEur: 11.55 },
    ],
    priceAsOf: threeDaysAgo, priceSource: 'Yahoo',
  },
  {
    instrumentId: 'b', isin: 'IE00B4L5Y983', name: 'iShares World', currency: 'EUR',
    openQuantity: 5, avgCostEur: 80, totalInvestedEur: 400,
    purchaseAmountEur: 393.10, buyCostsEur: 6.90,
    currentPriceNative: 90, priceCurrency: 'EUR',
    // Split across two brokers → two sell orders, both fees charged. 450 − 7.49 − 400 = 42.51.
    marketValueEur: 450, estimatedSellCostsEur: 7.49, unrealizedGainEur: 42.51,
    exitCostOrders: [
      { broker: 'Keytrade', quantity: 3, unitPriceEur: 90, orderValueEur: 270, brokerFeeEur: 5.95, tobEur: 0.32, totalEur: 6.27 },
      { broker: 'Revolut', quantity: 2, unitPriceEur: 90, orderValueEur: 180, brokerFeeEur: 1.00, tobEur: 0.22, totalEur: 1.22 },
    ],
    priceAsOf: threeDaysAgo, priceSource: 'Yahoo',
  },
  {
    instrumentId: 'c', isin: 'BE0974293251', name: 'Zeta Corp', currency: 'EUR',
    openQuantity: 3, avgCostEur: 50, totalInvestedEur: 150,
    purchaseAmountEur: 146.97, buyCostsEur: 3.03,
    currentPriceNative: null, priceCurrency: null,
    marketValueEur: null, estimatedSellCostsEur: null, unrealizedGainEur: null,
    exitCostOrders: null,
    priceAsOf: null, priceSource: null,
  },
];

// After refresh: every holding has a fresh quote.
const refreshedHoldings = initialHoldings.map(h => ({
  ...h,
  currentPriceNative: h.instrumentId === 'c' ? 55 : h.currentPriceNative,
  priceCurrency: 'EUR',
  marketValueEur: h.instrumentId === 'c' ? 165 : h.marketValueEur,
  // 3 × €55 = €165 at Keytrade: €2.45 (≤ €250 tier) + TOB 0.35% = €0.58.
  estimatedSellCostsEur: h.instrumentId === 'c' ? 3.03 : h.estimatedSellCostsEur,
  unrealizedGainEur: h.instrumentId === 'c' ? 11.97 : h.unrealizedGainEur,
  exitCostOrders: h.instrumentId === 'c'
    ? [{ broker: 'Keytrade', quantity: 3, unitPriceEur: 55, orderValueEur: 165, brokerFeeEur: 2.45, tobEur: 0.58, totalEur: 3.03 }]
    : h.exitCostOrders,
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

    // Apple market value (10 * 200 / 1.25 = 1,600) and unrealized P/L, net of exit costs (+588.45).
    const marketValues = page.getByTestId('market-value');
    await expect(marketValues.nth(0)).toContainText('1,600');
    await expect(page.getByTestId('unrealized-pl').nth(0)).toContainText('+588.45');

    // The headline is net of the summed exit costs of the priced positions (11.55 + 7.49).
    await expect(page.getByTestId('exit-costs-note')).toContainText('19.04');

    // Hovering a P/L spells out the calculation with this position's own numbers. The
    // iShares position sits at two brokers, so both sell orders are listed.
    await page.getByTestId('unrealized-pl').nth(1).hover();
    const breakdown = page.getByTestId('unrealized-breakdown');
    // Currency symbol placement is locale-dependent, so match on the figures themselves.
    await expect(breakdown).toContainText('2 sell orders');
    await expect(breakdown).toContainText(/Keytrade:\s*3\s*×/);
    await expect(breakdown).toContainText(/fee\s*\D*5[.,]95\s*\+\s*TOB\s*\D*0[.,]32\s*=\s*\D*6[.,]27/);
    await expect(breakdown).toContainText(/Revolut:\s*2\s*×/);
    await expect(breakdown).toContainText(/fee\s*\D*1[.,]00\s*\+\s*TOB\s*\D*0[.,]22\s*=\s*\D*1[.,]22/);

    // Both sides of the trade are itemised: the buy costs are not hidden inside "invested".
    await expect(breakdown).toContainText('Buy costs (fees + TOB)');
    await expect(breakdown).toContainText(/6[.,]90/);   // buyCostsEur
    await expect(breakdown).toContainText(/393[.,]10/); // purchaseAmountEur

    // The unpriced holding renders a dash, not a value.
    await expect(marketValues.nth(2)).toHaveText('—');

    // Refresh prices -> banner no longer stale, and the previously-unpriced holding gets a value.
    await page.getByTestId('refresh-prices-btn').click();
    await expect(banner).not.toContainText('may be outdated');
    await expect(banner).not.toContainText('some positions have no quote');
    await expect(marketValues.nth(2)).toContainText('165');
  });
});
