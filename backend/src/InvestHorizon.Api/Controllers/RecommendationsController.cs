using System.Security.Claims;
using InvestHorizon.Application.Services;
using InvestHorizon.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestHorizon.Api.Controllers;

[ApiController]
[Route("api/recommendations")]
[Authorize]
public class RecommendationsController : ControllerBase
{
    private readonly RecommendationService _service;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public RecommendationsController(RecommendationService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? instrumentId, [FromQuery] string? source, CancellationToken ct)
    {
        var results = await _service.GetAllWithEvaluationAsync(UserId, instrumentId, source, ct);
        return Ok(results.Select(r => ToDto(r)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRecommendationRequest req, CancellationToken ct)
    {
        try
        {
            var rec = await _service.CreateAsync(
                UserId, req.InstrumentId, req.Source,
                req.Rating, DateOnly.Parse(req.Date), req.Comment, ct);
            return CreatedAtAction(nameof(GetAll), null, ToSingleDto(rec));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRecommendationRequest req, CancellationToken ct)
    {
        try
        {
            var rec = await _service.UpdateAsync(
                id, UserId, req.Source,
                req.Rating, DateOnly.Parse(req.Date), req.Comment, ct);
            return Ok(ToSingleDto(rec));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException) { return Forbid(); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _service.DeleteAsync(id, UserId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException) { return Forbid(); }
    }

    [HttpGet("sources")]
    public async Task<IActionResult> GetSources(CancellationToken ct)
    {
        var sources = await _service.GetDistinctSources(UserId, ct);
        return Ok(sources);
    }

    [HttpGet("scorecard")]
    public async Task<IActionResult> GetScorecard(CancellationToken ct)
    {
        var scorecard = await _service.GetScorecardAsync(UserId, ct);
        return Ok(scorecard);
    }

    private static RecommendationDto ToDto(RecommendationWithEvaluation r) => new(
        r.Recommendation.Id,
        r.Recommendation.InstrumentId,
        r.Recommendation.Instrument?.Isin,
        r.Recommendation.Instrument?.Name,
        r.Recommendation.Source,
        r.Recommendation.Rating,
        r.Recommendation.Date.ToString("yyyy-MM-dd"),
        r.Recommendation.Comment,
        r.Recommendation.CreatedAt,
        r.Evaluation is null ? null : new RecommendationEvaluationDto(
            r.Evaluation.PriceAtRec,
            r.Evaluation.CurrentPrice,
            r.Evaluation.ReturnSince,
            r.Evaluation.DirectionallyCorrect,
            r.Evaluation.PerformanceScore)
    );

    private static RecommendationDto ToSingleDto(Domain.Entities.Recommendation r) => new(
        r.Id, r.InstrumentId, r.Instrument?.Isin, r.Instrument?.Name,
        r.Source, r.Rating, r.Date.ToString("yyyy-MM-dd"),
        r.Comment, r.CreatedAt, null);
}

public record CreateRecommendationRequest(
    Guid InstrumentId,
    string Source,
    RecommendationRating Rating,
    string Date,
    string? Comment
);

public record UpdateRecommendationRequest(
    string Source,
    RecommendationRating Rating,
    string Date,
    string? Comment
);

public record RecommendationDto(
    Guid Id,
    Guid InstrumentId,
    string? Isin,
    string? InstrumentName,
    string Source,
    RecommendationRating Rating,
    string Date,
    string? Comment,
    DateTime CreatedAt,
    RecommendationEvaluationDto? Evaluation
);

public record RecommendationEvaluationDto(
    decimal PriceAtRec,
    decimal CurrentPrice,
    decimal ReturnSince,
    bool? DirectionallyCorrect,
    double PerformanceScore
);
