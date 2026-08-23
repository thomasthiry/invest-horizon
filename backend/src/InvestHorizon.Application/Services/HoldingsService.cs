using InvestHorizon.Application.CostEngine;
using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Entities;
using InvestHorizon.Domain.Enums;

namespace InvestHorizon.Application.Services;

public sealed class HoldingsService
{
    private readonly ITransactionRepository _transactions;
    private readonly IInstrumentRepository _instruments;
    private readonly IInstrumentPriceRepository _prices;
    private readonly IFxRateProvider _fx;
    private readonly ExitCostEstimator _exitCosts;

    public HoldingsService(
        ITransactionRepository transactions,
        IInstrumentRepository instruments,
        IInstrumentPriceRepository prices,
        IFxRateProvider fx,
        ExitCostEstimator exitCosts)
    {
        _transactions = transactions;
        _instruments = instruments;
        _prices = prices;
        _fx = fx;
        _exitCosts = exitCosts;
    }

    public async Task<IReadOnlyList<HoldingDto>> GetHoldingsAsync(Guid portfolioId, CancellationToken ct = default)
    {
        var txs = await _transactions.GetByPortfolioAsync(portfolioId, ct);
        var buyLots = txs.Where(t => t.Side == TransactionSide.Buy && t.RemainingQuantity > 0).ToList();

        var priceList = await _prices.GetByPortfolioAsync(portfolioId, ct);
        var pricesByInstrument = priceList.ToDictionary(p => p.InstrumentId);

        var holdings = new List<HoldingDto>();
        foreach (var g in buyLots.GroupBy(t => t.InstrumentId))
        {
            var totalQty = g.Sum(t => t.RemainingQuantity);
            if (totalQty <= 0) continue;

            // Cost basis, and the same figure split into what was paid for the shares vs. what the
            // purchase itself cost, so the UI can show both sides of the P/L symmetrically.
            // TotalCost == AmountEur + BrokerFee + TobAmount, so the two parts add back up exactly.
            var totalCostEur = g.Sum(t => t.TotalCost * (t.RemainingQuantity / t.Quantity));
            var purchaseAmountEur = g.Sum(t => t.AmountEur * (t.RemainingQuantity / t.Quantity));
            var buyCostsEur = g.Sum(t => (t.BrokerFee + t.TobAmount) * (t.RemainingQuantity / t.Quantity));
            var avgCostEur = totalQty > 0 ? totalCostEur / totalQty : 0m;
            var avgCostNative = totalQty > 0 ? g.Sum(t => t.UnitPrice * t.RemainingQuantity) / totalQty : 0m;
            var first = g.First();

            decimal? currentPriceNative = null;
            string? priceCurrency = null;
            decimal? marketValueEur = null;
            decimal? estimatedSellCostsEur = null;
            IReadOnlyList<ExitCostOrder>? exitCostOrders = null;
            decimal? unrealizedGainEur = null;
            DateTime? priceAsOf = null;
            DateTime? priceFetchedAt = null;
            string? priceSource = null;

            if (pricesByInstrument.TryGetValue(g.Key, out var price))
            {
                currentPriceNative = price.PriceNative;
                priceCurrency = price.Currency;
                priceAsOf = price.AsOf;
                priceFetchedAt = price.FetchedAt;
                priceSource = price.Source;

                var eurRate = await _fx.GetEurRateAsync(price.Currency, ct); // 1 EUR = x native
                if (eurRate > 0)
                {
                    marketValueEur = totalQty * price.PriceNative / eurRate;

                    // Unrealized P/L is what closing the position today would actually leave:
                    // gross market value minus the exit costs (one sell order per broker), against
                    // a cost basis that already includes the buy-side fees and TOB.
                    // Instrument is always eager-loaded by the repository; the fallback is defensive only.
                    var instrumentType = first.Instrument?.Type ?? InstrumentType.Etf;
                    var exitCosts = _exitCosts.Estimate(g, price.PriceNative / eurRate, instrumentType);
                    estimatedSellCostsEur = exitCosts.TotalEur;
                    exitCostOrders = exitCosts.Orders;
                    unrealizedGainEur = marketValueEur - estimatedSellCostsEur - totalCostEur;
                }
            }

            holdings.Add(new HoldingDto(
                InstrumentId: g.Key,
                Isin: first.Instrument?.Isin ?? string.Empty,
                Name: first.Instrument?.Name ?? string.Empty,
                Currency: first.Currency,
                OpenQuantity: totalQty,
                AvgCostEur: avgCostEur,
                AvgCostNative: avgCostNative,
                TotalInvestedEur: totalCostEur,
                PurchaseAmountEur: purchaseAmountEur,
                BuyCostsEur: buyCostsEur,
                CurrentPriceNative: currentPriceNative,
                PriceCurrency: priceCurrency,
                MarketValueEur: marketValueEur,
                EstimatedSellCostsEur: estimatedSellCostsEur,
                ExitCostOrders: exitCostOrders,
                UnrealizedGainEur: unrealizedGainEur,
                PriceAsOf: priceAsOf,
                PriceFetchedAt: priceFetchedAt,
                PriceSource: priceSource
            ));
        }

        return holdings.OrderBy(h => h.Name).ToList();
    }
}

public record HoldingDto(
    Guid InstrumentId,
    string Isin,
    string Name,
    string Currency,
    decimal OpenQuantity,
    decimal AvgCostEur,
    decimal AvgCostNative,
    decimal TotalInvestedEur,
    // TotalInvestedEur split in two: what the shares themselves cost, and the broker fees + TOB
    // paid to acquire them. Both are already inside TotalInvestedEur; they are surfaced so the
    // UI can itemise the buy side the same way it itemises the exit side.
    decimal PurchaseAmountEur,
    decimal BuyCostsEur,
    decimal? CurrentPriceNative = null,
    string? PriceCurrency = null,
    decimal? MarketValueEur = null,
    // Broker fees + TOB that closing this position today would cost (one sell order per
    // broker). Already deducted from UnrealizedGainEur.
    decimal? EstimatedSellCostsEur = null,
    // Per-broker breakdown behind EstimatedSellCostsEur, so the UI can show the calculation.
    IReadOnlyList<ExitCostOrder>? ExitCostOrders = null,
    decimal? UnrealizedGainEur = null,
    DateTime? PriceAsOf = null,
    DateTime? PriceFetchedAt = null,
    string? PriceSource = null
);
