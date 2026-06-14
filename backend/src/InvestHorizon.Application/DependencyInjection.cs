using InvestHorizon.Application.CostEngine;
using InvestHorizon.Application.Interfaces;
using InvestHorizon.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace InvestHorizon.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, decimal annualTaxExemption = CapitalGainsTaxService.DefaultAnnualExemption)
    {
        services.AddSingleton<IBrokerFeeCalculator, KeytradeFeeCalculator>();
        services.AddSingleton<IBrokerFeeCalculator, RevolutFeeCalculator>();
        services.AddSingleton<ITobCalculator, BelgianTobCalculator>();
        services.AddSingleton<IFifoMatcher, FifoMatcher>();
        services.AddSingleton<ICapitalGainsTaxService>(_ => new CapitalGainsTaxService(annualTaxExemption));
        services.AddSingleton<TransactionCostEngine>();

        services.AddScoped<TransactionService>();
        services.AddScoped<HoldingsService>();
        services.AddScoped<RealizedGainsService>();
        services.AddScoped<PriceRefreshService>();
        services.AddScoped<ValuationHistoryService>();

        return services;
    }
}
