using System.Net.Http.Headers;
using InvestHorizon.Application.Interfaces;
using InvestHorizon.Infrastructure.Persistence;
using InvestHorizon.Infrastructure.Persistence.Repositories;
using InvestHorizon.Infrastructure.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvestHorizon.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddIdentityCore<AppUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
        })
        .AddEntityFrameworkStores<AppDbContext>();

        services.AddScoped<IPortfolioRepository, PortfolioRepository>();
        services.AddScoped<IInstrumentRepository, InstrumentRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IInstrumentPriceRepository, InstrumentPriceRepository>();
        services.AddScoped<IInstrumentPriceHistoryRepository, InstrumentPriceHistoryRepository>();
        services.AddScoped<IFxRateHistoryRepository, FxRateHistoryRepository>();
        services.AddScoped<IRecommendationRepository, RecommendationRepository>();
        services.AddScoped<DatabaseSeeder>();

        // Market data (free, keyless). Provider-agnostic abstraction; Yahoo is the only implementation today.
        services.AddMemoryCache();
        services.AddHttpClient<IPriceProvider, YahooFinancePriceProvider>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(15);
            // Yahoo's public endpoints reject requests without a browser-like User-Agent.
            c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (InvestHorizon)");
            c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
        services.AddHttpClient<IFxRateProvider, FrankfurterFxRateProvider>(c =>
            c.Timeout = TimeSpan.FromSeconds(15));

        return services;
    }
}
