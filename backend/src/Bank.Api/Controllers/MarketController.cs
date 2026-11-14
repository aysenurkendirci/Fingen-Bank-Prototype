using Bank.Application.Features.MarketData;
using Bank.Contracts.MarketData;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bank.Api.Controllers;

/// <summary>Market information shown next to the customer's money: exchange rates for now.</summary>
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
}
