using System.Security.Claims;
using InvestHorizon.Application.Interfaces;
using InvestHorizon.Application.Services;
using InvestHorizon.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestHorizon.Api.Controllers;

[ApiController]
[Route("api/portfolios")]
[Authorize]
public class PortfoliosController : ControllerBase
{
    private readonly IPortfolioRepository _portfolios;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public PortfoliosController(IPortfolioRepository portfolios) => _portfolios = portfolios;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var list = await _portfolios.GetByUserAsync(UserId, ct);
        return Ok(list.Select(p => new PortfolioDto(p.Id, p.Name, p.BaseCurrency)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var p = await _portfolios.GetByIdAsync(id, UserId, ct);
        return p is null ? NotFound() : Ok(new PortfolioDto(p.Id, p.Name, p.BaseCurrency));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePortfolioRequest req, CancellationToken ct)
    {
        var portfolio = new Portfolio
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Name = req.Name,
            BaseCurrency = req.BaseCurrency ?? "EUR"
        };
        await _portfolios.AddAsync(portfolio, ct);
        await _portfolios.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = portfolio.Id }, new PortfolioDto(portfolio.Id, portfolio.Name, portfolio.BaseCurrency));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePortfolioRequest req, CancellationToken ct)
    {
        var portfolio = await _portfolios.GetByIdAsync(id, UserId, ct);
        if (portfolio is null) return NotFound();
        portfolio.Name = req.Name;
        await _portfolios.UpdateAsync(portfolio, ct);
        await _portfolios.SaveChangesAsync(ct);
        return Ok(new PortfolioDto(portfolio.Id, portfolio.Name, portfolio.BaseCurrency));
    }
}

public record PortfolioDto(Guid Id, string Name, string BaseCurrency);
public record CreatePortfolioRequest(string Name, string? BaseCurrency);
public record UpdatePortfolioRequest(string Name);
