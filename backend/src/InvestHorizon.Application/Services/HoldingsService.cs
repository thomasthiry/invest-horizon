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

    public HoldingsService(
        ITransactionRepository transactions,
        IInstrumentRepository instruments,
        IInstrumentPriceRepository prices,
        IFxRateProvider fx)
    {
        _transactions = transactions;
        _instruments = instruments;
        _prices = prices;
        _fx = fx;
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

            var totalCostEur = g.Sum(t => t.TotalCost * (t.RemainingQuantity / t.Quantity));
            var avgCostEur = totalQty > 0 ? totalCostEur / totalQty : 0m;
            var avgCostNative = totalQty > 0 ? g.Sum(t => t.UnitPrice * t.RemainingQuantity) / totalQty : 0m;
            var first = g.First();

            decimal? currentPriceNative = null;
            string? priceCurrency = null;
            decimal? marketValueEur = null;
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
                    unrealizedGainEur = marketValueEur - totalCostEur;
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
                CurrentPriceNative: currentPriceNative,
                PriceCurrency: priceCurrency,
                MarketValueEur: marketValueEur,
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
    decimal? CurrentPriceNative = null,
    string? PriceCurrency = null,
    decimal? MarketValueEur = null,
    decimal? UnrealizedGainEur = null,
    DateTime? PriceAsOf = null,
    DateTime? PriceFetchedAt = null,
    string? PriceSource = null
);
