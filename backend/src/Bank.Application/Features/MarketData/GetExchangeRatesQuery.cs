using Bank.Application.Abstractions.MarketData;
using Bank.Contracts.MarketData;
using MediatR;

namespace Bank.Application.Features.MarketData;

/// <summary>Today's exchange rates for display and for estimating foreign currency balances in lira.</summary>
public sealed record GetExchangeRatesQuery : IRequest<ExchangeRatesResponse>;

public sealed class GetExchangeRatesQueryHandler : IRequestHandler<GetExchangeRatesQuery, ExchangeRatesResponse>
{
    private readonly IExchangeRates _rates;

    public GetExchangeRatesQueryHandler(IExchangeRates rates) => _rates = rates;

    public async Task<ExchangeRatesResponse> Handle(GetExchangeRatesQuery query, CancellationToken ct)
    {
        var snapshot = await _rates.GetAsync(ct);

        return new ExchangeRatesResponse(
            snapshot.Source,
            snapshot.PublishedOn,
            snapshot.FetchedAt,
            snapshot.Stale,
            snapshot.Rates.Select(r => new ExchangeRateResponse(r.CurrencyCode, r.Name, r.Buying, r.Selling)).ToList());
    }
}
