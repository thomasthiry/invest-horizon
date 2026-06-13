using System.Security.Claims;
using InvestHorizon.Application.Interfaces;
using InvestHorizon.Application.Services;
using InvestHorizon.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestHorizon.Api.Controllers;

[ApiController]
[Route("api/portfolios/{portfolioId:guid}/transactions")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly TransactionService _service;
    private readonly ITransactionRepository _transactions;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public TransactionsController(TransactionService service, ITransactionRepository transactions)
    {
        _service = service;
        _transactions = transactions;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid portfolioId, CancellationToken ct)
    {
        var txs = await _transactions.GetByPortfolioAsync(portfolioId, ct);
        return Ok(txs.Select(ToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid portfolioId, [FromBody] CreateTransactionRequest req, CancellationToken ct)
    {
        try
        {
            var tx = await _service.CreateAsync(
                portfolioId, UserId,
                req.InstrumentId,
                req.Broker, req.Side,
                DateOnly.Parse(req.Date),
                req.UnitPrice, req.Quantity,
                req.Currency, req.FxRate,
                req.CustodyFee, req.ManualBrokerFee,
                ct);

            return CreatedAtAction(nameof(GetAll), new { portfolioId }, ToDto(tx));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid portfolioId, Guid id, [FromBody] UpdateTransactionRequest req, CancellationToken ct)
    {
        var tx = await _transactions.GetByIdAsync(id, ct);
        if (tx is null || tx.PortfolioId != portfolioId) return NotFound();

        tx.CustodyFee = req.CustodyFee;
        await _transactions.UpdateAsync(tx, ct);
        await _transactions.SaveChangesAsync(ct);
        return Ok(ToDto(tx));
    }

    private static TransactionDto ToDto(Domain.Entities.Transaction t) => new(
        t.Id, t.PortfolioId, t.InstrumentId,
        t.Instrument?.Isin, t.Instrument?.Name,
        t.Broker, t.Side,
        t.Date.ToString("yyyy-MM-dd"),
        t.UnitPrice, t.Quantity, t.Currency, t.FxRate,
        t.AmountNative, t.AmountEur,
        t.BrokerFee, t.TobAmount, t.TotalCost, t.NetProceeds,
        t.CustodyFee, t.RemainingQuantity
    );
}

[ApiController]
[Route("api/transactions")]
[Authorize]
public class TransactionPreviewController : ControllerBase
{
    private readonly TransactionService _service;
    private readonly IInstrumentRepository _instruments;

    public TransactionPreviewController(TransactionService service, IInstrumentRepository instruments)
    {
        _service = service;
        _instruments = instruments;
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] PreviewRequest req, CancellationToken ct)
    {
        var instrument = await _instruments.GetByIdAsync(req.InstrumentId, ct);
        if (instrument is null) return NotFound(new { message = "Instrument not found." });

        var preview = _service.PreviewCost(
            req.Broker, req.Side,
            req.UnitPrice, req.Quantity, req.FxRate,
            instrument.Type,
            req.ManualBrokerFee);

        return Ok(preview);
    }
}

public record CreateTransactionRequest(
    Guid InstrumentId,
    Broker Broker,
    TransactionSide Side,
    string Date,
    decimal UnitPrice,
    decimal Quantity,
    string Currency,
    decimal FxRate,
    decimal? CustodyFee,
    decimal? ManualBrokerFee
);

public record UpdateTransactionRequest(decimal? CustodyFee);

public record PreviewRequest(
    Guid InstrumentId,
    Broker Broker,
    TransactionSide Side,
    decimal UnitPrice,
    decimal Quantity,
    decimal FxRate,
    decimal? ManualBrokerFee
);

public record TransactionDto(
    Guid Id,
    Guid PortfolioId,
    Guid InstrumentId,
    string? Isin,
    string? InstrumentName,
    Broker Broker,
    TransactionSide Side,
    string Date,
    decimal UnitPrice,
    decimal Quantity,
    string Currency,
    decimal FxRate,
    decimal AmountNative,
    decimal AmountEur,
    decimal BrokerFee,
    decimal TobAmount,
    decimal TotalCost,
    decimal NetProceeds,
    decimal? CustodyFee,
    decimal RemainingQuantity
);
