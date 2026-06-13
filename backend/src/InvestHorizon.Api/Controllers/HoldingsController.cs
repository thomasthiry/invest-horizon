using System.Security.Claims;
using InvestHorizon.Application.Interfaces;
using InvestHorizon.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestHorizon.Api.Controllers;

[ApiController]
[Route("api/portfolios/{portfolioId:guid}")]
[Authorize]
public class HoldingsController : ControllerBase
{
    private readonly HoldingsService _holdings;
    private readonly RealizedGainsService _realized;
    private readonly PriceRefreshService _priceRefresh;
    private readonly IPortfolioRepository _portfolios;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public HoldingsController(HoldingsService holdings, RealizedGainsService realized, PriceRefreshService priceRefresh, IPortfolioRepository portfolios)
    {
        _holdings = holdings;
        _realized = realized;
        _priceRefresh = priceRefresh;
        _portfolios = portfolios;
    }

    [HttpGet("holdings")]
    public async Task<IActionResult> GetHoldings(Guid portfolioId, CancellationToken ct)
    {
        if (await _portfolios.GetByIdAsync(portfolioId, UserId, ct) is null)
            return NotFound();
        var result = await _holdings.GetHoldingsAsync(portfolioId, ct);
        return Ok(result);
    }

    [HttpPost("holdings/refresh-prices")]
    public async Task<IActionResult> RefreshPrices(Guid portfolioId, CancellationToken ct)
    {
        if (await _portfolios.GetByIdAsync(portfolioId, UserId, ct) is null)
            return NotFound();
        await _priceRefresh.RefreshPortfolioAsync(portfolioId, ct);
        var result = await _holdings.GetHoldingsAsync(portfolioId, ct);
        return Ok(result);
    }

    [HttpGet("realized")]
    public async Task<IActionResult> GetRealized(Guid portfolioId, [FromQuery] int year, CancellationToken ct)
    {
        if (await _portfolios.GetByIdAsync(portfolioId, UserId, ct) is null)
            return NotFound();
        var result = await _realized.GetReportAsync(portfolioId, year, ct);
        return Ok(result);
    }
}
