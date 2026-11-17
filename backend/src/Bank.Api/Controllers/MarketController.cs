using Bank.Api.Security;
using Bank.Application.Features.MarketData;
using Bank.Contracts.MarketData;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bank.Api.Controllers;

/// <summary>Market information shown next to the customer's money: exchange rates and a converter.</summary>
[ApiController]
[Route("api/market")]
[Authorize]
public sealed class MarketController : ControllerBase
{
    private readonly ISender _sender;

    public MarketController(ISender sender) => _sender = sender;

    [HttpGet("exchange-rates")]
    public async Task<ActionResult<ExchangeRatesResponse>> ExchangeRates(CancellationToken ct)
        => Ok(await _sender.Send(new GetExchangeRatesQuery(), ct));

    /// <summary>
    /// What an amount of one currency buys in another; indicative, not a transaction. Each call may reach the
    /// bank's calculator, so it shares the per-customer limit of money movements.
    /// </summary>
    [HttpPost("convert")]
    [EnableRateLimiting(MoneyRateLimiting.Policy)]
    public async Task<ActionResult<ConversionResponse>> Convert([FromBody] ConvertCurrencyRequest request, CancellationToken ct)
        => Ok(await _sender.Send(new ConvertCurrencyQuery(request), ct));
}
