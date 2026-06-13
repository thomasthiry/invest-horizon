using InvestHorizon.Application.Interfaces;
using InvestHorizon.Domain.Entities;
using InvestHorizon.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestHorizon.Api.Controllers;

[ApiController]
[Route("api/instruments")]
[Authorize]
public class InstrumentsController : ControllerBase
{
    private readonly IInstrumentRepository _instruments;
    public InstrumentsController(IInstrumentRepository instruments) => _instruments = instruments;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var list = await _instruments.GetAllAsync(ct);
        return Ok(list.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var inst = await _instruments.GetByIdAsync(id, ct);
        return inst is null ? NotFound() : Ok(ToDto(inst));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInstrumentRequest req, CancellationToken ct)
    {
        var existing = await _instruments.GetByIsinAsync(req.Isin, ct);
        if (existing is not null)
            return Ok(ToDto(existing));

        var inst = new Instrument
        {
            Id = Guid.NewGuid(),
            Isin = req.Isin,
            Name = req.Name,
            Type = req.Type,
            Currency = req.Currency,
            Ticker = req.Ticker
        };
        await _instruments.AddAsync(inst, ct);
        await _instruments.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = inst.Id }, ToDto(inst));
    }

    private static InstrumentDto ToDto(Instrument i) =>
        new(i.Id, i.Isin, i.Name, i.Type, i.Currency, i.Ticker);
}

public record InstrumentDto(Guid Id, string Isin, string Name, InstrumentType Type, string Currency, string? Ticker);
public record CreateInstrumentRequest(string Isin, string Name, InstrumentType Type, string Currency, string? Ticker);
